using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VisorEmpresa;
using WpfAppVba.Data;

namespace WpfAppVba
{
    /// <summary>
    /// Consola del VISOR (ConsolaVisor.xaml). Declara la clase
    /// WpfAppVba.ConsolaMovimientos a propósito — ver la nota en el XAML: los
    /// formularios vinculados de la app principal buscan esa clase para abrir sus
    /// pestañas (AbrirPestaña / CerrarPestaña / SeleccionarPestaña /
    /// CerrarPestañaPorClave / ConfirmarCierrePestañasRelacionadas), y al
    /// compilarla aquí (la consola original NO se vincula) funcionan sin cambios.
    ///
    /// Secciones: Dashboard + documentos en solo-visualización (Pedidos,
    /// Traspasos, Correcciones, Facturas — paneles propios del visor, a nivel
    /// empresa) + catálogos con edición completa (Precios, Empresas, Sucursales,
    /// Usuarios — formularios vinculados de la app principal).
    /// </summary>
    public partial class ConsolaMovimientos : Window
    {
        private Button? _btnActivo;

        // Paneles fijos por sección.
        private readonly DashboardVisor    _panelDashboard    = new();
        private readonly PedidosVisor      _panelPedidos      = new();
        private readonly TraspasosVisor    _panelTraspasos    = new();
        private readonly CorreccionesVisor _panelCorrecciones = new();
        private readonly FacturasVisor     _panelFacturas     = new();
        private readonly PreciosGeneral    _panelPrecios      = new();
        private readonly EmpresasGeneral   _panelEmpresas     = new();
        private readonly SucursalesGeneral _panelSucursales   = new();
        private readonly UsuariosGeneral   _panelUsuarios     = new();

        // Cada sección del menú lateral conserva su propio juego de pestañas dinámicas.
        private string _seccionActiva = "dashboard";
        private readonly Dictionary<string, List<TabItem>> _pestañasPorSeccion = new()
        {
            ["dashboard"]    = new List<TabItem>(),
            ["pedidos"]      = new List<TabItem>(),
            ["traspasos"]    = new List<TabItem>(),
            ["correcciones"] = new List<TabItem>(),
            ["facturas"]     = new List<TabItem>(),
            ["precios"]      = new List<TabItem>(),
            ["empresas"]     = new List<TabItem>(),
            ["sucursales"]   = new List<TabItem>(),
            ["usuarios"]     = new List<TabItem>(),
        };
        private readonly Dictionary<string, TabItem?> _pestañaSeleccionadaPorSeccion = new()
        {
            ["dashboard"]    = null,
            ["pedidos"]      = null,
            ["traspasos"]    = null,
            ["correcciones"] = null,
            ["facturas"]     = null,
            ["precios"]      = null,
            ["empresas"]     = null,
            ["sucursales"]   = null,
            ["usuarios"]     = null,
        };

        public ConsolaMovimientos()
        {
            InitializeComponent();
            TabFijoContenido.Content = _panelDashboard;
            MostrarVersion();
            ActualizarInfoUsuario();
            ActualizarIconoTema();
            MarcarActivo(BtnNav_Dashboard);

            // Estado de conexión: pintar el estado actual y escuchar cambios.
            ActualizarLabelConexion(ConexionEstado.EnLinea);
            ConexionEstado.Cambio += OnConexionCambio;
            ConexionEstado.Iniciar(Dispatcher);
        }

        // ─── Versión de la app (barra de título de la ventana) ────────────────
        private void MostrarVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            Title = v == null
                ? "Visor Empresa"
                : $"Visor Empresa  v{v.Major}.{v.Minor}.{v.Build}";
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
            string empresaDesc = sql.EmpresasObj.ObtenerItem("descripcion", AppState.EmpresaActiva)?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(empresaDesc)) empresaDesc = "(todas)";

