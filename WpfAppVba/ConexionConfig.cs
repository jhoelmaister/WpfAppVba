using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WpfAppVba.Data
{
    public static class ConexionConfig
    {
        private static readonly string RutaArchivo = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfAppVba", "conexion.dat");

        public static bool HayConfiguracion() => File.Exists(RutaArchivo);

        public static void Guardar(string servidor, string baseDatos, string usuario, string contrasena)
        {
            var json = JsonSerializer.Serialize(new
            {
                Servidor   = servidor,
                BaseDatos  = baseDatos,
                Usuario    = usuario,
                Contrasena = contrasena
            });
            byte[] plain   = Encoding.UTF8.GetBytes(json);
            byte[] cifrado = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            File.WriteAllBytes(RutaArchivo, cifrado);
        }

        public static (string servidor, string baseDatos, string usuario, string contrasena)? Cargar()
        {
            if (!File.Exists(RutaArchivo)) return null;
            try
            {
                byte[] plain = ProtectedData.Unprotect(
                    File.ReadAllBytes(RutaArchivo), null, DataProtectionScope.CurrentUser);
                using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(plain));
                var r = doc.RootElement;
                return (
                    r.GetProperty("Servidor").GetString()   ?? "",
                    r.GetProperty("BaseDatos").GetString()  ?? "",
                    r.GetProperty("Usuario").GetString()    ?? "",
                    r.GetProperty("Contrasena").GetString() ?? "");
            }
            catch { return null; }
        }
    }
}
