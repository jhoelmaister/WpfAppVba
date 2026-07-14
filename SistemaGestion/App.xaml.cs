using System.Configuration;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Velopack;

namespace SistemaGestion
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Evita abrir dos instancias de la app a la vez (mismo usuario/sesión de Windows).
        private Mutex? _instanciaUnica;

        public App()
        {
            // Aplicar modo oscuro a la barra de título de cada Window al cargarse
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
                        WindowTheming.AplicarModoOscuro(w, ThemeManager.EsOscuroActivo);
                    }
                }));

            var cultura = CultureInfo.CurrentCulture;

            // Propagar cultura (con ajustes manuales de Windows) a todos los hilos.
            CultureInfo.DefaultThreadCurrentCulture   = cultura;
            CultureInfo.DefaultThreadCurrentUICulture = cultura;

            // WPF usa XmlLanguage -> GetEquivalentCulture() para los StringFormat,
            // lo que crea una CultureInfo FRESCA desde el tag IETF e ignora los
            // ajustes manuales del usuario (p.ej. español + separadores en-US).
            // Solución: si el separador decimal real es "." (coma para miles,
            // punto para decimales) usamos el tag "en-US" para que WPF genere
            // esa cultura; si es "," usamos el tag nativo (formato europeo).
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
            VelopackApp.Build().Run();

            // Instancia única: si ya hay una ventana de esta app abierta en la misma
            // sesión de Windows, avisar y cerrar esta segunda instancia sin mostrar nada.
            _instanciaUnica = new Mutex(true, "SistemaGestion.InstanciaUnica", out bool esNueva);
            if (!esNueva)
            {
                // No es dueña del mutex (ya lo tiene la otra instancia): soltar el
                // handle sin liberarlo, para que OnExit no intente un ReleaseMutex
                // sobre un mutex que este proceso nunca llegó a poseer.
                _instanciaUnica.Dispose();
                _instanciaUnica = null;

                MessageBox.Show("Sistema de Gestión ya está abierto.", "Sistema de Gestión",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Aplicar el último tema usado en esta PC ANTES de mostrar la LoginWindow
            string temaLocal = ThemeManager.CargarTemaLocal();
            Data.AppState.TemaActivo = temaLocal;
            ThemeManager.AplicarTema(temaLocal);

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _instanciaUnica?.ReleaseMutex();
            base.OnExit(e);
        }
    }

}
