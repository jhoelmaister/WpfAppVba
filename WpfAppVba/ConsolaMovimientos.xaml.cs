using System;
using System.Collections.Generic;
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

        private readonly ArticulosGeneral    _panelArticulos    = new();
        private readonly PedidosGeneral      _panelPedidos      = new();
        private readonly TraspasosGeneral    _panelTraspasos    = new();
        private readonly CorreccionesGeneral _panelCorrecciones = new();
        private readonly TercerosGeneral     _panelTerceros     = new();
        private readonly SucursalesGeneral   _panelSucursales   = new();
        private readonly FamiliasGeneral     _panelFamilias     = new();
        private readonly ProductosGeneral    _panelProductos    = new();
        private readonly IndustriasGeneral   _panelIndustrias   = new();
        private readonly CategoriasGeneral   _panelCategorias   = new();
        private readonly InventariosGeneral  _panelInventarios  = new();
        private readonly PreciosGeneral      _panelPrecios      = new();
        private readonly RegionesGeneral     _panelRegiones     = new();
        private readonly EmpresasGeneral     _panelEmpresas     = new();
        private readonly Configuracion       _panelConfiguracion= new();
        private readonly MovimientosGeneral  _panelMovimientos  = new();

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
            ["movimientos"]  = new List<TabItem>(),
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
            ["movimientos"]  = null,
        };

        public ConsolaMovimientos()
        {
            InitializeComponent();
            TabFijoContenido.Content = _panelArticulos;
            ActualizarInfoUsuario();
            MarcarActivo(BtnNav_Articulos);
        }

        // ─── Info usuario ─────────────────────────────────────────────────────
        public void ActualizarInfoUsuario()
        {
            var sql = SqlData.Instance;
            string nombres = sql.UsuariosObj.ObtenerItem("nombres", AppState.UsuarioActivo)?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(nombres)) nombres = AppState.UsuarioActivo;
            string sucursalDesc = sql.SucursalesObj.ObtenerItem("descripcion", AppState.SucursalActiva)?.ToString() ?? "";

            LblUsuario.Text  = $"Usuario: {nombres}  |  Período: {AppState.PeriodoActivo}";
            LblSucursal.Text = $"Sucursal: {sucursalDesc}";
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
                case "movimientos":  TabFijoContenido.Content = _panelMovimientos;  TabFijoTitulo.Text = "Movimientos";  break;
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

        // ─── Cerrar sesión ────────────────────────────────────────────────────

        private void BtnNav_Movimientos_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("movimientos");
            MarcarActivo(BtnNav_Movimientos);
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

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
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
