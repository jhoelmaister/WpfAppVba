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

        // Cada sección del menú lateral conserva su propio juego de pestañas dinámicas.
        private string _seccionActiva = "articulos";
        private readonly Dictionary<string, List<TabItem>> _pestañasPorSeccion = new()
        {
            ["articulos"]    = new List<TabItem>(),
            ["pedidos"]      = new List<TabItem>(),
            ["traspasos"]    = new List<TabItem>(),
            ["correcciones"] = new List<TabItem>(),
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
            LblUsuario.Text = $"Usuario: {AppState.UsuarioActivo}  |  Período: {AppState.PeriodoActivo}";
        }

        // ─── Navegación por pestañas ──────────────────────────────────────────
        private void MostrarPanel(string nombre)
        {
            if (nombre == _seccionActiva)
            {
                TabContenido.SelectedItem = TabFijo;
                return;
            }

            // 1. Guardar las pestañas dinámicas de la sección actual y quitarlas de la vista
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
            }

            // 3. Restaurar las pestañas propias de la nueva sección
            _seccionActiva = nombre;
            var restaurar = _pestañasPorSeccion[nombre];
            foreach (var t in restaurar)
                TabContenido.Items.Add(t);
            restaurar.Clear();

            TabContenido.SelectedItem = TabFijo;
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
