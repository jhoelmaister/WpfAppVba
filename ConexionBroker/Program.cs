using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

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
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

// ─── /login: valida cuenta/contraseña contra "usuarios" y, si es válida,
// devuelve la conexión real de SQL Server (solo para esa sesión del cliente).
// Misma lógica que tenía SistemaGestion.AppLoader.ValidarLogin, migrada acá.
app.MapPost("/login", async (LoginRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Cuenta) || string.IsNullOrWhiteSpace(req.Contrasena))
        return Results.BadRequest();

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

        if (string.IsNullOrEmpty(id)) return Results.Unauthorized();

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

        if (!valido) return Results.Unauthorized();

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
    catch
    {
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
