using System;
using System.Windows;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class DatosUsuario : Window
    {
        private static SqlData Sql => SqlData.Instance;

        public DatosUsuario()
        {
            InitializeComponent();
            Loaded += (_, _) => CargarDatos();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarDatos()
        {
            string usuId = AppState.UsuarioActivo.ToString();

            string cuenta   = Sql.UsuariosObj.ObtenerItem("cuenta",    usuId)?.ToString() ?? "";
            string nombres  = Sql.UsuariosObj.ObtenerItem("nombres",   usuId)?.ToString() ?? "";
            string apellidos= Sql.UsuariosObj.ObtenerItem("apellidos", usuId)?.ToString() ?? "";
            string sucId    = Sql.UsuariosObj.ObtenerItem("sucursal",  usuId)?.ToString() ?? "";
            string sucDesc  = Sql.SucursalesObj.ObtenerItem("descripcion", sucId)?.ToString() ?? "";
            string sucIni   = Sql.SucursalesObj.ObtenerItem("fecha",       sucId)?.ToString() ?? "";

            TxtCuenta.Text      = cuenta;
            TxtNombres.Text     = nombres;
            TxtApellidos.Text   = apellidos;
            TxtSucursal.Text    = sucDesc;
            TxtFechaInicio.Text = sucIni;

            // Llenar ComboBox con años desde la apertura hasta hoy
            CmbPeriodo.Items.Clear();
            int inicio = AppState.AperturaFecha != default
                         ? AppState.AperturaFecha.Year
                         : DateTime.Now.Year;
            int final  = DateTime.Now.Year;
            for (int y = inicio; y <= final; y++)
                CmbPeriodo.Items.Add(y.ToString());

            // Seleccionar el periodo activo
            CmbPeriodo.SelectedItem = AppState.PeriodoActivo;
        }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string usuId = AppState.UsuarioActivo.ToString();

                // Actualizar nombres y apellidos en memoria / SQL
                Sql.UsuariosObj.EstablecerItem("nombres",   usuId, TxtNombres.Text.Trim());
                Sql.UsuariosObj.EstablecerItem("apellidos", usuId, TxtApellidos.Text.Trim());

                // Actualizar periodo activo
                string periodoStr = CmbPeriodo.SelectedItem?.ToString() ?? DateTime.Now.Year.ToString();
                AppState.PeriodoActivo = periodoStr;

                if (int.TryParse(periodoStr, out int periodo))
                {
                    AppState.ActualizarBase(periodo);
                    AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);
                }

                MessageBox.Show("Guardado exitoso", "Datos de Usuario",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Datos de Usuario",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
