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
    // existe. 429 + Retry-After con los segundos que faltan.
    int espera = Math.Max(throttleCuenta.SegundosBloqueo(claveCuenta),
                          throttleIp.SegundosBloqueo(claveIp));
    if (espera > 0)
    {
        ctx.Response.Headers.RetryAfter = espera.ToString();
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    // Un login inválido cuenta como fallo para la cuenta Y para la IP.
    Microsoft.AspNetCore.Http.IResult Rechazar()
    {
        throttleCuenta.RegistrarFallo(claveCuenta);
        throttleIp.RegistrarFallo(claveIp);
        return Results.Unauthorized();
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
            Contrasena = cfg["Sql:Contrasena"] ?? ""
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

app.Run();

record LoginRequest(string Cuenta, string Contrasena);

class LoginResponse
{
    public string UsuarioId  { get; set; } = "";
    public string Servidor   { get; set; } = "";
    public string BaseDatos  { get; set; } = "";
    public string Usuario    { get; set; } = "";
    public string Contrasena { get; set; } = "";
}
