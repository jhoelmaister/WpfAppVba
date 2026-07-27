using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SistemaGestion.Data
{
    /// <summary>
    /// Un broker de autenticación registrado. "Servidor" es la URL base del
    /// broker (ver AuthBrokerClient) — NO son credenciales de SQL Server: esas
    /// ya no las guarda ni las conoce esta app fuera de una sesión logueada.
    /// </summary>
    public class ServidorConexion
    {
        public string Id       { get; set; } = Guid.NewGuid().ToString("N");
        public string Nombre   { get; set; } = "";
        public string Servidor { get; set; } = "";
    }

    /// <summary>
    /// Almacena de forma cifrada (DPAPI, CurrentUser) una lista de brokers de
    /// autenticación registrados en %AppData%\SistemaGestion\conexion.dat. Ya
    /// no guarda credenciales de SQL Server (la URL del broker no es secreta;
    /// el cifrado queda solo por prolijidad, sin necesidad real de proteger
    /// este archivo en particular).
    /// </summary>
    public static class ConexionConfig
    {
        private static readonly string RutaArchivo = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SistemaGestion", "conexion.dat");

        // ─── API de lista ─────────────────────────────────────────────────────

        public static bool HayConfiguracion()
        {
            if (!File.Exists(RutaArchivo)) return false;
            return LeerArchivo().lista.Count > 0;
        }

        public static List<ServidorConexion> CargarLista() => LeerArchivo().lista;

        public static string ObtenerActivoId() => LeerArchivo().activo;

        public static ServidorConexion? ObtenerPorId(string id) =>
            LeerArchivo().lista.FirstOrDefault(x => x.Id == id);

        /// <summary>URL del broker activo, o "" si no hay ninguno configurado.</summary>
        public static string ObtenerBrokerActivo()
        {
            var (lista, activo) = LeerArchivo();
            if (lista.Count == 0) return "";
            var s = lista.FirstOrDefault(x => x.Id == activo) ?? lista[0];
            return s.Servidor;
        }

        public static void Agregar(ServidorConexion s)
        {
            var (lista, activo) = LeerArchivo();
            if (string.IsNullOrEmpty(s.Id)) s.Id = Guid.NewGuid().ToString("N");
            lista.Add(s);
            if (string.IsNullOrEmpty(activo)) activo = s.Id;   // el primero queda activo
            EscribirArchivo(lista, activo);
        }

        public static void Actualizar(ServidorConexion s)
        {
            var (lista, activo) = LeerArchivo();
            int i = lista.FindIndex(x => x.Id == s.Id);
            if (i >= 0) lista[i] = s;
            else        lista.Add(s);
            EscribirArchivo(lista, activo);
        }

        public static void Eliminar(string id)
        {
            var (lista, activo) = LeerArchivo();
            lista.RemoveAll(x => x.Id == id);
            if (activo == id) activo = lista.Count > 0 ? lista[0].Id : "";
            EscribirArchivo(lista, activo);
        }

        public static void EstablecerActivo(string id)
        {
            var (lista, _) = LeerArchivo();
            if (lista.Any(x => x.Id == id))
                EscribirArchivo(lista, id);
        }

        // ─── Lectura / escritura cifrada ─────────────────────────────────────

        private static (List<ServidorConexion> lista, string activo) LeerArchivo()
        {
            var lista = new List<ServidorConexion>();
            string activo = "";
            if (!File.Exists(RutaArchivo)) return (lista, activo);

            try
            {
                byte[] plain = ProtectedData.Unprotect(
                    File.ReadAllBytes(RutaArchivo), null, DataProtectionScope.CurrentUser);
                using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(plain));
                var root = doc.RootElement;

                if (root.TryGetProperty("Servidores", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    if (root.TryGetProperty("Activo", out var act))
                        activo = act.GetString() ?? "";
                    foreach (var el in arr.EnumerateArray())
                        lista.Add(LeerServidor(el));
                }
            }
            catch { /* archivo corrupto, de otro usuario, o formato previo incompatible → lista vacía */ }

            if (string.IsNullOrEmpty(activo) && lista.Count > 0)
                activo = lista[0].Id;

            return (lista, activo);
        }

        private static ServidorConexion LeerServidor(JsonElement el)
        {
            string Get(string p) => el.TryGetProperty(p, out var v) ? v.GetString() ?? "" : "";
            var s = new ServidorConexion
            {
                Nombre   = Get("Nombre"),
                Servidor = Get("Servidor")
            };
            string id = Get("Id");
            if (!string.IsNullOrEmpty(id)) s.Id = id;
            return s;
        }

        private static void EscribirArchivo(List<ServidorConexion> lista, string activo)
        {
            var obj = new { Activo = activo, Servidores = lista };
            byte[] plain   = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
            byte[] cifrado = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            File.WriteAllBytes(RutaArchivo, cifrado);
        }
    }
}
