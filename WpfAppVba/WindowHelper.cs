using System.Windows;

namespace WpfAppVba
{
    /// <summary>
    /// Utilidades para adaptar el tamaño de las ventanas al área de trabajo disponible.
    /// </summary>
    public static class WindowHelper
    {
        // Porcentaje máximo del área de trabajo que puede ocupar una ventana
        private const double MaxFactor = 0.92;

        /// <summary>
        /// Reduce el ancho y/o alto de la ventana si supera el 92 % del área de
        /// trabajo del monitor principal.  Debe llamarse justo después de
        /// <c>InitializeComponent()</c>, antes de <c>ShowDialog()</c> / <c>Show()</c>.
        /// </summary>
        public static void AjustarAlEcran(Window w)
        {
            var area = SystemParameters.WorkArea;
            if (area.Width <= 0 || area.Height <= 0) return;

            double maxW = area.Width  * MaxFactor;
            double maxH = area.Height * MaxFactor;

            if (w.Width  > maxW) w.Width  = maxW;
            if (w.Height > maxH) w.Height = maxH;
        }
    }
}
