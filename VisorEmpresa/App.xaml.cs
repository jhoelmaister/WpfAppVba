using System;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Velopack;

namespace VisorEmpresa
{
    /// <summary>
    /// Visor Empresa: aplicación de SOLO LECTURA que muestra los totales de la
    /// empresa completa (todas las sucursales, sin filtro por sucursal activa).
    /// Extensión de la app principal SistemaGestion; comparte conexión, temas y
    /// ventanas de configuración por archivos vinculados.
    /// </summary>
    public partial class App : Application
    {
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
                        SistemaGestion.WindowTheming.AplicarModoOscuro(w, TemaVisor.EsOscuroActivo);
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
            // El visor todavía no busca updates solo (sin botón 🔄): para subir de versión
            // se corre el Setup nuevo; el feed usa el canal "visor" de este mismo repo.
            VelopackApp.Build().Run();

            // Aplicar el último tema usado en esta PC ANTES de mostrar el login.
            TemaVisor.AplicarTema(TemaVisor.CargarTemaLocal());
            base.OnStartup(e);
        }
    }
}
