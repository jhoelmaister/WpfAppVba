using System;
using System.Linq;
using System.Windows;

namespace WpfAppVba
{
    /// <summary>
    /// Aplica y persiste el tema visual (claro / oscuro) del proyecto.
    /// El tema se almacena en la columna usuarios.temaC y se carga al iniciar sesión.
    /// </summary>
    public static class ThemeManager
    {
        public const string TemaClaro  = "claro";
        public const string TemaOscuro = "oscuro";

        private static readonly Uri UriLight = new("pack://application:,,,/Themes/LightTheme.xaml", UriKind.Absolute);
        private static readonly Uri UriDark  = new("pack://application:,,,/Themes/DarkTheme.xaml",  UriKind.Absolute);

        /// <summary>Normaliza el nombre del tema y aplica el ResourceDictionary correspondiente.</summary>
        public static void AplicarTema(string? tema)
        {
            string normalizado = (tema ?? "").Trim().ToLowerInvariant() == TemaOscuro
                ? TemaOscuro
                : TemaClaro;

            var app = Application.Current;
            if (app == null) return;

            Uri uriNuevo = normalizado == TemaOscuro ? UriDark : UriLight;
            var nuevoDic = new ResourceDictionary { Source = uriNuevo };

            // Quitar diccionarios de tema previos (LightTheme.xaml / DarkTheme.xaml)
            var existentes = app.Resources.MergedDictionaries
                .Where(d => d.Source != null &&
                            (d.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                             d.Source.OriginalString.EndsWith("DarkTheme.xaml",  StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (var d in existentes)
                app.Resources.MergedDictionaries.Remove(d);

            // Insertar el nuevo tema al inicio para que esté disponible para los siguientes
            app.Resources.MergedDictionaries.Insert(0, nuevoDic);
        }
    }
}
