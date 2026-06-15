using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class Configuracion : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private bool _cargando;

        // Sucursales de la empresa seleccionada en el combo (consulta directa, ya que el
        // caché global está filtrado por la empresa activa y puede no tenerlas todas).
        private DataConsulta? _sucursalesEmpresa;

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

        private class EmpresaItem
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
                string empId    = Sql.UsuariosObj.ObtenerItem("empresa",   usuId)?.ToString() ?? "";
                string tipo     = Sql.UsuariosObj.ObtenerItem("tipo",      usuId)?.ToString() ?? "";
                string temaDb   = Sql.UsuariosObj.ObtenerItem("temaC",     usuId)?.ToString() ?? "";

                TxtCuenta.Text    = cuenta;
                TxtNombres.Text   = nombres;
                TxtApellidos.Text = apellidos;
                TxtTipo.Text      = tipo;

                bool esAdmin = AppState.EsAdmin;
                CmbEmpresa.IsEnabled   = esAdmin;
                CmbSucursal.IsEnabled  = esAdmin;

                // Tema: si el valor de BD no es válido, usar el tema activo o "claro"
                string temaInicial = temaDb.Trim().ToLowerInvariant() == ThemeManager.TemaOscuro
                    ? ThemeManager.TemaOscuro
                    : ThemeManager.TemaClaro;
                SeleccionarTema(temaInicial);

                // Llenar ComboBox de empresas
                CmbEmpresa.Items.Clear();
                int totalEmp = Sql.EmpresasObj.ContarFilas;
                for (int i = 1; i <= totalEmp; i++)
                {
                    var idObj = Sql.EmpresasObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    string desc = Sql.EmpresasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                    CmbEmpresa.Items.Add(new EmpresaItem { Id = id, Descripcion = desc });
                }

                var empActual = CmbEmpresa.Items.OfType<EmpresaItem>()
                                                .FirstOrDefault(x => x.Id == empId);
                if (empActual != null) CmbEmpresa.SelectedItem = empActual;
                else if (CmbEmpresa.Items.Count > 0) CmbEmpresa.SelectedIndex = 0;

                // Llenar ComboBox de sucursales según la empresa seleccionada
                PoblarSucursales((CmbEmpresa.SelectedItem as EmpresaItem)?.Id ?? "");

                var actual = CmbSucursal.Items.OfType<SucursalItem>()
                                              .FirstOrDefault(s => s.Id == sucId);
                if (actual != null) CmbSucursal.SelectedItem = actual;
                else if (CmbSucursal.Items.Count > 0) CmbSucursal.SelectedIndex = 0;

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

        // ─── Poblar sucursales según la empresa seleccionada ─────────────────
        private void PoblarSucursales(string empresaId)
        {
            CmbSucursal.Items.Clear();
            _sucursalesEmpresa = null;
            if (string.IsNullOrEmpty(empresaId)) return;

            // Online: consulta directa, ya que las sucursales de la empresa elegida pueden
            // no estar en el caché global (que está filtrado por la empresa activa).
            if (ConexionEstado.EnLinea)
            {
                try
                {
                    var sucursales = new DataConsulta();
                    sucursales.Conectar("sucursales",
                        $"SELECT * FROM sucursales WHERE estadof = 'normal' AND empresa = '{empresaId}' ORDER BY id ASC");
                    _sucursalesEmpresa = sucursales;

                    int total = sucursales.ContarFilas;
                    for (int i = 1; i <= total; i++)
                    {
                        var idObj = sucursales.Mover(i);
                        if (idObj == null) continue;
                        string id = idObj.ToString()!;
                        string desc = sucursales.ObtenerItem("descripcion", id)?.ToString() ?? "";
                        CmbSucursal.Items.Add(new SucursalItem { Id = id, Descripcion = desc });
                    }
                    return;
                }
                catch
                {
                    _sucursalesEmpresa = null;
                    CmbSucursal.Items.Clear();
                }
            }

            // Sin conexión (o falló la consulta): usar el caché global de sucursales
            // filtrando por la empresa pedida. Evita el congelamiento por consultar SQL.
            int totalCache = Sql.SucursalesObj.ContarFilas;
            for (int i = 1; i <= totalCache; i++)
            {
                var idObj = Sql.SucursalesObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                string emp = Sql.SucursalesObj.ObtenerItem("empresa", id)?.ToString() ?? "";
                if (emp != empresaId) continue;
                string desc = Sql.SucursalesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                CmbSucursal.Items.Add(new SucursalItem { Id = id, Descripcion = desc });
            }
        }

        // ─── Empresa: refiltra las sucursales dependientes ───────────────────
        private void CmbEmpresa_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargando) return;
            PoblarSucursales((CmbEmpresa.SelectedItem as EmpresaItem)?.Id ?? "");
            if (CmbSucursal.Items.Count > 0) CmbSucursal.SelectedIndex = 0;
            ActualizarFechaInicio();
            ActualizarPeriodos();
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
            {
                // Offline _sucursalesEmpresa es null: caer al caché global de sucursales.
                var fechaObj = _sucursalesEmpresa?.ObtenerItem("fecha", item.Id)
                               ?? Sql.SucursalesObj.ObtenerItem("fecha", item.Id);
                TxtFechaInicio.Text = fechaObj?.ToString() ?? "";
            }
            else
                TxtFechaInicio.Text = "";
        }

        private void ActualizarPeriodos()
        {
            int inicioAno = DateTime.Now.Year;
            if (CmbSucursal.SelectedItem is SucursalItem item)
            {
                // Base = año de la MÁXIMA fecha de inventario de la sucursal. Si la
                // sucursal no tiene inventarios, se usa el año de sucursal.fecha.
                // MaxFecha consulta SQL en vivo: solo se intenta si hay conexión (offline
                // se cae al año de sucursal.fecha del caché, sin congelar).
                DateTime? maxInv = null;
                if (ConexionEstado.EnLinea)
                {
                    try { maxInv = Sql.DocumentosIObj.MaxFecha("sucursal", item.Id); }
                    catch { maxInv = null; }
                }

                if (maxInv.HasValue)
                {
                    inicioAno = maxInv.Value.Year;
                }
                else
                {
                    var fechaObj = _sucursalesEmpresa?.ObtenerItem("fecha", item.Id)
                                   ?? Sql.SucursalesObj.ObtenerItem("fecha", item.Id);
                    if (fechaObj != null && DateTime.TryParse(fechaObj.ToString(), out DateTime fecha))
                        inicioAno = fecha.Year;
                }
            }

            string? selActual = CmbPeriodo.SelectedItem?.ToString();
            CmbPeriodo.Items.Clear();
            int final = Math.Max(DateTime.Now.Year, inicioAno);
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

        // ─── Sincronizar AppSheets ────────────────────────────────────────────
        private void BtnSincronizarAppsheets_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SincronizarAppsheetsDialog { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true ||
                dlg.Opcion == SincronizarAppsheetsDialog.OpcionSync.Cancelar)
                return;

            var btn = sender as Button;
            try
            {
                if (btn != null) btn.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;

                string resumen = dlg.Opcion == SincronizarAppsheetsDialog.OpcionSync.Todo
                    ? AppsheetsSync.SincronizarTodasLasSucursales()
                    : AppsheetsSync.SincronizarSucursalActiva();

                MessageBox.Show(resumen, "Sincronizar AppSheets",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al sincronizar AppSheets: {ex.Message}",
                                "Sincronizar AppSheets", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                if (btn != null) btn.IsEnabled = true;
            }
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
            // Verificación de conexión en 2 capas (label + chequeo real) antes de guardar.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this)))
                return;

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

        // ─── Guardar ─────────────────────────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Verificación de conexión en 2 capas (label + chequeo real) antes de guardar.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this)))
                return;

            // No se puede guardar sin una sucursal activa (empresa sin sucursales
            // o combo vacío): se requiere una sucursal para el contexto de trabajo.
            if (CmbSucursal.SelectedItem is not SucursalItem)
            {
                MessageBox.Show(
                    "La empresa seleccionada no tiene sucursales o no hay una sucursal elegida.\n" +
                    "Seleccioná una sucursal para poder guardar los cambios.",
                    "Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string usuId = AppState.UsuarioActivo.ToString();

                // Actualizar nombres y apellidos en memoria / SQL
                Sql.UsuariosObj.EstablecerItem("nombres",   usuId, TxtNombres.Text.Trim());
                Sql.UsuariosObj.EstablecerItem("apellidos", usuId, TxtApellidos.Text.Trim());

                // Actualizar empresa activa
                bool empresaCambio = false;
                if (CmbEmpresa.SelectedItem is EmpresaItem empItem)
                {
                    string empActualBD = Sql.UsuariosObj.ObtenerItem("empresa", usuId)?.ToString() ?? "";
                    if (empItem.Id != empActualBD)
                    {
                        Sql.UsuariosObj.EstablecerItem("empresa", usuId, empItem.Id);
                        empresaCambio = true;
                    }
                    AppState.EmpresaActiva = empItem.Id;
                }

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
                    AppState.RegionActiva   = _sucursalesEmpresa?.ObtenerItem("region", sucItem.Id)?.ToString()
                                              ?? Sql.SucursalesObj.ObtenerItem("region", sucItem.Id)?.ToString() ?? "";
                }

                // Persistir cambios de usuarios en SQL Server
                Sql.UsuariosObj.ExportarItems();

                // Si cambió la empresa: recargar maestros/cachés filtrados por la nueva empresa
                if (empresaCambio)
                    AppLoader.ConectarProductos();

                // Si cambió empresa o sucursal: recargar documentosI e inventarios de la nueva
                // sucursal ANTES de ActualizarBase, para calcular la apertura con datos correctos
                if (empresaCambio || sucursalCambio)
                    AppLoader.ConectarBases();

                // Actualizar periodo activo y recalcular apertura
                string periodoStr = CmbPeriodo.SelectedItem?.ToString() ?? DateTime.Now.Year.ToString();
                bool periodoCambio = periodoStr != AppState.PeriodoActivo;
                AppState.PeriodoActivo = periodoStr;

                if (int.TryParse(periodoStr, out int periodo))
                    AppState.ActualizarBase(periodo);

                if (empresaCambio || sucursalCambio || periodoCambio)
                    AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);

                MessageBox.Show("Guardado exitoso", "Configuración",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                bool contextoCambio = empresaCambio || sucursalCambio || periodoCambio;

                // Si está embebido en ConsolaMovimientos (tab):
                //   - con cambio de contexto: refrescar toda la consola SIN cerrar sesión,
                //     manteniendo el enfoque en Configuración (como si recién iniciara sesión).
                //   - sin cambio de contexto: solo actualizar el header.
                // Si es diálogo independiente: cerrar.
                var w = Window.GetWindow(this);
                if (w is ConsolaMovimientos cm)
                {
                    if (contextoCambio)
                    {
                        cm.RecargarContexto();
                        // RecargarContexto conserva este panel de Configuración como
                        // panel fijo activo; repoblar sus combos con los datos recargados.
                        CargarDatos();
                    }
                    else
                    {
                        cm.ActualizarInfoUsuario();
                    }
                }
                else
                {
                    w?.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Configuración",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
