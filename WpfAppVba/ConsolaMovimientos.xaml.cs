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
            // TODO: new PedidosDetalle().Show();
            MessageBox.Show("Venta Rápida — próximamente");
        }

        private void BtnVentas_Click(object sender, RoutedEventArgs e)
        {
            AppState.TipoMovimiento = "venta";
            // TODO: new PedidosGeneral().Show();
            MessageBox.Show("Registros de Ventas — próximamente");
        }

        private void BtnCompras_Click(object sender, RoutedEventArgs e)
        {
            AppState.TipoMovimiento = "compra";
            // TODO: new PedidosGeneral().Show();
            MessageBox.Show("Registros de Compras — próximamente");
        }

        // ─── TRASPASOS ────────────────────────────────────────────────────────

        private void BtnEntradaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "entrada";
            // TODO: new TraspasoDetalle().Show();
            MessageBox.Show("Entrada Rápida — próximamente");
        }

        private void BtnSalidaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "salida";
            // TODO: new TraspasoDetalle().Show();
            MessageBox.Show("Salida Rápida — próximamente");
        }

        private void BtnEntradas_Click(object sender, RoutedEventArgs e)
        {
            AppState.TipoMovimiento = "entrada";
            // TODO: new TraspasoGeneral().Show();
            MessageBox.Show("Registros de Entradas — próximamente");
        }

        private void BtnSalidas_Click(object sender, RoutedEventArgs e)
        {
            AppState.TipoMovimiento = "salida";
            // TODO: new TraspasoGeneral().Show();
            MessageBox.Show("Registros de Salidas — próximamente");
        }

        // ─── CATÁLOGOS ────────────────────────────────────────────────────────

        private void BtnArticulos_Click(object sender, RoutedEventArgs e)
        {
            // TODO: new ArticulosGeneral().Show();
            MessageBox.Show("Registros de Artículos — próximamente");
        }

        private void BtnTerceros_Click(object sender, RoutedEventArgs e)
        {
            // TODO: new TercerosGeneral().Show();
            MessageBox.Show("Registros de Terceros — próximamente");
        }

        private void BtnInventarios_Click(object sender, RoutedEventArgs e)
        {
            // TODO: new InventariosGeneral().Show();
            MessageBox.Show("Registro de Inventarios — próximamente");
        }

        private void BtnUsuario_Click(object sender, RoutedEventArgs e)
        {
            // TODO: new DatosUsuario().Show();
            MessageBox.Show("Datos de Usuario — próximamente");
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
