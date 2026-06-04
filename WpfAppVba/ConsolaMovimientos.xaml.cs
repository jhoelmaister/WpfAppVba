using System;
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

        private PedidosGeneral   TabPedidos   => _panelPedidos;
        private TraspasosGeneral TabTraspasos => _panelTraspasos;

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
            LblUsuario.Text = $"Usuario: {AppState.UsuarioActivo}  |  Período: {AppState.PeriodoActivo}";
        }

        // ─── Navegación por pestañas ──────────────────────────────────────────
        private void MostrarPanel(string nombre)
        {
            switch (nombre)
            {
                case "articulos":    TabFijoContenido.Content = _panelArticulos;    TabFijoTitulo.Text = "Artículos";    break;
                case "pedidos":      TabFijoContenido.Content = _panelPedidos;      TabFijoTitulo.Text = "Pedidos";      break;
                case "traspasos":    TabFijoContenido.Content = _panelTraspasos;    TabFijoTitulo.Text = "Traspasos";    break;
                case "correcciones": TabFijoContenido.Content = _panelCorrecciones; TabFijoTitulo.Text = "Correcciones"; break;
            }
            TabContenido.SelectedItem = TabFijo;
        }

        public void AbrirPestaña(string titulo, UIElement contenido)
        {
            foreach (TabItem t in TabContenido.Items)
                if (t.Content == contenido) { TabContenido.SelectedItem = t; return; }

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

            var tab = new TabItem { Header = header, Content = contenido };
            btnCerrar.Click += (s, e) => { e.Handled = true; CerrarPestaña(contenido); };

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

        private void BtnNav_Inventarios_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title                 = "Inventarios",
                Width                 = 520,
                Height                = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = this,
                ShowInTaskbar         = false,
                Background            = Brushes.Transparent,
                Content               = new InventariosGeneral()
            };
            WindowHelper.AjustarAlEcran(win);
            win.ShowDialog();
        }

        private void BtnNav_Precios_Click(object sender, RoutedEventArgs e)
        {
            new PreciosGeneral { Owner = this }.ShowDialog();
        }

        private void BtnNav_Configuracion_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title                 = "Configuración",
                Width                 = 480,
                Height                = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = this,
                ShowInTaskbar         = false,
                ResizeMode            = ResizeMode.NoResize,
                Background            = Brushes.Transparent,
                Content               = new Configuracion()
            };
            WindowHelper.AjustarAlEcran(win);
            win.ShowDialog();
            ActualizarInfoUsuario();
        }

        // ─── Acciones rápidas ─────────────────────────────────────────────────

        private void BtnVentaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "venta";
            AppState.TipoPedido        = "rapido";
            var dlg = new PedidosDetalle();
            dlg.Cerrando += () => { CerrarPestaña(dlg); TabPedidos.CargarPedidos(); };
            AbrirPestaña("Venta Rápida", dlg);
        }

        private void BtnEntradaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "entrada";
            new TraspasosDetalle { Owner = this }.ShowDialog();
            TabTraspasos.CargarTraspasos();
        }

        private void BtnSalidaRapida_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoMovimiento    = "salida";
            new TraspasosDetalle { Owner = this }.ShowDialog();
            TabTraspasos.CargarTraspasos();
        }

        // ─── Cerrar sesión ────────────────────────────────────────────────────

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
