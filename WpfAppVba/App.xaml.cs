using System.Configuration;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace WpfAppVba
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Aplicar modo oscuro a la barra de título de cada Window al cargarse
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, _) =>
                {
                    if (s is Window w)
                        WindowTheming.AplicarModoOscuro(w, ThemeManager.EsOscuroActivo);
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
            // Aplicar el último tema usado en esta PC ANTES de mostrar la LoginWindow
            string temaLocal = ThemeManager.CargarTemaLocal();
            Data.AppState.TemaActivo = temaLocal;
            ThemeManager.AplicarTema(temaLocal);

            base.OnStartup(e);
        }
    }

}
