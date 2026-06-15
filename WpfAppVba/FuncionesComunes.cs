using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    /// <summary>
    /// Equivalente a Funciones_Comunes.bas + test_coneccion.bas
    /// Funciones utilitarias reutilizables en todos los formularios.
    /// </summary>
    public static class FuncionesComunes
    {
        // ─── Equivalente a formActivo(formNombre) ─────────────────────────────
        /// <summary>Devuelve true si hay una ventana del tipo T abierta.</summary>
        public static bool FormActivo<T>() where T : Window
            => Application.Current.Windows.OfType<T>().Any();

        // ─── Equivalente a TieneInternetHTTP() ───────────────────────────────
        /// <summary>
        /// En VBA verificaba internet; en WPF verifica la conexión a la BD.
        /// Reemplaza todos los "If TieneInternetHTTP Then" del código original.
        /// </summary>
        public static bool TieneConexion()
            => DatabaseConnection.ConexionEstaActiva();

        // ─── Guard de conexión antes de guardar ───────────────────────────────
        /// <summary>
        /// Verifica la conexión ANTES de guardar, en dos capas:
        ///   1) Estado del label (ConexionEstado.EnLinea): lectura instantánea, no congela.
        ///      Si está offline, corta de inmediato.
        ///   2) Si el label dice "en línea": verificación REAL (TieneConexion / SELECT 1)
        ///      para mayor seguridad antes de escribir.
        /// Si cualquiera falla, muestra una advertencia simple y devuelve false.
        /// </summary>
        public static bool VerificarConexionParaGuardar(Window? owner = null)
        {
            // Capa 1: estado del label (instantáneo).
            // Capa 2: verificación real (solo si la capa 1 pasó, por el cortocircuito &&).
            bool hay = ConexionEstado.EnLinea && TieneConexion();
            if (!hay)
            {
                MessageBox.Show(owner,
                    "Sin conexión. No se pueden guardar los cambios.",
                    "Conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return hay;
        }

        // ─── Equivalente a ValidarSoloNumeros ────────────────────────────────
        /// <summary>
        /// Usar en PreviewTextInput de un TextBox para aceptar solo dígitos.
        /// permitirDecimales = true → acepta un único punto decimal.
        /// </summary>
        public static void ValidarSoloNumeros(
            object sender,
            TextCompositionEventArgs e,
            bool permitirDecimales = false)
        {
            if (permitirDecimales && e.Text == ".")
            {
                // Permitir punto solo si no hay otro ya
                e.Handled = (sender is TextBox tb && tb.Text.Contains('.'));
            }
            else
            {
                e.Handled = !e.Text.All(char.IsDigit);
            }
        }

        // ─── Equivalente a ValidarSoloLetras ─────────────────────────────────
        /// <summary>
        /// Usar en PreviewTextInput de un TextBox para aceptar solo letras.
        /// permitirEspacios = true → también acepta el espacio.
        /// </summary>
        public static void ValidarSoloLetras(
            object sender,
            TextCompositionEventArgs e,
            bool permitirEspacios = true)
        {
            e.Handled = !e.Text.All(c => char.IsLetter(c) || (permitirEspacios && c == ' '));
        }

        // ─── Equivalente a UnirVariables(...) ────────────────────────────────
        /// <summary>
        /// Une valores no vacíos separados por espacio.
        /// Equivalente a UnirVariables(a, b, c) en VBA.
        /// </summary>
        public static string UnirVariables(params string?[] variables)
            => string.Join(" ", variables
                .Select(v => v?.Trim() ?? "")
                .Where(v => v != ""));
    }
}
