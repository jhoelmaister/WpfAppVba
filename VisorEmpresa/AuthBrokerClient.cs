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
    /// Resultado de un intento de login contra el broker. Exactamente uno de los
    /// estados aplica: éxito (<see cref="Credenciales"/> no nulo), bloqueado por
    /// fuerza bruta, credenciales inválidas (con o sin cuántos intentos quedan), o
    /// sin conexión con el broker.
    /// </summary>
    public class LoginBrokerResultado
    {
        public LoginBrokerResponse? Credenciales     { get; init; }
        public bool                 Bloqueado        { get; init; }
        public int                  SegundosBloqueo  { get; init; }
        public int?                 IntentosRestantes{ get; init; }
        public bool                 SinConexion      { get; init; }

        public bool EsExito => Credenciales != null;
    }

    // Cuerpo de las respuestas de fallo que manda el broker (401/429).
    internal class LoginErrorBody
    {
        public int IntentosRestantes { get; set; }
        public int SegundosBloqueo   { get; set; }
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

        /// <summary>
        /// Intenta loguear. Nunca lanza: devuelve un <see cref="LoginBrokerResultado"/>
        /// que distingue éxito, bloqueo por fuerza bruta (429), credenciales inválidas
        /// (401, con los intentos restantes si el broker los informa) y sin conexión.
        /// </summary>
        public static async Task<LoginBrokerResultado> LoginAsync(
            string brokerUrl, string cuenta, string contrasena)
        {
            if (string.IsNullOrWhiteSpace(brokerUrl))
                return new LoginBrokerResultado { SinConexion = true };
            try
            {
                using var res = await _http.PostAsJsonAsync(
                    CombinarUrl(brokerUrl, "login"), new { cuenta, contrasena });

                if (res.IsSuccessStatusCode)
                {
                    var cred = await res.Content.ReadFromJsonAsync<LoginBrokerResponse>();
                    return cred != null
                        ? new LoginBrokerResultado { Credenciales = cred }
                        : new LoginBrokerResultado { SinConexion = true };
                }

                if ((int)res.StatusCode == 429)
                    return new LoginBrokerResultado { Bloqueado = true, SegundosBloqueo = await LeerAsync(res, b => b.SegundosBloqueo) ?? 0 };

                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return new LoginBrokerResultado { IntentosRestantes = await LeerAsync(res, b => b.IntentosRestantes) };

                // Otro código (400, etc.): tratar como credenciales inválidas sin detalle.
                return new LoginBrokerResultado { IntentosRestantes = null };
            }
            catch { return new LoginBrokerResultado { SinConexion = true }; }
        }

        // Lee un campo del cuerpo de error del broker. Tolera cuerpo vacío o no-JSON
        // (p. ej. un broker viejo aún sin desplegar): devuelve null en ese caso.
        private static async Task<int?> LeerAsync(HttpResponseMessage res, Func<LoginErrorBody, int> campo)
        {
            try
            {
                var body = await res.Content.ReadFromJsonAsync<LoginErrorBody>();
                return body == null ? null : campo(body);
            }
            catch { return null; }
        }

        private static string CombinarUrl(string brokerUrl, string ruta) =>
            brokerUrl.TrimEnd('/') + "/" + ruta;
    }
}
