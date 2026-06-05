using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class Configuracion : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private bool _cargando;

        public Configuracion()
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
                string tipo     = Sql.UsuariosObj.ObtenerItem("tipo",      usuId)?.ToString() ?? "";
                string temaDb   = Sql.UsuariosObj.ObtenerItem("temaC",     usuId)?.ToString() ?? "";

                TxtCuenta.Text    = cuenta;
                TxtNombres.Text   = nombres;
                TxtApellidos.Text = apellidos;
                TxtTipo.Text      = tipo;

                // Conexión SQL Server guardada
                var cfg = ConexionConfig.Cargar();
                if (cfg != null)
                {
                    TxtConServidor.Text    = cfg.Value.servidor;
                    TxtConBaseDatos.Text   = cfg.Value.baseDatos;
                    TxtConUsuario.Text     = cfg.Value.usuario;
                    PwdConContrasena.Password = cfg.Value.contrasena;
                }

                // Tema: si el valor de BD no es válido, usar el tema activo o "claro"
                string temaInicial = temaDb.Trim().ToLowerInvariant() == ThemeManager.TemaOscuro
                    ? ThemeManager.TemaOscuro
                    : ThemeManager.TemaClaro;
                SeleccionarTema(temaInicial);

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
                ActualizarPeriodos();

                // Restaurar periodo activo si está disponible en el combo
                if (CmbPeriodo.Items.Contains(AppState.PeriodoActivo))
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
            ActualizarPeriodos();
        }

        private void ActualizarFechaInicio()
        {
            if (CmbSucursal.SelectedItem is SucursalItem item)
                TxtFechaInicio.Text = Sql.SucursalesObj.ObtenerItem("fecha", item.Id)?.ToString() ?? "";
            else
                TxtFechaInicio.Text = "";
        }

        private void ActualizarPeriodos()
        {
            int inicioAno = DateTime.Now.Year;
            if (CmbSucursal.SelectedItem is SucursalItem item)
            {
                var fechaObj = Sql.SucursalesObj.ObtenerItem("fecha", item.Id);
                if (fechaObj != null && DateTime.TryParse(fechaObj.ToString(), out DateTime fecha))
                    inicioAno = fecha.Year;
            }

            string? selActual = CmbPeriodo.SelectedItem?.ToString();
            CmbPeriodo.Items.Clear();
            int final = DateTime.Now.Year;
            for (int y = inicioAno; y <= final; y++)
                CmbPeriodo.Items.Add(y.ToString());

            // Mantener el periodo seleccionado si sigue disponible; si no, el más reciente
            if (selActual != null && CmbPeriodo.Items.Contains(selActual))
                CmbPeriodo.SelectedItem = selActual;
            else if (CmbPeriodo.Items.Count > 0)
                CmbPeriodo.SelectedIndex = CmbPeriodo.Items.Count - 1;
        }

        // ─── Tema ─────────────────────────────────────────────────────────────
        private void SeleccionarTema(string tema)
        {
            foreach (ComboBoxItem item in CmbTema.Items)
            {
                if (item.Content?.ToString() == tema)
                { CmbTema.SelectedItem = item; return; }
            }
            if (CmbTema.Items.Count > 0) CmbTema.SelectedIndex = 0;
        }

        private void CmbTema_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // El tema solo se aplica al guardar con BtnGuardarTema
        }

        // ─── Cambiar contraseña ───────────────────────────────────────────────
        private void BtnCambiarContrasena_Click(object sender, RoutedEventArgs e)
        {
            var win = new CambiarContrasena
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        // ─── Guardar solo el tema ────────────────────────────────────────────
        private void BtnGuardarTema_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string usuId = AppState.UsuarioActivo.ToString();
                string tema  = (CmbTema.SelectedItem as ComboBoxItem)?.Content?.ToString()
                               ?? ThemeManager.TemaClaro;

                ThemeManager.AplicarTema(tema);
                AppState.TemaActivo = tema;

                Sql.UsuariosObj.EstablecerItem("temaC", usuId, tema);
                Sql.UsuariosObj.ExportarItems();

                MessageBox.Show("Tema guardado", "Configuración",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el tema: {ex.Message}", "Configuración",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Conexión SQL Server ─────────────────────────────────────────────

        private string ObtenerConContrasena() =>
            TxtConContrasenaVisible.Visibility == Visibility.Visible
                ? TxtConContrasenaVisible.Text
                : PwdConContrasena.Password;

        private void BtnMostrarConPwd_Click(object sender, RoutedEventArgs e)
        {
            if (PwdConContrasena.Visibility == Visibility.Visible)
            {
                TxtConContrasenaVisible.Text    = PwdConContrasena.Password;
                PwdConContrasena.Visibility     = Visibility.Collapsed;
                TxtConContrasenaVisible.Visibility = Visibility.Visible;
                BtnMostrarConPwd.Content        = "Ocultar";
            }
            else
            {
                PwdConContrasena.Password          = TxtConContrasenaVisible.Text;
                TxtConContrasenaVisible.Visibility = Visibility.Collapsed;
                PwdConContrasena.Visibility        = Visibility.Visible;
                BtnMostrarConPwd.Content           = "Ver";
            }
        }

        private async void BtnProbarConexion_Click(object sender, RoutedEventArgs e)
        {
            string servidor   = TxtConServidor.Text.Trim();
            string baseDatos  = TxtConBaseDatos.Text.Trim();
            string usuario    = TxtConUsuario.Text.Trim();
            string contrasena = ObtenerConContrasena();

            if (string.IsNullOrEmpty(servidor) || string.IsNullOrEmpty(baseDatos))
            {
                MessageBox.Show("Completa al menos Servidor y Base de datos.", "Probar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnProbarConexion.IsEnabled  = false;
            BtnGuardarConexion.IsEnabled = false;
            try
            {
                string cs = $"Server={servidor};Database={baseDatos};User Id={usuario};Password={contrasena};" +
                            "Connect Timeout=10;TrustServerCertificate=True;";
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(cs);
                    conn.Open();
                    using var cmd = new SqlCommand("SELECT 1", conn);
                    cmd.ExecuteScalar();
                });
                MessageBox.Show("Conexión exitosa.", "Probar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar:\n{ex.Message}", "Probar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnProbarConexion.IsEnabled  = true;
                BtnGuardarConexion.IsEnabled = true;
            }
        }

        private void BtnGuardarConexion_Click(object sender, RoutedEventArgs e)
        {
            string servidor   = TxtConServidor.Text.Trim();
            string baseDatos  = TxtConBaseDatos.Text.Trim();
            string usuario    = TxtConUsuario.Text.Trim();
            string contrasena = ObtenerConContrasena();

            if (string.IsNullOrEmpty(servidor) || string.IsNullOrEmpty(baseDatos))
            {
                MessageBox.Show("Completa al menos Servidor y Base de datos.", "Guardar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ConexionConfig.Guardar(servidor, baseDatos, usuario, contrasena);
                DatabaseConnection.Configurar(servidor, baseDatos, usuario, contrasena);
                MessageBox.Show("Conexión guardada. Los cambios se aplicarán al reiniciar la aplicación.",
                                "Guardar conexión", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Guardar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                // Si cambió la sucursal: recargar documentosI e inventarios de la nueva sucursal
                // ANTES de ActualizarBase, para que calcule la apertura con los datos correctos
                if (sucursalCambio)
                    AppLoader.ConectarBases();

                // Actualizar periodo activo y recalcular apertura
                string periodoStr = CmbPeriodo.SelectedItem?.ToString() ?? DateTime.Now.Year.ToString();
                bool periodoCambio = periodoStr != AppState.PeriodoActivo;
                AppState.PeriodoActivo = periodoStr;

                if (int.TryParse(periodoStr, out int periodo))
                    AppState.ActualizarBase(periodo);

                if (periodoCambio || sucursalCambio)
                    AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);

                MessageBox.Show("Guardado exitoso", "Configuración",
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
                MessageBox.Show($"Error al guardar: {ex.Message}", "Configuración",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
