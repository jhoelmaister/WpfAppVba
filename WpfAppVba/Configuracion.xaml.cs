using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        // ─── Cerrar sesión y reabrir login con el servidor activo ────────────
        private void CerrarSesionYReabrirLogin()
        {
            AppState.SesionActiva  = false;
            AppState.UsuarioActivo = "";
            DatabaseConnection.CerrarConexion();

            // LoginWindow lee el servidor activo desde ConexionConfig,
            // por lo que el seleccionado queda como predeterminado.
            var login = new LoginWindow();
            login.Show();

            Window.GetWindow(this)?.Close();
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
                    AppState.SucursalActiva = sucItem.Id;
                    AppState.RegionActiva   = Sql.SucursalesObj.ObtenerItem("region", sucItem.Id)?.ToString() ?? "";
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

                // Si se cambió la sucursal activa, cerrar sesión y reabrir login.
                if (sucursalCambio)
                {
                    CerrarSesionYReabrirLogin();
                    return;
                }

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
