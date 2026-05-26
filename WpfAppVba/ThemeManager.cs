using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace WpfAppVba
{
    /// <summary>
    /// Aplica y persiste el tema visual (claro / oscuro) del proyecto.
    /// - Persistencia en SQL Server: usuarios.temaC (preferencia del usuario)
    /// - Persistencia local: %LOCALAPPDATA%\WpfAppVba\theme.txt (último tema usado en esta PC,
    ///   para aplicar el tema correcto a la LoginWindow antes de saber qué usuario es)
    /// </summary>
    public static class ThemeManager
    {
        public const string TemaClaro  = "claro";
        public const string TemaOscuro = "oscuro";

        private static readonly Uri UriLight = new("pack://application:,,,/Themes/LightTheme.xaml", UriKind.Absolute);
        private static readonly Uri UriDark  = new("pack://application:,,,/Themes/DarkTheme.xaml",  UriKind.Absolute);

        private static string RutaArchivoLocal =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WpfAppVba",
                "theme.txt");

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

            // Actualizar barra de título de todas las ventanas abiertas
            WindowTheming.AplicarModoOscuroATodas(normalizado == TemaOscuro);

            // Guardar como último tema usado (para arrancar la próxima sesión con el mismo)
            GuardarTemaLocal(normalizado);
        }

        /// <summary>Indica si el tema activo es oscuro (lee AppState.TemaActivo).</summary>
        public static bool EsOscuroActivo =>
            (Data.AppState.TemaActivo ?? "").Trim().ToLowerInvariant() == TemaOscuro;

        /// <summary>Lee el último tema aplicado en esta PC. Devuelve "claro" si no existe.</summary>
        public static string CargarTemaLocal()
        {
            try
            {
                if (!File.Exists(RutaArchivoLocal)) return TemaClaro;
                string contenido = File.ReadAllText(RutaArchivoLocal).Trim().ToLowerInvariant();
                return contenido == TemaOscuro ? TemaOscuro : TemaClaro;
            }
            catch
            {
                return TemaClaro;
            }
        }

        /// <summary>Persiste el último tema en %LOCALAPPDATA%\WpfAppVba\theme.txt</summary>
        public static void GuardarTemaLocal(string tema)
        {
            try
            {
                string ruta = RutaArchivoLocal;
                string? dir = Path.GetDirectoryName(ruta);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(ruta, tema);
            }
            catch
            {
                // Permisos / disco lleno: ignorar — la próxima vez intentará de nuevo
            }
        }
    }
}
