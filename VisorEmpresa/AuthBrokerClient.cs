using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace VisorEmpresa.Data
{
    /// <summary>
    /// Respuesta del broker tras un login válido: credenciales reales de SQL
    /// Server, entregadas SOLO para esta sesión — la app nunca las escribe a disco
    /// (ver DatabaseConnection.Configurar, que las guarda solo en memoria).
    /// </summary>
    public class LoginBrokerResponse
    {
        public string UsuarioId  { get; set; } = "";
        public string Servidor   { get; set; } = "";
        public string BaseDatos  { get; set; } = "";
        public string Usuario    { get; set; } = "";
        public string Contrasena { get; set; } = "";
    }

    /// <summary>
    /// Cliente del servicio de autenticación remoto ("broker", ver proyecto
    /// ConexionBroker/). La app ya no guarda ni conoce las credenciales reales
    /// de SQL Server: solo conoce la URL del broker (no es secreta) y, tras un
    /// login válido, recibe la conexión real solo en memoria para esa sesión.
    /// </summary>
    public static class AuthBrokerClient
    {
        // Única dirección del broker para toda la empresa — fija en el código
        // a propósito (no hay pantalla de "elegir servidor").
        public const string BrokerUrl = "https://conexion.jhoelmaister.tech";

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

        /// <summary>true si el broker (y la base de datos detrás) responde.</summary>
        public static async Task<bool> PingAsync(string brokerUrl)
        {
            if (string.IsNullOrWhiteSpace(brokerUrl)) return false;
            try
            {
                using var res = await _http.GetAsync(CombinarUrl(brokerUrl, "ping"));
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>null si las credenciales son inválidas o el broker no responde.</summary>
        public static async Task<LoginBrokerResponse?> LoginAsync(
            string brokerUrl, string cuenta, string contrasena)
        {
            if (string.IsNullOrWhiteSpace(brokerUrl)) return null;
            try
            {
                using var res = await _http.PostAsJsonAsync(
                    CombinarUrl(brokerUrl, "login"), new { cuenta, contrasena });
                if (!res.IsSuccessStatusCode) return null;
                return await res.Content.ReadFromJsonAsync<LoginBrokerResponse>();
            }
            catch { return null; }
        }

        private static string CombinarUrl(string brokerUrl, string ruta) =>
            brokerUrl.TrimEnd('/') + "/" + ruta;
    }
}
