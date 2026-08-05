using System.Security.Cryptography;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using ConexionBroker;

var builder = WebApplication.CreateBuilder(args);

// El broker escucha solo en 127.0.0.1 (detrás de Caddy), así que la IP real del
// cliente llega en X-Forwarded-For. Sin esto, RemoteIpAddress sería siempre la
// del proxy (127.0.0.1) y el anti fuerza bruta por IP metería a todos en la misma
// bolsa. Solo Caddy puede alcanzar el puerto local, por eso se confía en el header.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

// ─── Anti fuerza bruta del login ──────────────────────────────────────────
// Umbrales configurables (appsettings o variables Login__*). Por defecto: 5
// fallos por cuenta y 20 por IP en 15 min bloquean 15 min. El de IP es más alto
// para no dejar afuera una oficina entera detrás de un mismo NAT.
var loginCfg    = app.Configuration.GetSection("Login");
int maxCuenta   = loginCfg.GetValue("MaxFallosCuenta", 5);
int maxIp       = loginCfg.GetValue("MaxFallosIp", 20);
int ventanaMin  = loginCfg.GetValue("VentanaMinutos", 15);
int bloqueoMin  = loginCfg.GetValue("BloqueoMinutos", 15);
var throttleCuenta = new LoginThrottle(maxCuenta, TimeSpan.FromMinutes(ventanaMin), TimeSpan.FromMinutes(bloqueoMin));
var throttleIp     = new LoginThrottle(maxIp,     TimeSpan.FromMinutes(ventanaMin), TimeSpan.FromMinutes(bloqueoMin));

// ─── Almacenamiento de documentos (idea #1: el broker es la nube) ──────────
// Los archivos se guardan por hash (content-addressed) en una carpeta del VPS.
// La referencia (el hash) se guarda en la base (documentosF.archivo); acá solo
// viven los bytes. Configurable por appsettings o variables Documentos__*.
var docCfg     = app.Configuration.GetSection("Documentos");
string docDir  = docCfg["Carpeta"] ?? "/opt/documentos";
long   docMax  = docCfg.GetValue<long>("MaxBytes", 26_214_400); // 25 MB por archivo
int    tokenHoras = app.Configuration.GetValue("Login:TokenHoras", 12);
var tokens     = new TokenStore(TimeSpan.FromHours(tokenHoras));
try { Directory.CreateDirectory(docDir); }
catch (Exception ex) { app.Logger.LogError(ex, "No se pudo crear la carpeta de documentos {dir}.", docDir); }

// Credenciales REALES de SQL Server: viven únicamente acá (appsettings.json
// local sin commitear, o variables de entorno Sql__Servidor/Sql__BaseDatos/
// Sql__Usuario/Sql__Contrasena — ver README.md). La app WPF nunca las guarda
// en disco: las recibe recién tras un login válido, solo en memoria.
string ConnectionString()
{
    var cfg = app.Configuration;
    string servidor   = cfg["Sql:Servidor"]   ?? throw new InvalidOperationException("Falta configurar Sql:Servidor.");
    string baseDatos  = cfg["Sql:BaseDatos"]  ?? throw new InvalidOperationException("Falta configurar Sql:BaseDatos.");
    string usuario    = cfg["Sql:Usuario"]    ?? throw new InvalidOperationException("Falta configurar Sql:Usuario.");
    string contrasena = cfg["Sql:Contrasena"] ?? throw new InvalidOperationException("Falta configurar Sql:Contrasena.");
    return $"Server={servidor};Database={baseDatos};User Id={usuario};Password={contrasena};" +
           "Application Name=ConexionBroker;Connect Timeout=10;Command Timeout=10;TrustServerCertificate=True;";
}

