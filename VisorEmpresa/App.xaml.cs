using System;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace VisorEmpresa
{
    /// <summary>
    /// Visor Empresa: aplicación de SOLO LECTURA que muestra los totales de la
    /// empresa completa (todas las sucursales, sin filtro por sucursal activa).
    /// Extensión de la app principal WpfAppVba; comparte conexión, temas y
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
                        WpfAppVba.WindowTheming.AplicarModoOscuro(w, TemaVisor.EsOscuroActivo);
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
            // Aplicar el último tema usado en esta PC ANTES de mostrar el login.
            TemaVisor.AplicarTema(TemaVisor.CargarTemaLocal());
            base.OnStartup(e);
        }
    }
}
