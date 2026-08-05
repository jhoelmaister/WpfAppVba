using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ConexionBroker
{
    /// <summary>
    /// Tokens de sesión en memoria. Al loguear con éxito se emite uno; los
    /// endpoints de archivos (/upload, /download) lo exigen en el header
    /// Authorization: Bearer. El token expira por inactividad (vida deslizante);
    /// si el broker se reinicia, todos los tokens se pierden y hay que volver a
    /// loguear (aceptable — la app ya vuelve a loguear al reabrir).
    /// </summary>
    public sealed class TokenStore
    {
        private sealed class Entrada
        {
            public string   UsuarioId = "";
            public DateTime  Expira;
        }

        private readonly ConcurrentDictionary<string, Entrada> _tokens = new();
        private readonly TimeSpan _vida;

        public TokenStore(TimeSpan vida) => _vida = vida;

        /// <summary>Emite un token nuevo para el usuario y purga los vencidos.</summary>
        public string Emitir(string usuarioId)
        {
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _tokens[token] = new Entrada { UsuarioId = usuarioId, Expira = DateTime.UtcNow + _vida };
            Purgar();
            return token;
        }

        /// <summary>
        /// Devuelve el id de usuario si el token es válido y no venció (renovando
        /// su vida), o null si no existe o expiró.
        /// </summary>
        public string? Validar(string? token)
        {
            if (string.IsNullOrEmpty(token) || !_tokens.TryGetValue(token, out var e))
                return null;
            if (e.Expira <= DateTime.UtcNow)
            {
                _tokens.TryRemove(token, out _);
                return null;
            }
            e.Expira = DateTime.UtcNow + _vida; // vida deslizante: cada uso lo renueva
            return e.UsuarioId;
        }

        private void Purgar()
        {
            var ahora = DateTime.UtcNow;
            foreach (var kv in _tokens)
                if (kv.Value.Expira <= ahora)
                    _tokens.TryRemove(kv.Key, out _);
        }
    }
}