            LblUsuario.Text = $"Usuario: {nombres}";
            LblEmpresa.Text = $"Empresa: {empresaDesc}";
        }

        // ─── Tema claro / oscuro ──────────────────────────────────────────────
        private void BtnTema_Click(object sender, RoutedEventArgs e)
        {
            string nuevo = TemaVisor.EsOscuroActivo ? TemaVisor.TemaClaro : TemaVisor.TemaOscuro;
            TemaVisor.AplicarTema(nuevo);
            AppState.TemaActivo = nuevo;   // por si algún formulario vinculado lo consulta
            ActualizarIconoTema();
            // Los gráficos del dashboard resuelven sus brushes al dibujar: re-render.
            _panelDashboard.RefrescarTema();
        }

        private void ActualizarIconoTema()
        {
            BtnTema.Content = TemaVisor.EsOscuroActivo ? "☀" : "🌙";
        }

        // ─── Navegación por pestañas (mismo mecanismo que la app principal) ───
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
                case "dashboard":    TabFijoContenido.Content = _panelDashboard;    TabFijoTitulo.Text = "Dashboard";    break;
                case "pedidos":      TabFijoContenido.Content = _panelPedidos;      TabFijoTitulo.Text = "Pedidos";      break;
                case "traspasos":    TabFijoContenido.Content = _panelTraspasos;    TabFijoTitulo.Text = "Traspasos";    break;
                case "correcciones": TabFijoContenido.Content = _panelCorrecciones; TabFijoTitulo.Text = "Correcciones"; break;
                case "facturas":     TabFijoContenido.Content = _panelFacturas;     TabFijoTitulo.Text = "Facturas";     break;
                case "precios":      TabFijoContenido.Content = _panelPrecios;      TabFijoTitulo.Text = "Precios";      break;
                case "empresas":     TabFijoContenido.Content = _panelEmpresas;     TabFijoTitulo.Text = "Empresas";     break;
                case "sucursales":   TabFijoContenido.Content = _panelSucursales;   TabFijoTitulo.Text = "Sucursales";   break;
                case "usuarios":     TabFijoContenido.Content = _panelUsuarios;     TabFijoTitulo.Text = "Usuarios";     break;
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

        // ─── Verificar y cerrar pestañas vinculadas antes de guardar/cerrar ─────
        // Devuelve true si se puede continuar (no hay pestañas o el usuario aceptó cerrarlas).
        public bool ConfirmarCierrePestañasRelacionadas(string contexto)
        {
            if (string.IsNullOrEmpty(contexto)) return true;
            var relacionadas = new List<TabItem>();
            foreach (TabItem t in TabContenido.Items)
                if (t.Tag is string clave && clave.EndsWith($"|{contexto}"))
                    relacionadas.Add(t);
            if (relacionadas.Count == 0) return true;

            string lista = string.Join("\n• ", relacionadas.Select(t =>
                t.Header is StackPanel sp
                    ? sp.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? "pestaña"
                    : t.Tag?.ToString() ?? "pestaña"));

            var res = MessageBox.Show(
                $"Tiene pestaña(s) vinculada(s) aún abierta(s):\n• {lista}\n\nAceptar: cerrarlas y continuar.\nCancelar: volver sin cerrar nada.",
                "Pestañas relacionadas abiertas",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.OK) return false;
            foreach (var t in relacionadas)
                TabContenido.Items.Remove(t);
            return true;
        }

        // ─── Resaltar ítem activo en la barra lateral ─────────────────────────
        private void MarcarActivo(Button btn)
        {
            if (_btnActivo != null)
            {
                _btnActivo.Background  = Brushes.Transparent;
                _btnActivo.BorderBrush = Brushes.Transparent;
                _btnActivo.SetResourceReference(Control.ForegroundProperty, "ThemeTextoSec");
            }
            _btnActivo = btn;
            // SetResourceReference (en vez de asignar un Brush fijo) para que el
            // resaltado del ítem activo siga el tema actual incluso si el usuario
            // cambia de tema sin volver a hacer clic en el ítem.
            btn.SetResourceReference(Control.BackgroundProperty, "ThemeNavActivoBg");
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x6F, 0xE3));
            btn.SetResourceReference(Control.ForegroundProperty, "ThemeNavActivoFg");
        }

        // ─── Navegación lateral ───────────────────────────────────────────────

        private void BtnNav_Dashboard_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("dashboard");
            MarcarActivo(BtnNav_Dashboard);
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

        private void BtnNav_Facturas_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("facturas");
            MarcarActivo(BtnNav_Facturas);
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

        private void BtnNav_Sucursales_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("sucursales");
            MarcarActivo(BtnNav_Sucursales);
        }

        private void BtnNav_Usuarios_Click(object sender, RoutedEventArgs e)
        {
            MostrarPanel("usuarios");
            MarcarActivo(BtnNav_Usuarios);
        }

        // ─── Cambios sin guardar (mismo mecanismo que la app principal) ───────

        // Marcado cuando el cierre ya fue confirmado (p. ej. desde Cerrar sesión) para
        // que el evento Closing no vuelva a preguntar.
        private bool _cierreConfirmado = false;

        // Devuelve los títulos de las pestañas (nuevo/editar) con cambios sin guardar,
        // tanto en la sección actual como en las demás secciones.
        private List<string> PestañasConCambios()
        {
            var res = new List<string>();
            foreach (TabItem t in TabContenido.Items)
                if (t != TabFijo && TieneCambiosSinGuardar(t.Content)) res.Add(TituloPestaña(t));
            foreach (var lista in _pestañasPorSeccion.Values)
                foreach (var t in lista)
                    if (TieneCambiosSinGuardar(t.Content)) res.Add(TituloPestaña(t));
            return res;
        }

        // Extrae el texto del título de una pestaña (el Header es un StackPanel con un
        // TextBlock de título seguido del botón de cierre).
        private static string TituloPestaña(TabItem t)
        {
            if (t.Header is StackPanel sp)
                foreach (var hijo in sp.Children)
                    if (hijo is TextBlock tb) return tb.Text;
            return t.Header?.ToString() ?? "(pestaña)";
        }

        // Lee por reflexión el estado de cambios del detalle: la propiedad "HayCambios"
        // o el campo "_hayCambios". Los paneles que no tengan ninguno cuentan como
        // "sin cambios".
        private static bool TieneCambiosSinGuardar(object? content)
        {
            if (content == null) return false;
            var tipo = content.GetType();

            var prop = tipo.GetProperty("HayCambios",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(bool))
                return prop.GetValue(content) is bool pb && pb;

            var field = tipo.GetField("_hayCambios",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
                return field.GetValue(content) is bool fb && fb;

            return false;
        }

        // Pide confirmación si hay cambios sin guardar, indicando EXACTAMENTE en qué
        // pestañas. Devuelve true si se puede cerrar.
        public bool ConfirmarPerderCambios()
        {
            var conCambios = PestañasConCambios();
            if (conCambios.Count == 0) return true;

            string detalle = string.Join("\n", conCambios.ConvertAll(t => "   •  " + t));
            var res = MessageBox.Show(
                "Hay cambios sin guardar en:\n\n" + detalle +
                "\n\nSi cierras se perderán esos cambios.\n¿Seguro que deseas cerrar?",
                "Cerrar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return res == MessageBoxResult.Yes;
        }

        private void ConsolaVisor_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_cierreConfirmado && !ConfirmarPerderCambios())
            {
                e.Cancel = true;   // el usuario decidió no cerrar
                return;
            }
            ConexionEstado.Cambio -= OnConexionCambio;
        }

        private void BtnCerrarSesion_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmarPerderCambios()) return;

            _cierreConfirmado = true;   // ya confirmado: Closing no vuelve a preguntar

            AppState.SesionActiva  = false;
            AppState.UsuarioActivo = "";
            AppState.EmpresaActiva = "";
            VisorState.UsuarioActivo = "";
            VisorState.EmpresaActiva = "";
            DatabaseConnection.CerrarConexion();

            var login = new LoginVisorWindow();
            login.Show();
            Close();
        }
    }
}
