using System;
using System.Security.Cryptography;

namespace ConexionBroker
{
    /// <summary>
    /// Copia exacta de SistemaGestion/PasswordHasher.cs: el broker necesita
    /// verificar el mismo hash PBKDF2-SHA256 que guarda "usuarios.llave" sin
    /// depender de la app WPF. Formato almacenado:
    /// "{iteraciones}.{saltBase64}.{hashBase64}".
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iteraciones = 100_000;
        private const int TamanoSalt  = 16;
        private const int TamanoHash  = 32;

        public static string Hashear(string contrasena)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(TamanoSalt);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, salt, Iteraciones,
                HashAlgorithmName.SHA256, TamanoHash);
            return $"{Iteraciones}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

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

        public static bool EsHash(string valorAlmacenado) => valorAlmacenado.Split('.').Length == 3;
    }
}
