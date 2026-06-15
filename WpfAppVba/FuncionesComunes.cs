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
        /// Verifica la conexión en dos capas y, si no hay, muestra <paramref name="mensaje"/>:
        ///   1) Estado del label (ConexionEstado.EnLinea): lectura instantánea, no congela.
        ///   2) Si el label dice "en línea": verificación REAL (TieneConexion / SELECT 1).
        /// Devuelve false si no hay conexión.
        /// </summary>
        public static bool HayConexionOAvisa(Window? owner, string mensaje)
        {
            // Capa 1: estado del label (instantáneo). Capa 2: verificación real
            // (solo si la capa 1 pasó, por el cortocircuito &&).
            bool hay = ConexionEstado.EnLinea && TieneConexion();
            if (!hay)
                MessageBox.Show(owner, mensaje, "Conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            return hay;
        }

        /// <summary>Guard de conexión para acciones de guardado/borrado.</summary>
        public static bool VerificarConexionParaGuardar(Window? owner = null)
            => HayConexionOAvisa(owner, "Sin conexión. No se pueden guardar los cambios.");

        /// <summary>Guard de conexión para acciones de actualizar/refrescar datos.</summary>
        public static bool VerificarConexionParaActualizar(Window? owner = null)
            => HayConexionOAvisa(owner, "Sin conexión. No se pueden actualizar los datos.");

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
