using System.Windows;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ConsolaMovimientos : Window
    {
        public ConsolaMovimientos()
        {
            InitializeComponent();
            ActualizarInfoUsuario();
        }

        // ─── Actualiza la etiqueta de usuario/período en el header ────────────
        public void ActualizarInfoUsuario()
        {
            LblUsuario.Text = $"Usuario: {AppState.UsuarioActivo}  |  Período: {AppState.PeriodoActivo}";
        }

        // ─── ACCIONES RÁPIDAS (abren formulario de detalle directamente) ─────

        private void BtnVentaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "venta";
            AppState.TipoPedido        = "rapido";
            new PedidosDetalle().ShowDialog();
            TabVentas.CargarPedidos();
        }

        private void BtnEntradaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "entrada";
            new TraspasosDetalle().ShowDialog();
            TabEntradas.CargarTraspasos();
        }

        private void BtnSalidaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "salida";
            new TraspasosDetalle().ShowDialog();
            TabSalidas.CargarTraspasos();
        }

        // ─── INVENTARIOS ─────────────────────────────────────────────────────

        private void BtnInventarios_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title                 = "Inventarios",
                Width                 = 520,
                Height                = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = this,
                Background            = System.Windows.Media.Brushes.Transparent,
                Content               = new InventariosGeneral()
            };
            win.ShowDialog();
        }

        // ─── MI CUENTA ────────────────────────────────────────────────────────

        private void BtnMiCuenta_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title                 = "Configuración",
                Width                 = 480,
                Height                = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = this,
                ResizeMode            = ResizeMode.NoResize,
                Background            = System.Windows.Media.Brushes.Transparent,
                Content               = new DatosUsuario()
            };
            win.ShowDialog();
            ActualizarInfoUsuario();
        }

        // ─── CERRAR SESIÓN ────────────────────────────────────────────────────

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
