using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using VisorEmpresa.Data;

namespace VisorEmpresa
{
    /// <summary>
    /// Estado de conexión a SQL Server. SOLO mantiene estado (no muestra mensajes).
    /// Un <see cref="DispatcherTimer"/> sondea la conexión en segundo plano y actualiza
    /// <see cref="EnLinea"/>; el label de la top bar y el guard de guardado solo LEEN
    /// ese valor (la lectura del estado es instantánea: no congela la app esperando red).
    /// </summary>
    public static class ConexionEstado
    {
        private static DispatcherTimer? _timer;
        private static Dispatcher? _dispatcher;
        private static bool _enLinea = true;   // optimista hasta la primera sonda

        /// <summary>Último estado conocido de la conexión.</summary>
        public static bool EnLinea => _enLinea;

        /// <summary>Se dispara en el hilo de UI cuando cambia el estado.</summary>
        public static event Action<bool>? Cambio;

        /// <summary>Intervalo entre sondas de conexión.</summary>
        public static TimeSpan Intervalo { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Arranca el sondeo periódico. Idempotente. Lanza una sonda inicial inmediata.
        /// Debe llamarse desde el hilo de UI.
        /// </summary>
        public static void Iniciar(Dispatcher dispatcher)
        {
            if (_timer != null) return;

            _dispatcher = dispatcher;
            _timer = new DispatcherTimer { Interval = Intervalo };
            _timer.Tick += (_, _) => SondearEnSegundoPlano();
            _timer.Start();

            SondearEnSegundoPlano();   // primera lectura inmediata
        }

        public static void Detener()
        {
            _timer?.Stop();
            _timer = null;
        }

        /// <summary>
        /// Fuerza una revisión inmediata de la conexión (en segundo plano, no bloquea).
        /// Útil tras guardar para refrescar el estado/label sin esperar al timer.
        /// </summary>
        public static void Revisar() => SondearEnSegundoPlano();

        // Sonda rápida (2 s) en un hilo de fondo; al terminar vuelca el resultado al estado.
        private static void SondearEnSegundoPlano()
        {
            Task.Run(() => Establecer(DatabaseConnection.Sondear(2)));
        }

        private static void Establecer(bool enLinea)
        {
            if (_enLinea == enLinea) return;
            _enLinea = enLinea;

            if (_dispatcher != null && !_dispatcher.CheckAccess())
                _dispatcher.BeginInvoke(new Action(() => Cambio?.Invoke(enLinea)));
            else
                Cambio?.Invoke(enLinea);
        }
    }
}
