using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ConsolaMovimientos : Window
    {
        private Button? _btnActivo;

        // Paneles "General": mutables para poder recrearlos tras un cambio de contexto
        // (empresa/sucursal/periodo) y que relean los cachés recién cargados.
        private ArticulosGeneral    _panelArticulos    = new();
        private PedidosGeneral      _panelPedidos      = new();
        private TraspasosGeneral    _panelTraspasos    = new();
        private CorreccionesGeneral _panelCorrecciones = new();
        private TercerosGeneral     _panelTerceros     = new();
        private SucursalesGeneral   _panelSucursales   = new();
        private FamiliasGeneral     _panelFamilias     = new();
        private ProductosGeneral    _panelProductos    = new();
        private IndustriasGeneral   _panelIndustrias   = new();
        private CategoriasGeneral   _panelCategorias   = new();
        private InventariosGeneral  _panelInventarios  = new();
        private PreciosGeneral      _panelPrecios      = new();
        private RegionesGeneral     _panelRegiones     = new();
        private EmpresasGeneral     _panelEmpresas     = new();
        private readonly Configuracion       _panelConfiguracion= new();
        private UsuariosGeneral     _panelUsuarios     = new();
        private MovimientosGeneral  _panelMovimientos  = new();
        private DashboardGeneral    _panelDashboard    = new();

        // Cada sección del menú lateral conserva su propio juego de pestañas dinámicas.
        private string _seccionActiva = "articulos";
        private readonly Dictionary<string, List<TabItem>> _pestañasPorSeccion = new()
        {
            ["articulos"]    = new List<TabItem>(),
            ["pedidos"]      = new List<TabItem>(),
            ["traspasos"]    = new List<TabItem>(),
            ["correcciones"] = new List<TabItem>(),
            ["terceros"]     = new List<TabItem>(),
            ["sucursales"]   = new List<TabItem>(),
            ["familias"]     = new List<TabItem>(),
            ["productos"]    = new List<TabItem>(),
            ["industrias"]   = new List<TabItem>(),
            ["categorias"]   = new List<TabItem>(),
            ["inventarios"]  = new List<TabItem>(),
            ["precios"]      = new List<TabItem>(),
            ["regiones"]     = new List<TabItem>(),
            ["empresas"]     = new List<TabItem>(),
            ["configuracion"]= new List<TabItem>(),
            ["usuarios"]     = new List<TabItem>(),
            ["movimientos"]  = new List<TabItem>(),
            ["dashboard"]    = new List<TabItem>(),
        };
        private readonly Dictionary<string, TabItem?> _pestañaSeleccionadaPorSeccion = new()
        {
            ["articulos"]    = null,
            ["pedidos"]      = null,
            ["traspasos"]    = null,
            ["correcciones"] = null,
            ["terceros"]     = null,
            ["sucursales"]   = null,
            ["familias"]     = null,
            ["productos"]    = null,
            ["industrias"]   = null,
            ["categorias"]   = null,
            ["inventarios"]  = null,
            ["precios"]      = null,
            ["regiones"]     = null,
            ["empresas"]     = null,
            ["configuracion"]= null,
            ["usuarios"]     = null,
            ["movimientos"]  = null,
            ["dashboard"]    = null,
        };

        public ConsolaMovimientos()
        {
            InitializeComponent();
            TabFijoContenido.Content = _panelArticulos;
            ActualizarInfoUsuario();
            MarcarActivo(BtnNav_Articulos);
            if (AppState.EsAdmin) BtnNav_Usuarios.Visibility = Visibility.Visible;

            // Estado de conexión: pintar el estado actual y escuchar cambios.
            ActualizarLabelConexion(ConexionEstado.EnLinea);
            ConexionEstado.Cambio += OnConexionCambio;
            ConexionEstado.Iniciar(Dispatcher);
        }

        // ─── Estado de conexión (top bar) ─────────────────────────────────────
        private void OnConexionCambio(bool enLinea) => ActualizarLabelConexion(enLinea);

        private void ActualizarLabelConexion(bool enLinea)
        {
            if (enLinea)
            {
                LblConexion.Text = "●  En línea";
                PillConexion.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // verde
            }
            else
            {
                LblConexion.Text = "●  Sin conexión";
                PillConexion.Background = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)); // rojo
            }
        }

        // ─── Info usuario ─────────────────────────────────────────────────────
        public void ActualizarInfoUsuario()
        {
            var sql = SqlData.Instance;
            string nombres = sql.UsuariosObj.ObtenerItem("nombres", AppState.UsuarioActivo)?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(nombres)) nombres = AppState.UsuarioActivo;
            string sucursalDesc = sql.SucursalesObj.ObtenerItem("descripcion", AppState.SucursalActiva)?.ToString() ?? "";
            string empresaDesc  = sql.EmpresasObj.ObtenerItem("descripcion", AppState.EmpresaActiva)?.ToString() ?? "";

            LblUsuario.Text  = $"Usuario: {nombres}  |  Período: {AppState.PeriodoActivo}";
            LblSucursal.Text = $"Sucursal: {sucursalDesc}";
            LblEmpresa.Text  = $"Empresa: {empresaDesc}";
        }

        /// <summary>
        /// Refresca toda la consola tras un cambio de contexto (empresa/sucursal/periodo)
        /// hecho desde Configuración, sin cerrar sesión y manteniendo el enfoque en
        /// Configuración. Recrea los paneles "General" para que relean los cachés
        /// recién cargados (como si recién se hubiera iniciado sesión).
        /// </summary>
        public void RecargarContexto()
        {
            // 1. Cerrar todas las pestañas dinámicas (estado "recién iniciado")
            for (int i = TabContenido.Items.Count - 1; i >= 0; i--)
                if (TabContenido.Items[i] is TabItem t && t != TabFijo)
                    TabContenido.Items.RemoveAt(i);
            foreach (var clave in _pestañasPorSeccion.Keys.ToList())
                _pestañasPorSeccion[clave].Clear();
            foreach (var clave in _pestañaSeleccionadaPorSeccion.Keys.ToList())
                _pestañaSeleccionadaPorSeccion[clave] = null;

            // 2. Recrear los paneles "General" (no Configuración: se mantiene el enfoque)
            _panelArticulos    = new();
            _panelPedidos      = new();
            _panelTraspasos    = new();
            _panelCorrecciones = new();
            _panelTerceros     = new();
            _panelSucursales   = new();
            _panelFamilias     = new();
            _panelProductos    = new();
            _panelIndustrias   = new();
            _panelCategorias   = new();
            _panelInventarios  = new();
            _panelPrecios      = new();
            _panelRegiones     = new();
            _panelEmpresas     = new();
            _panelMovimientos  = new();
            _panelDashboard    = new();
            _panelUsuarios     = new();

            // 3. Mantener Configuración como panel fijo enfocado
            _seccionActiva = "configuracion";
            TabFijoContenido.Content = _panelConfiguracion;
            TabFijoTitulo.Text = "Configuración";
            TabContenido.SelectedItem = TabFijo;
            MarcarActivo(BtnNav_Configuracion);

            // 4. Refrescar la barra superior
            ActualizarInfoUsuario();
        }

        // ─── Navegación por pestañas ──────────────────────────────────────────
        private void MostrarPanel(string nombre)
        {
            if (nombre == _seccionActiva)
            {
                TabContenido.SelectedItem = TabFijo;
                return;
            }

            // 1. Guardar la pestaña activa y las pestañas dinámicas de la sección actual
            _pestañaSeleccionadaPorSeccion[_seccionActiva] = TabContenido.SelectedItem as TabItem;
            var guardadas = _pestañasPorSeccion[_seccionActiva];
            guardadas.Clear();
            for (int i = TabContenido.Items.Count - 1; i >= 0; i--)
            {
                if (TabContenido.Items[i] is TabItem t && t != TabFijo)
                {
                    guardadas.Insert(0, t);
                    TabContenido.Items.RemoveAt(i);
                }
            }

            // 2. Cambiar el contenido y título de la pestaña fija
            switch (nombre)
            {
                case "articulos":    TabFijoContenido.Content = _panelArticulos;    TabFijoTitulo.Text = "Artículos";    break;
                case "pedidos":      TabFijoContenido.Content = _panelPedidos;      TabFijoTitulo.Text = "Pedidos";      break;
                case "traspasos":    TabFijoContenido.Content = _panelTraspasos;    TabFijoTitulo.Text = "Traspasos";    break;
                case "correcciones": TabFijoContenido.Content = _panelCorrecciones; TabFijoTitulo.Text = "Correcciones"; break;
                case "terceros":     TabFijoContenido.Content = _panelTerceros;     TabFijoTitulo.Text = "Terceros";     break;
                case "sucursales":   TabFijoContenido.Content = _panelSucursales;   TabFijoTitulo.Text = "Sucursales";   break;
                case "familias":     TabFijoContenido.Content = _panelFamilias;     TabFijoTitulo.Text = "Familias";     break;
                case "productos":    TabFijoContenido.Content = _panelProductos;    TabFijoTitulo.Text = "Productos";    break;
                case "industrias":   TabFijoContenido.Content = _panelIndustrias;   TabFijoTitulo.Text = "Industrias";   break;
                case "categorias":   TabFijoContenido.Content = _panelCategorias;   TabFijoTitulo.Text = "Categorías";   break;
                case "inventarios":  TabFijoContenido.Content = _panelInventarios;  TabFijoTitulo.Text = "Inventarios";  break;
                case "precios":      TabFijoContenido.Content = _panelPrecios;      TabFijoTitulo.Text = "Precios";      break;
                case "regiones":     TabFijoContenido.Content = _panelRegiones;     TabFijoTitulo.Text = "Regiones";     break;
                case "empresas":     TabFijoContenido.Content = _panelEmpresas;     TabFijoTitulo.Text = "Empresas";     break;
                case "configuracion":TabFijoContenido.Content = _panelConfiguracion;TabFijoTitulo.Text = "Configuración";break;
                case "usuarios":     TabFijoContenido.Content = _panelUsuarios;     TabFijoTitulo.Text = "Usuarios";     break;
                case "movimientos":  TabFijoContenido.Content = _panelMovimientos;  TabFijoTitulo.Text = "Movimientos";  break;
                case "dashboard":    TabFijoContenido.Content = _panelDashboard;    TabFijoTitulo.Text = "Dashboard";    break;
            }

            // 3. Restaurar las pestañas propias de la nueva sección
            _seccionActiva = nombre;
            var restaurar = _pestañasPorSeccion[nombre];
            foreach (var t in restaurar)
                TabContenido.Items.Add(t);
            restaurar.Clear();

            // 4. Restaurar la pestaña que estaba activa al salir de esta sección
            var selAnterior = _pestañaSeleccionadaPorSeccion[nombre];
            TabContenido.SelectedItem = (selAnterior != null && TabContenido.Items.Contains(selAnterior))
                ? selAnterior
                : TabFijo;
        }

        public void AbrirPestaña(string titulo, UIElement contenido, string? clave = null)
        {
            foreach (TabItem t in TabContenido.Items)
            {
                if (clave != null && t.Tag as string == clave) { TabContenido.SelectedItem = t; return; }
                if (clave == null && t.Content == contenido)   { TabContenido.SelectedItem = t; return; }
            }

            var lblTitulo = new TextBlock
            {
                Text = titulo,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btnCerrar = new Button
            {
                Content           = "✕",
                Background        = Brushes.Transparent,
                BorderThickness   = new Thickness(0),
                Padding           = new Thickness(6, 0, 0, 0),
                FontSize          = 10,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB8)),
                VerticalAlignment = VerticalAlignment.Center,
                FocusVisualStyle  = null,
                Cursor            = Cursors.Hand
            };
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(lblTitulo);
            header.Children.Add(btnCerrar);

            var tab = new TabItem { Header = header, Content = contenido, Tag = clave };
            btnCerrar.Click += (s, e) =>
            {
                e.Handled = true;
                var intentar = contenido.GetType().GetMethod("IntentarCerrar", Type.EmptyTypes);
                if (intentar != null) intentar.Invoke(contenido, null);
                else CerrarPestaña(contenido);
            };

            TabContenido.Items.Add(tab);
            TabContenido.SelectedItem = tab;
        }

        public void CerrarPestaña(UIElement contenido)
        {
            TabItem? target = null;
            foreach (TabItem t in TabContenido.Items)
                if (t.Content == contenido) { target = t; break; }
            if (target == null) return;
            int idx = TabContenido.Items.IndexOf(target);
            TabContenido.Items.Remove(target);
            if (TabContenido.Items.Count > 0)
                TabContenido.SelectedIndex = Math.Max(0, idx - 1);
        }

        public void SeleccionarPestaña(UIElement? contenido)
        {
            if (contenido == null) return;
            foreach (TabItem t in TabContenido.Items)
                if (t.Content == contenido) { TabContenido.SelectedItem = t; return; }
        }

        public void CerrarPestañaPorClave(string clave)
        {
            TabItem? target = null;
            foreach (TabItem t in TabContenido.Items)
                if (t.Tag as string == clave) { target = t; break; }
            if (target == null) return;
            int idx = TabContenido.Items.IndexOf(target);
            TabContenido.Items.Remove(target);
            if (TabContenido.Items.Count > 0)
                TabContenido.SelectedIndex = Math.Max(0, idx - 1);
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

        private void BtnNav_Terceros_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("terceros");
            MarcarActivo(BtnNav_Terceros);
        }

        private void BtnNav_Sucursales_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("sucursales");
            MarcarActivo(BtnNav_Sucursales);
        }

        private void BtnNav_Familias_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("familias");
            MarcarActivo(BtnNav_Familias);
        }

        private void BtnNav_Productos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("productos");
            MarcarActivo(BtnNav_Productos);
        }

        private void BtnNav_Industrias_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("industrias");
            MarcarActivo(BtnNav_Industrias);
        }

        private void BtnNav_Categorias_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("categorias");
            MarcarActivo(BtnNav_Categorias);
        }

        private void BtnNav_Inventarios_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("inventarios");
            MarcarActivo(BtnNav_Inventarios);
        }

        private void BtnNav_Regiones_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("regiones");
            MarcarActivo(BtnNav_Regiones);
        }

        private void BtnNav_Precios_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("precios");
            MarcarActivo(BtnNav_Precios);
        }

        private void BtnNav_Empresas_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("empresas");
            MarcarActivo(BtnNav_Empresas);
        }

        private void BtnNav_Configuracion_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("configuracion");
            MarcarActivo(BtnNav_Configuracion);
        }

        private void BtnNav_Usuarios_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("usuarios");
            MarcarActivo(BtnNav_Usuarios);
        }

        // ─── Cerrar sesión ────────────────────────────────────────────────────

        private void BtnNav_Movimientos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("movimientos");
            MarcarActivo(BtnNav_Movimientos);
        }

        private void BtnNav_Dashboard_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("dashboard");
            MarcarActivo(BtnNav_Dashboard);
        }

        // ─── Accesos rápidos del top bar ──────────────────────────────────────
        private PedidosDetalle? BuscarTabPedidoRapido()
        {
            foreach (TabItem t in TabContenido.Items)
                if (t.Tag as string == "nuevo-pedido" && t.Content is PedidosDetalle pd) return pd;
            return null;
        }

        private TraspasosDetalle? BuscarTabTraspasoRapido()
        {
            foreach (TabItem t in TabContenido.Items)
                if (t.Tag as string == "nuevo-traspaso" && t.Content is TraspasosDetalle td) return td;
            return null;
        }

        private void BtnQuick_Venta_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("pedidos");
            MarcarActivo(BtnNav_Pedidos);
            var existing = BuscarTabPedidoRapido();
            if (existing != null) { existing.CambiarTipoMovimiento("venta"); foreach (TabItem t in TabContenido.Items) if (t.Content == existing) { TabContenido.SelectedItem = t; break; } }
            else _panelPedidos.AbrirNuevoPedido("rapido", "venta");
        }

        private void BtnQuick_Compra_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("pedidos");
            MarcarActivo(BtnNav_Pedidos);
            var existing = BuscarTabPedidoRapido();
            if (existing != null) { existing.CambiarTipoMovimiento("compra"); foreach (TabItem t in TabContenido.Items) if (t.Content == existing) { TabContenido.SelectedItem = t; break; } }
            else _panelPedidos.AbrirNuevoPedido("rapido", "compra");
        }

        private void BtnQuick_Salida_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("traspasos");
            MarcarActivo(BtnNav_Traspasos);
            var existing = BuscarTabTraspasoRapido();
            if (existing != null) { existing.CambiarTipoMovimiento("salida"); foreach (TabItem t in TabContenido.Items) if (t.Content == existing) { TabContenido.SelectedItem = t; break; } }
            else _panelTraspasos.AbrirNuevoTraspaso("salida");
        }

        private void BtnQuick_Entrada_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("traspasos");
            MarcarActivo(BtnNav_Traspasos);
            var existing = BuscarTabTraspasoRapido();
            if (existing != null) { existing.CambiarTipoMovimiento("entrada"); foreach (TabItem t in TabContenido.Items) if (t.Content == existing) { TabContenido.SelectedItem = t; break; } }
            else _panelTraspasos.AbrirNuevoTraspaso("entrada");
        }

        private void MarcarInactivo()
        {
            if (string.IsNullOrEmpty(AppState.UsuarioActivo)) return;
            try
            {
                var sql = SqlData.Instance;
                sql.UsuariosObj.EstablecerItem("estadoU", AppState.UsuarioActivo, "inactivo");
                sql.UsuariosObj.ExportarItems();
            }
            catch { }
        }

        private void ConsolaMovimientos_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ConexionEstado.Cambio -= OnConexionCambio;
            MarcarInactivo();
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            MarcarInactivo();
            AppState.SesionActiva  = false;
            AppState.UsuarioActivo = "";
            AppState.EmpresaActiva = "";
            DatabaseConnection.CerrarConexion();

            var login = new LoginWindow();
            login.Show();
            Close();
        }
    }
}
