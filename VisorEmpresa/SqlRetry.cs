using System;
using System.Linq;
using System.Threading;
using Microsoft.Data.SqlClient;

namespace VisorEmpresa.Data
{
    /// <summary>
    /// Reintentos con backoff para operaciones SQL ante fallos TRANSITORIOS
    /// (timeouts, conexión rota por microcortes, deadlocks). Pensado para redes
    /// inestables: la mayoría de los cortes breves se reintentan solos y el usuario
    /// no ve el error.
    ///
    /// IMPORTANTE: corre de forma SÍNCRONA (Thread.Sleep) en el hilo que llama, así
    /// que un reintento añade una pausa breve (backoff) a esa operación.
    /// </summary>
    public static class SqlRetry
    {
        // Números de error de SQL Server considerados transitorios (timeout de
        // comando, conexión rota / red caída, y deadlock víctima).
        private static readonly int[] Transitorios =
            { -2, 53, 64, 121, 233, 1205, 10053, 10054, 10060, 10061, 11001, 40197, 40501, 40613 };

        public static T Ejecutar<T>(Func<T> accion, int intentos = 3, int baseMs = 400)
        {
            int delay = baseMs;
            for (int intento = 1; ; intento++)
            {
                try
                {
                    return accion();
                }
                catch (SqlException ex) when (intento < intentos && EsTransitorio(ex))
                {
                    Thread.Sleep(delay);
                    delay *= 2;   // backoff exponencial: 400ms, 800ms, ...
                }
            }
        }

        public static void Ejecutar(Action accion, int intentos = 3, int baseMs = 400)
            => Ejecutar<object?>(() => { accion(); return null; }, intentos, baseMs);

        private static bool EsTransitorio(SqlException ex)
            => ex.Errors.Cast<SqlError>().Any(e => Array.IndexOf(Transitorios, e.Number) >= 0);
    }
}