// ─── /ping: confirma que el broker Y la base de datos detrás responden ─────
// (no requiere login: la app lo usa antes de mostrar la pantalla de ingreso,
// igual que antes se probaba la conexión directa a SQL Server).
app.MapGet("/ping", async () =>
{
    try
    {
        using var conn = new SqlConnection(ConnectionString());
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync();
        return Results.Ok();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Fallo /ping: no se pudo conectar a SQL Server.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

// ─── /login: valida cuenta/contraseña contra "usuarios" y, si es válida,
// devuelve la conexión real de SQL Server (solo para esa sesión del cliente).
// Misma lógica que tenía SistemaGestion.AppLoader.ValidarLogin, migrada acá.
app.MapPost("/login", async (LoginRequest req, HttpContext ctx) =>
{
    if (string.IsNullOrWhiteSpace(req.Cuenta) || string.IsNullOrWhiteSpace(req.Contrasena))
        return Results.BadRequest();

    string claveCuenta = req.Cuenta.Trim().ToLowerInvariant();
    string claveIp     = ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    // ── Anti fuerza bruta: si la cuenta o la IP están bloqueadas por demasiados
    // intentos fallidos, cortar acá — sin tocar la base ni revelar si la cuenta
    // existe. 429 + Retry-After con los segundos que faltan, y el mismo dato en
    // el cuerpo para que la app pueda avisar "esperá X".
    int espera = Math.Max(throttleCuenta.SegundosBloqueo(claveCuenta),
                          throttleIp.SegundosBloqueo(claveIp));
    if (espera > 0)
    {
        ctx.Response.Headers.RetryAfter = espera.ToString();
        return Results.Json(new LoginError { SegundosBloqueo = espera },
                            statusCode: StatusCodes.Status429TooManyRequests);
    }

    // Un login inválido cuenta como fallo para la cuenta Y para la IP. Si ese
    // fallo dispara el bloqueo, se responde 429 con el tiempo de espera; si no,
    // 401 con cuántos intentos quedan (el menor entre cuenta e IP) para que la
    // app pueda avisar al usuario.
    Microsoft.AspNetCore.Http.IResult Rechazar()
    {
        int restantesIp     = throttleIp.RegistrarFallo(claveIp);
        int restantesCuenta = throttleCuenta.RegistrarFallo(claveCuenta);

        int esperaAhora = Math.Max(throttleCuenta.SegundosBloqueo(claveCuenta),
                                   throttleIp.SegundosBloqueo(claveIp));
        if (esperaAhora > 0)
        {
            ctx.Response.Headers.RetryAfter = esperaAhora.ToString();
            return Results.Json(new LoginError { SegundosBloqueo = esperaAhora },
                                statusCode: StatusCodes.Status429TooManyRequests);
        }

        return Results.Json(new LoginError { IntentosRestantes = Math.Min(restantesCuenta, restantesIp) },
                            statusCode: StatusCodes.Status401Unauthorized);
    }

    try
    {
        using var conn = new SqlConnection(ConnectionString());
        await conn.OpenAsync();

        string id = "";
        string llaveDb = "";
        using (var cmd = new SqlCommand(
            "SELECT id, llave FROM usuarios WHERE cuenta = @cuenta AND estadof = 'normal'", conn))
        {
            cmd.Parameters.AddWithValue("@cuenta", req.Cuenta);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                id      = reader["id"].ToString() ?? "";
                llaveDb = reader["llave"] is DBNull ? "" : reader["llave"].ToString() ?? "";
            }
        }

        if (string.IsNullOrEmpty(id)) return Rechazar();

        bool valido = PasswordHasher.Verificar(req.Contrasena, llaveDb);

        // Migración: contraseña antigua en texto plano que coincide → re-hashear.
        if (!valido && !PasswordHasher.EsHash(llaveDb) && llaveDb == req.Contrasena)
        {
            using var cmdUpd = new SqlCommand(
                "UPDATE usuarios SET llave = @llave WHERE id = @id", conn);
            cmdUpd.Parameters.AddWithValue("@llave", PasswordHasher.Hashear(req.Contrasena));
            cmdUpd.Parameters.AddWithValue("@id", new Guid(id));
            await cmdUpd.ExecuteNonQueryAsync();
            valido = true;
        }

        if (!valido) return Rechazar();

        // Login correcto: limpiar el historial de fallos de la cuenta y la IP.
        throttleCuenta.RegistrarExito(claveCuenta);
        throttleIp.RegistrarExito(claveIp);

        var cfg = app.Configuration;
        return Results.Ok(new LoginResponse
        {
            UsuarioId  = id,
            Servidor   = cfg["Sql:Servidor"]   ?? "",
            BaseDatos  = cfg["Sql:BaseDatos"]  ?? "",
            Usuario    = cfg["Sql:Usuario"]    ?? "",
            Contrasena = cfg["Sql:Contrasena"] ?? "",
            // Token de sesión para los endpoints de archivos (/upload, /download).
            Token      = tokens.Emitir(id)
        });
    }
    catch (Exception ex)
    {
        // Un fallo de infraestructura (base caída) NO cuenta como intento fallido:
        // no debe gastar el presupuesto anti fuerza bruta de un usuario legítimo.
        app.Logger.LogError(ex, "Fallo /login: no se pudo conectar a SQL Server.");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

// ─── Autenticación de los endpoints de archivos ───────────────────────────
// Devuelve el id de usuario del token (Authorization: Bearer <token>) o null.
string? UsuarioDeToken(HttpContext ctx)
{
    string auth = ctx.Request.Headers.Authorization.ToString();
    if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
    return tokens.Validar(auth["Bearer ".Length..].Trim());
}

// ─── /upload: sube un archivo (multipart, campo "file") y lo guarda por hash ─
// Requiere token. Devuelve { hash, nombre, tamaño }. La app guarda ese hash en
// documentosF.archivo y el nombre original en documentosF.archivoNombre.
app.MapPost("/upload", async (HttpContext ctx) =>
{
    if (UsuarioDeToken(ctx) is null) return Results.Unauthorized();
    if (!ctx.Request.HasFormContentType) return Results.BadRequest();

    var form = await ctx.Request.ReadFormAsync();
    var archivo = form.Files.GetFile("file");
    if (archivo is null || archivo.Length == 0) return Results.BadRequest();
    if (archivo.Length > docMax)
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    // Se escribe a un temporal calculando el SHA-256 al vuelo y luego se mueve a
    // {hash}: si dos facturas adjuntan el mismo archivo, se guarda una sola vez.
    string temp = Path.Combine(docDir, Path.GetRandomFileName());
    string hash;
    try
    {
        Directory.CreateDirectory(docDir);
        using (var sha = SHA256.Create())
        using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
        using (var crypto = new CryptoStream(fs, sha, CryptoStreamMode.Write))
        {
            await archivo.CopyToAsync(crypto);
            crypto.FlushFinalBlock();
            hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        string destino = Path.Combine(docDir, hash);
        if (File.Exists(destino))
        {
            File.Delete(temp); // ya estaba (dedup): descartar el temporal
        }
        else
        {
            // Carrera: si otro subió el mismo archivo nuevo en el mismo instante,
            // el Move choca contra un destino ya creado → tratarlo como dedup.
            try { File.Move(temp, destino); }
            catch (IOException) when (File.Exists(destino)) { File.Delete(temp); }
        }
    }
    catch (Exception ex)
    {
        try { if (File.Exists(temp)) File.Delete(temp); } catch { /* nada más que hacer */ }
        app.Logger.LogError(ex, "Fallo /upload.");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    return Results.Ok(new UploadResponse
    {
        Hash   = hash,
        Nombre = Path.GetFileName(archivo.FileName),
        Tamano = archivo.Length
    });
});

// ─── /download/{hash}: descarga un archivo por su hash ─────────────────────
// Requiere token. El nombre original lo pone la app (lo tiene en documentosF).
app.MapGet("/download/{hash}", (string hash, HttpContext ctx) =>
{
    if (UsuarioDeToken(ctx) is null) return Results.Unauthorized();

    // El hash es hex de 64 chars: valida el formato y evita path traversal
    // (nada de '/', '..', etc. puede colarse en la ruta).
    hash = hash.ToLowerInvariant();
    if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) return Results.BadRequest();

    string ruta = Path.Combine(docDir, hash);
    if (!File.Exists(ruta)) return Results.NotFound();
    return Results.File(ruta, "application/octet-stream");
});

app.Run();

record LoginRequest(string Cuenta, string Contrasena);

class LoginResponse
{
    public string UsuarioId  { get; set; } = "";
    public string Servidor   { get; set; } = "";
    public string BaseDatos  { get; set; } = "";
    public string Usuario    { get; set; } = "";
    public string Contrasena { get; set; } = "";
    // Token de sesión para /upload y /download (la app vieja sin archivos lo ignora).
    public string Token      { get; set; } = "";
}

// Respuesta de /upload: el hash con el que se guardó el archivo (referencia que
// va a documentosF.archivo), su nombre original y su tamaño en bytes.
class UploadResponse
{
    public string Hash   { get; set; } = "";
    public string Nombre { get; set; } = "";
    public long   Tamano { get; set; }
}

// Cuerpo de las respuestas de fallo de /login, para que la app avise al usuario:
//   • 401 → IntentosRestantes: cuántos intentos quedan antes del bloqueo.
//   • 429 → SegundosBloqueo: segundos que faltan para poder reintentar.
class LoginError
{
    public int IntentosRestantes { get; set; }
    public int SegundosBloqueo   { get; set; }
}
