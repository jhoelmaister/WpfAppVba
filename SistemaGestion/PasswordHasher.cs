using System;
using System.Security.Cryptography;

namespace SistemaGestion.Data
{
    /// <summary>
    /// Hash de contraseñas con PBKDF2-SHA256. Formato almacenado:
    /// "{iteraciones}.{saltBase64}.{hashBase64}".
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iteraciones  = 100_000;
        private const int TamanoSalt   = 16;
        private const int TamanoHash   = 32;

        public static string Hashear(string contrasena)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(TamanoSalt);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, Iteraciones,
                HashAlgorithmName.SHA256, TamanoHash);
            return $"{Iteraciones}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifica una contraseña contra un valor almacenado con <see cref="Hashear"/>.
        /// Devuelve false si <paramref name="valorAlmacenado"/> no tiene el formato esperado
        /// (p. ej. contraseñas antiguas guardadas en texto plano).
        /// </summary>
        public static bool Verificar(string contrasena, string valorAlmacenado)
        {
            string[] partes = valorAlmacenado.Split('.');
            if (partes.Length != 3) return false;
            if (!int.TryParse(partes[0], out int iteraciones)) return false;

            byte[] salt, hashEsperado;
            try
            {
                salt         = Convert.FromBase64String(partes[1]);
                hashEsperado = Convert.FromBase64String(partes[2]);
            }
            catch (FormatException) { return false; }

            byte[] hashCalculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, iteraciones,
                HashAlgorithmName.SHA256, hashEsperado.Length);

            return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
        }

        /// <summary>True si el valor almacenado ya tiene el formato de hash (no texto plano).</summary>
        public static bool EsHash(string valorAlmacenado) => valorAlmacenado.Split('.').Length == 3;
    }
}
