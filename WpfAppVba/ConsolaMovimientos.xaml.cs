using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ConsolaMovimientos : Window
    {
        private Button? _btnActivo;

        public ConsolaMovimientos()
        {
            InitializeComponent();
            ActualizarInfoUsuario();
            MarcarActivo(BtnNav_Articulos);
        }

        // ─── Info usuario ─────────────────────────────────────────────────────
        public void ActualizarInfoUsuario()
        {
            LblUsuario.Text = $"Usuario: {AppState.UsuarioActivo}  |  Período: {AppState.PeriodoActivo}";
        }

        // ─── Mostrar panel de contenido ───────────────────────────────────────
        private void MostrarPanel(string nombre)
        {
            TabArticulos.Visibility    = Visibility.Collapsed;
            TabPedidos.Visibility      = Visibility.Collapsed;
            TabTraspasos.Visibility    = Visibility.Collapsed;
            TabCorrecciones.Visibility = Visibility.Collapsed;

            switch (nombre)
            {
                case "articulos":    TabArticulos.Visibility    = Visibility.Visible; break;
                case "pedidos":      TabPedidos.Visibility      = Visibility.Visible; break;
                case "traspasos":    TabTraspasos.Visibility    = Visibility.Visible; break;
                case "correcciones": TabCorrecciones.Visibility = Visibility.Visible; break;
            }
        }

        // ─── Resaltar ítem activo en la barra lateral ─────────────────────────
        private void MarcarActivo(Button btn)
        {
            if (_btnActivo != null)
            {
                _btnActivo.Background  = Brushes.Transparent;
                _btnActivo.BorderBrush = Brushes.Transparent;
                _btnActivo.Foreground  = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB8));
            }
            _btnActivo      = btn;
            btn.Background  = new SolidColorBrush(Color.FromRgb(0x25, 0x2A, 0x40));
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x6F, 0xE3));
            btn.Foreground  = Brushes.White;
        }

        // ─── Navegación lateral ───────────────────────────────────────────────

        private void BtnNav_Articulos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("articulos");
            MarcarActivo(BtnNav_Articulos);
        }

        private void BtnNav_Pedidos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("pedidos");
            MarcarActivo(BtnNav_Pedidos);
        }

        private void BtnNav_Traspasos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("traspasos");
            MarcarActivo(BtnNav_Traspasos);
        }

        private void BtnNav_Correcciones_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("correcciones");
            MarcarActivo(BtnNav_Correcciones);
        }

        private void BtnNav_Inventarios_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title                 = "Inventarios",
                Width                 = 520,
                Height                = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = this,
                ShowInTaskbar         = false,
                Background            = Brushes.Transparent,
                Content               = new InventariosGeneral()
            };
            WindowHelper.AjustarAlEcran(win);
            win.ShowDialog();
        }

        private void BtnNav_Precios_Click(object sender, RoutedEventArgs e)
        {
            new PreciosGeneral { Owner = this }.ShowDialog();
        }

        private void BtnNav_Configuracion_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title                 = "Configuración",
                Width                 = 480,
                Height                = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = this,
                ShowInTaskbar         = false,
                ResizeMode            = ResizeMode.NoResize,
                Background            = Brushes.Transparent,
                Content               = new Configuracion()
            };
            WindowHelper.AjustarAlEcran(win);
            win.ShowDialog();
            ActualizarInfoUsuario();
        }

        // ─── Overlay modal (stack) ────────────────────────────────────────────

        private readonly Stack<UIElement> _overlayStack = new();

        public void EmpujarOverlay(UIElement contenido)
        {
            _overlayStack.Push(contenido);
            OverlayContenido.Content = contenido;
            OverlayFondo.Visibility  = Visibility.Visible;
        }

        public void SacarOverlay()
        {
            if (_overlayStack.Count > 0) _overlayStack.Pop();
            if (_overlayStack.Count > 0)
            {
                OverlayContenido.Content = _overlayStack.Peek();
            }
            else
            {
                OverlayFondo.Visibility  = Visibility.Collapsed;
                OverlayContenido.Content = null;
            }
        }

        public void CerrarOverlay()
        {
            _overlayStack.Clear();
            OverlayFondo.Visibility  = Visibility.Collapsed;
            OverlayContenido.Content = null;
        }

        private void BtnCerrarOverlay_Click(object sender, RoutedEventArgs e)
        {
            switch (OverlayContenido.Content)
            {
                case PedidosDetalle   pd: pd.IntentarCerrar(); break;
                case TercerosDetalle  td: td.IntentarCerrar(); break;
                case TercerosGeneral  tg: tg.IntentarCerrar(); break;
                default: SacarOverlay(); break;
            }
        }

        // ─── Acciones rápidas ─────────────────────────────────────────────────

        private void BtnVentaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "venta";
            AppState.TipoPedido        = "rapido";
            var dlg = new PedidosDetalle();
            dlg.Cerrando += () => { SacarOverlay(); TabPedidos.CargarPedidos(); };
            EmpujarOverlay(dlg);
        }

        private void BtnEntradaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "entrada";
            new TraspasosDetalle { Owner = this }.ShowDialog();
            TabTraspasos.CargarTraspasos();
        }

        private void BtnSalidaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "salida";
            new TraspasosDetalle { Owner = this }.ShowDialog();
            TabTraspasos.CargarTraspasos();
        }

        // ─── Cerrar sesión ────────────────────────────────────────────────────

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            AppState.SesionActiva  = false;
            AppState.UsuarioActivo = 0;
            DatabaseConnection.CerrarConexion();

            var login = new LoginWindow();
            login.Show();
            Close();
        }
    }
}
