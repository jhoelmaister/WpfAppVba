using System.Windows;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ConsolaMovimientos : Window
    {
        public ConsolaMovimientos()
        {
            InitializeComponent();
            LblUsuario.Text = $"Usuario: {AppState.UsuarioActivo}  |  Período: {AppState.PeriodoActivo}";
        }

        // ─── VENTAS / COMPRAS ─────────────────────────────────────────────────

        private void BtnVentaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "venta";
            AppState.TipoPedido        = "rapido";
            new PedidosDetalle().Show();
        }

        private void BtnVentas_Click(object sender, RoutedEventArgs e)
        {
            AppState.TipoMovimiento = "venta";
            new PedidosGeneral().Show();
        }

        private void BtnCompras_Click(object sender, RoutedEventArgs e)
        {
            AppState.TipoMovimiento = "compra";
            new PedidosGeneral().Show();
        }

        // ─── TRASPASOS ────────────────────────────────────────────────────────

        private void BtnEntradaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "entrada";
            new TraspasosDetalle().Show();
        }

        private void BtnSalidaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "salida";
            new TraspasosDetalle().Show();
        }

        private void BtnEntradas_Click(object sender, RoutedEventArgs e)
        {
            AppState.TipoMovimiento = "entrada";
            new TraspasosGeneral().Show();
        }

        private void BtnSalidas_Click(object sender, RoutedEventArgs e)
        {
            AppState.TipoMovimiento = "salida";
            new TraspasosGeneral().Show();
        }

        // ─── CATÁLOGOS ────────────────────────────────────────────────────────

        private void BtnArticulos_Click(object sender, RoutedEventArgs e)
        {
            new ArticulosGeneral().Show();
        }

        private void BtnTerceros_Click(object sender, RoutedEventArgs e)
        {
            new TercerosGeneral().Show();
        }

        private void BtnInventarios_Click(object sender, RoutedEventArgs e)
        {
            new InventariosGeneral().Show();
        }

        private void BtnUsuario_Click(object sender, RoutedEventArgs e)
        {
            new DatosUsuario().ShowDialog();
            // Refrescar etiqueta por si cambió el periodo
            LblUsuario.Text = $"Usuario: {AppState.UsuarioActivo}  |  Período: {AppState.PeriodoActivo}";
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
