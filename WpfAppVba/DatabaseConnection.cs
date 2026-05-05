using System;
using Microsoft.Data.SqlClient;

namespace TuProyecto.Data
{
    /// <summary>
    /// Equivalente a Conectar_Datos.bas
    /// Maneja la conexión global a SQL Server con reconexión automática.
    /// </summary>
    public static class DatabaseConnection
    {
        private static SqlConnection? _connection;

        private static string _server   = "maister";
        private static string _user     = "SA";
        private static string _password = "papa1122";
        private static string _database = "edberBase";

        private static string ConnectionString =>
            $"Server={_server};Database={_database};User Id={_user};Password={_password};" +
            $"Application Name=edber;Connect Timeout=10;Command Timeout=10;TrustServerCertificate=True;";

        // ─── Configurar credenciales desde fuera (appsettings, etc.) ──────────
        public static void Configurar(string server, string database, string user, string password)
        {
            _server   = server;
            _database = database;
            _user     = user;
            _password = password;
        }

        // ─── Obtener (o crear) la conexión ───────────────────────────────────
        public static SqlConnection ObtenerConexion()
        {
            if (_connection == null)
            {
                _connection = new SqlConnection(ConnectionString);
                _connection.Open();
                return _connection;
            }

            if (_connection.State == System.Data.ConnectionState.Closed ||
                _connection.State == System.Data.ConnectionState.Broken)
            {
                try { _connection.Open(); }
                catch
                {
                    // Si falla la reconexión, crear una nueva
                    _connection.Dispose();
                    _connection = new SqlConnection(ConnectionString);
                    _connection.Open();
                }
            }

            return _connection;
        }

        // ─── Verificar si la conexión está activa (SELECT 1) ─────────────────
        public static bool ConexionEstaActiva()
        {
            try
            {
                var conn = ObtenerConexion();
                using var cmd = new SqlCommand("SELECT 1", conn);
                cmd.ExecuteScalar();
                return true;
            }
            catch { return false; }
        }

        // ─── Cerrar la conexión global ────────────────────────────────────────
        public static void CerrarConexion()
        {
            if (_connection != null)
            {
                if (_connection.State == System.Data.ConnectionState.Open)
                    _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }
    }
}
