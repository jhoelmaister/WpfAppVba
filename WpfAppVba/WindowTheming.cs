using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WpfAppVba
{
    /// <summary>
    /// Aplica el modo oscuro/claro a la barra de título (área no-cliente)
    /// de las ventanas WPF mediante la API DWM de Windows.
    /// </summary>
    internal static class WindowTheming
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // Win10 < 20H1
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE     = 20; // Win10 20H1+, Win11

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void AplicarModoOscuro(Window window, bool oscuro)
        {
            if (window == null) return;
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int valor = oscuro ? 1 : 0;
                int res = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref valor, sizeof(int));
                if (res != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref valor, sizeof(int));
            }
            catch
            {
                // Versiones de Windows que no soportan DWM dark mode: ignorar
            }
        }

        public static void AplicarModoOscuroATodas(bool oscuro)
        {
            if (Application.Current == null) return;
            foreach (Window w in Application.Current.Windows)
                AplicarModoOscuro(w, oscuro);
        }
    }
}
