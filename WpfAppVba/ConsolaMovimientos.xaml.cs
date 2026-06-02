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
            TabArticulos.Visibility            = Visibility.Collapsed;
            TabVentas.Visibility               = Visibility.Collapsed;
            TabCompras.Visibility              = Visibility.Collapsed;
            TabEntradas.Visibility             = Visibility.Collapsed;
            TabSalidas.Visibility              = Visibility.Collapsed;
            TabCorreccionesIngresos.Visibility = Visibility.Collapsed;
            TabCorreccionesEgresos.Visibility  = Visibility.Collapsed;

            switch (nombre)
            {
                case "articulos": TabArticulos.Visibility            = Visibility.Visible; break;
                case "ventas":    TabVentas.Visibility               = Visibility.Visible; break;
                case "compras":   TabCompras.Visibility              = Visibility.Visible; break;
                case "entradas":  TabEntradas.Visibility             = Visibility.Visible; break;
                case "salidas":   TabSalidas.Visibility              = Visibility.Visible; break;
                case "ingresos":  TabCorreccionesIngresos.Visibility = Visibility.Visible; break;
                case "egresos":   TabCorreccionesEgresos.Visibility  = Visibility.Visible; break;
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
            _btnActivo       = btn;
            btn.Background   = new SolidColorBrush(Color.FromRgb(0x25, 0x2A, 0x40));
            btn.BorderBrush  = new SolidColorBrush(Color.FromRgb(0x4A, 0x6F, 0xE3));
            btn.Foreground   = Brushes.White;
        }

        // ─── Navegación lateral ───────────────────────────────────────────────

        private void BtnNav_Articulos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("articulos");
            MarcarActivo(BtnNav_Articulos);
        }

        private void BtnNav_Pedidos_Click(object sender, RoutedEventArgs e)
        {
            bool expandir = PanelPedidosSub.Visibility == Visibility.Collapsed;
            PanelPedidosSub.Visibility = expandir ? Visibility.Visible : Visibility.Collapsed;
            if (expandir) { MostrarPanel("ventas"); MarcarActivo(BtnNav_Ventas); }
        }

        private void BtnNav_Ventas_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("ventas");
            MarcarActivo(BtnNav_Ventas);
        }

        private void BtnNav_Compras_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("compras");
            MarcarActivo(BtnNav_Compras);
        }

        private void BtnNav_Traspasos_Click(object sender, RoutedEventArgs e)
        {
            bool expandir = PanelTraspasosSub.Visibility == Visibility.Collapsed;
            PanelTraspasosSub.Visibility = expandir ? Visibility.Visible : Visibility.Collapsed;
            if (expandir) { MostrarPanel("entradas"); MarcarActivo(BtnNav_Entradas); }
        }

        private void BtnNav_Entradas_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("entradas");
            MarcarActivo(BtnNav_Entradas);
        }

        private void BtnNav_Salidas_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("salidas");
            MarcarActivo(BtnNav_Salidas);
        }

        private void BtnNav_Correcciones_Click(object sender, RoutedEventArgs e)
        {
            bool expandir = PanelCorrencionesSub.Visibility == Visibility.Collapsed;
            PanelCorrencionesSub.Visibility = expandir ? Visibility.Visible : Visibility.Collapsed;
            if (expandir) { MostrarPanel("ingresos"); MarcarActivo(BtnNav_Ingresos); }
        }

        private void BtnNav_Ingresos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("ingresos");
            MarcarActivo(BtnNav_Ingresos);
        }

        private void BtnNav_Egresos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("egresos");
            MarcarActivo(BtnNav_Egresos);
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

        // ─── Acciones rápidas ─────────────────────────────────────────────────

        private void BtnVentaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "venta";
            AppState.TipoPedido        = "rapido";
            new PedidosDetalle { Owner = this }.ShowDialog();
            TabVentas.CargarPedidos();
        }

        private void BtnEntradaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "entrada";
            new TraspasosDetalle { Owner = this }.ShowDialog();
            TabEntradas.CargarTraspasos();
        }

        private void BtnSalidaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "salida";
            new TraspasosDetalle { Owner = this }.ShowDialog();
            TabSalidas.CargarTraspasos();
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
