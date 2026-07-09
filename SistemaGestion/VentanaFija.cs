using System;
using System.Windows;
using System.Windows.Interop;

namespace SistemaGestion
{
    public class VentanaFija : Window
    {
        private const int WM_MOVING = 0x0216;

        public VentanaFija()
        {
            ResizeMode = ResizeMode.NoResize;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = PresentationSource.FromVisual(this) as HwndSource;
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOVING)
                handled = true;
            return IntPtr.Zero;
        }
    }
}
