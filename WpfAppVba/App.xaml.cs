using System.Configuration;
using System.Data;
using System.Globalization;
using System.Threading;
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
            // Forzar formato numérico inglés (coma para miles, punto para decimales)
            // para todos los StringFormat de bindings WPF en toda la aplicación.
            var cultura = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture   = cultura;
            CultureInfo.DefaultThreadCurrentUICulture = cultura;
            Thread.CurrentThread.CurrentCulture       = cultura;
            Thread.CurrentThread.CurrentUICulture     = cultura;

            // Que los bindings con StringFormat respeten la cultura del hilo.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));
        }
    }

}
