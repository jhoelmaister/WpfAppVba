using System;
using System.Collections.Concurrent;

namespace ConexionBroker
{
    /// <summary>
    /// Anti fuerza bruta para /login. Cuenta los intentos FALLIDOS por clave
    /// (una por cuenta y otra por IP) dentro de una ventana deslizante y, al
    /// superar el umbral, bloquea temporalmente esa clave: mientras dure el
    /// bloqueo el endpoint responde 429 sin siquiera tocar la base de datos.
    ///
    /// Se usan dos claves a la vez para cubrir los dos ataques típicos:
    ///   • por cuenta  → frena la fuerza bruta contra UNA cuenta desde muchas IPs.
    ///   • por IP      → frena el "password spraying" (una IP probando muchas
    ///     cuentas). El umbral por IP es más alto que el de cuenta para tolerar
    ///     una oficina entera detrás de un mismo NAT.
    ///
    /// Todo vive en memoria: si el broker se reinicia, los contadores se pierden
    /// (aceptable — un ataque tendría que recomenzar). Las entradas vencidas se
    /// purgan de a poco para que el diccionario no crezca sin límite aunque el
    /// atacante mande cuentas/IPs distintas.
    /// </summary>
    public sealed class LoginThrottle
    {
        private sealed class Contador
        {
            public int       Fallos;
            public DateTime   PrimerFallo;
            public DateTime?  BloqueadoHasta;
        }

        private readonly ConcurrentDictionary<string, Contador> _porClave =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly int      _maxFallos;
        private readonly TimeSpan _ventana;
        private readonly TimeSpan _bloqueo;

        private long _ultimaLimpiezaTicks = DateTime.UtcNow.Ticks;
        private static readonly TimeSpan IntervaloLimpieza = TimeSpan.FromMinutes(5);

        public LoginThrottle(int maxFallos, TimeSpan ventana, TimeSpan bloqueo)
        {
            _maxFallos = Math.Max(1, maxFallos);
            _ventana   = ventana;
            _bloqueo   = bloqueo;
        }

        /// <summary>
        /// Segundos que faltan para que <paramref name="clave"/> pueda reintentar,
        /// o 0 si no está bloqueada.
        /// </summary>
        public int SegundosBloqueo(string clave)
        {
            if (!_porClave.TryGetValue(clave, out var c)) return 0;
            lock (c)
            {
                if (c.BloqueadoHasta is { } hasta)
                {
                    var resto = hasta - DateTime.UtcNow;
                    if (resto > TimeSpan.Zero) return (int)Math.Ceiling(resto.TotalSeconds);
                }
            }
            return 0;
        }

        /// <summary>
        /// Registra un intento fallido para la clave y, si corresponde, la bloquea.
        /// Devuelve cuántos intentos quedan antes del bloqueo (0 = recién bloqueada).
        /// </summary>
        public int RegistrarFallo(string clave)
        {
            var ahora = DateTime.UtcNow;
            var c = _porClave.GetOrAdd(clave, _ => new Contador { PrimerFallo = ahora });
            int restantes;
            lock (c)
            {
                // Reiniciar el conteo si el bloqueo previo ya venció, o si la ventana
                // deslizante expiró sin haber llegado al umbral.
                bool bloqueoVencido = c.BloqueadoHasta is { } h && h <= ahora;
                bool ventanaVencida = c.BloqueadoHasta is null && ahora - c.PrimerFallo > _ventana;
                if (bloqueoVencido || ventanaVencida)
                {
                    c.Fallos         = 0;
                    c.PrimerFallo    = ahora;
                    c.BloqueadoHasta = null;
                }

                c.Fallos++;
                if (c.Fallos >= _maxFallos)
                    c.BloqueadoHasta = ahora + _bloqueo;

                restantes = Math.Max(0, _maxFallos - c.Fallos);
            }
            LimpiarSiCorresponde(ahora);
            return restantes;
        }

        /// <summary>Login correcto: se olvida cualquier historial de fallos de la clave.</summary>
        public void RegistrarExito(string clave) => _porClave.TryRemove(clave, out _);

        // Purga entradas cuya ventana y cuyo bloqueo ya vencieron. Se ejecuta a lo
        // sumo una vez cada IntervaloLimpieza; el resto de las llamadas salen sin costo.
        private void LimpiarSiCorresponde(DateTime ahora)
        {
            long previo = System.Threading.Interlocked.Read(ref _ultimaLimpiezaTicks);
            if (ahora - new DateTime(previo, DateTimeKind.Utc) < IntervaloLimpieza) return;
            // Solo un hilo gana el derecho a limpiar; los demás siguen de largo.
            if (System.Threading.Interlocked.CompareExchange(ref _ultimaLimpiezaTicks, ahora.Ticks, previo) != previo)
                return;

            foreach (var kv in _porClave)
            {
                var c = kv.Value;
                lock (c)
                {
                    bool bloqueoInactivo = c.BloqueadoHasta is null || c.BloqueadoHasta <= ahora;
                    bool ventanaVencida  = ahora - c.PrimerFallo > _ventana;
                    if (bloqueoInactivo && ventanaVencida)
                        _porClave.TryRemove(kv.Key, out _);
                }
            }
        }
    }
}
