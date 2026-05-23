using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class DatosUsuario : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private bool _cargando;

        public DatosUsuario()
        {
            InitializeComponent();
            Loaded += (_, _) => CargarDatos();
        }

        private class SucursalItem
        {
            public string Id          { get; set; } = "";
            public string Descripcion { get; set; } = "";
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarDatos()
        {
            _cargando = true;
            try
            {
                string usuId = AppState.UsuarioActivo.ToString();

                string cuenta   = Sql.UsuariosObj.ObtenerItem("cuenta",    usuId)?.ToString() ?? "";
                string nombres  = Sql.UsuariosObj.ObtenerItem("nombres",   usuId)?.ToString() ?? "";
                string apellidos= Sql.UsuariosObj.ObtenerItem("apellidos", usuId)?.ToString() ?? "";
                string sucId    = Sql.UsuariosObj.ObtenerItem("sucursal",  usuId)?.ToString() ?? "";

                TxtCuenta.Text    = cuenta;
                TxtNombres.Text   = nombres;
                TxtApellidos.Text = apellidos;

                // Llenar ComboBox de sucursales
                CmbSucursal.Items.Clear();
                int total = Sql.SucursalesObj.ContarFilas;
                for (int i = 1; i <= total; i++)
                {
                    var idObj = Sql.SucursalesObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    string desc = Sql.SucursalesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                    CmbSucursal.Items.Add(new SucursalItem { Id = id, Descripcion = desc });
                }

                var actual = CmbSucursal.Items.OfType<SucursalItem>()
                                              .FirstOrDefault(s => s.Id == sucId);
                if (actual != null) CmbSucursal.SelectedItem = actual;

                ActualizarFechaInicio();

                // Llenar ComboBox con años desde la apertura hasta hoy
                CmbPeriodo.Items.Clear();
                int inicio = AppState.AperturaFecha != default
                             ? AppState.AperturaFecha.Year
                             : DateTime.Now.Year;
                int final  = DateTime.Now.Year;
                for (int y = inicio; y <= final; y++)
                    CmbPeriodo.Items.Add(y.ToString());

                CmbPeriodo.SelectedItem = AppState.PeriodoActivo;
            }
            finally
            {
                _cargando = false;
            }
        }

        // ─── Actualizar fecha de inicio según sucursal seleccionada ──────────
        private void CmbSucursal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargando) return;
            ActualizarFechaInicio();
        }

        private void ActualizarFechaInicio()
        {
            if (CmbSucursal.SelectedItem is SucursalItem item)
                TxtFechaInicio.Text = Sql.SucursalesObj.ObtenerItem("fecha", item.Id)?.ToString() ?? "";
            else
                TxtFechaInicio.Text = "";
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

                // Actualizar sucursal activa
                bool sucursalCambio = false;
                if (CmbSucursal.SelectedItem is SucursalItem sucItem)
                {
                    string sucActualBD = Sql.UsuariosObj.ObtenerItem("sucursal", usuId)?.ToString() ?? "";
                    if (sucItem.Id != sucActualBD)
                    {
                        Sql.UsuariosObj.EstablecerItem("sucursal", usuId, sucItem.Id);
                        sucursalCambio = true;
                    }
                    AppState.SucursalActiva = Convert.ToInt64(sucItem.Id);
                    AppState.RegionActiva   = Convert.ToInt64(
                        Sql.SucursalesObj.ObtenerItem("region", sucItem.Id) ?? 0);
                }

                // Persistir cambios de usuarios en SQL Server
                Sql.UsuariosObj.ExportarItems();

                // Actualizar periodo activo
                string periodoStr = CmbPeriodo.SelectedItem?.ToString() ?? DateTime.Now.Year.ToString();
                bool periodoCambio = periodoStr != AppState.PeriodoActivo;
                AppState.PeriodoActivo = periodoStr;

                if (int.TryParse(periodoStr, out int periodo))
                    AppState.ActualizarBase(periodo);

                if (periodoCambio || sucursalCambio)
                    AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);

                MessageBox.Show("Guardado exitoso", "Datos de Usuario",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                // Si está embebido en ConsolaMovimientos (tab), actualizar header; si es diálogo, cerrar
                var w = Window.GetWindow(this);
                if (w is ConsolaMovimientos cm)
                    cm.ActualizarInfoUsuario();
                else
                    w?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Datos de Usuario",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
