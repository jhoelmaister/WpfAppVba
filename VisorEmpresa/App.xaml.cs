using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Velopack;

namespace VisorEmpresa
{
    /// <summary>
    /// Visor Empresa: aplicación de SOLO LECTURA que muestra los totales de la
    /// empresa completa (todas las sucursales, sin filtro por sucursal activa).
    /// Código 100% independiente de SistemaGestion (sin archivos vinculados,
    /// ver el comentario en VisorEmpresa.csproj); solo comparte con esa app la
    /// convención de dónde vive en disco el archivo de conexión cifrado.
    /// </summary>
    public partial class App : Application
    {
        // Evita abrir dos instancias de la app a la vez (mismo usuario/sesión de Windows).
        private Mutex? _instanciaUnica;

        public App()
        {
            // Icono + modo oscuro de la barra de título en cada ventana al cargarse
            // (mismo patrón que la app principal).
            var appIcon = BitmapFrame.Create(
                new Uri("pack://application:,,,/icono.ico", UriKind.Absolute));

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    if (s is Window w)
                    {
                        w.Icon = appIcon;
                        VisorEmpresa.WindowTheming.AplicarModoOscuro(w, TemaVisor.EsOscuroActivo);
                    }
                }));

            var cultura = CultureInfo.CurrentCulture;

            // Propagar cultura (con ajustes manuales de Windows) a todos los hilos.
            CultureInfo.DefaultThreadCurrentCulture   = cultura;
            CultureInfo.DefaultThreadCurrentUICulture = cultura;

            // WPF usa XmlLanguage -> GetEquivalentCulture() para los StringFormat,
            // lo que crea una CultureInfo FRESCA desde el tag IETF e ignora los
            // ajustes manuales del usuario (mismo workaround que la app principal).
            string tag = cultura.NumberFormat.NumberDecimalSeparator == "."
                ? "en-US"
                : cultura.IetfLanguageTag;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(tag)));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Velopack: DEBE ejecutarse antes que cualquier otra cosa de la app.
            // Maneja los "hooks" del ciclo de vida (primera instalación, actualización,
            // desinstalación) que el instalador invoca con argumentos especiales y que
            // terminan el proceso sin mostrar UI. En ejecución normal no hace nada visible.
            // El feed de actualizaciones usa el canal "visor" de este mismo repo (ver
            // ActualizadorApp.cs) — Velopack lo detecta solo porque el Setup instalado
            // quedó marcado con ese canal al empaquetarse (--channel visor).
            VelopackApp.Build().Run();

            // Instancia única: si ya hay una ventana de esta app abierta en la misma
            // sesión de Windows, avisar y cerrar esta segunda instancia sin mostrar nada.
            _instanciaUnica = new Mutex(true, "VisorEmpresa.InstanciaUnica", out bool esNueva);
            if (!esNueva)
            {
                // No es dueña del mutex (ya lo tiene la otra instancia): soltar el
                // handle sin liberarlo, para que OnExit no intente un ReleaseMutex
                // sobre un mutex que este proceso nunca llegó a poseer.
                _instanciaUnica.Dispose();
                _instanciaUnica = null;

                MessageBox.Show("Visor Empresa ya está abierto.", "Visor Empresa",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Aplicar el último tema usado en esta PC ANTES de mostrar el login.
            TemaVisor.AplicarTema(TemaVisor.CargarTemaLocal());
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _instanciaUnica?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
