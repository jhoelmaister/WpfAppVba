using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class IndustriasGeneral : UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        public event Action? Cerrando;
        public void IntentarCerrar() => Cerrando?.Invoke();

        // Modo selector: cuando se abre desde un detalle para elegir una industria.
        private readonly Action<string>? _callbackSeleccion;
        public bool ModoSelector => _callbackSeleccion != null;

        private bool _iniciado = false;

        /// <summary>Constructor sin parámetros requerido por el compilador XAML y el panel del sidebar.</summary>
        public IndustriasGeneral() : this(null) { }

        public IndustriasGeneral(Action<string>? callbackSeleccion)
        {
            InitializeComponent();
            _callbackSeleccion = callbackSeleccion;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarIndustrias(); ConfigurarModo(); };
        }

        /// <summary>Abre IndustriasGeneral como pestaña selector dentro de ConsolaMovimientos.</summary>
        public static void OpenAsTab(Window owner, Action<string>? callbackSeleccion = null,
                                     string contexto = "", UIElement? llamador = null)
        {
            var consola = owner as ConsolaMovimientos;
            if (consola == null) return;

            string titulo = string.IsNullOrEmpty(contexto) ? "Seleccionar Industria" : $"Seleccionar Industria ({contexto})";
            string clave  = $"seleccionar-industria|{contexto}";

            var ctrl = new IndustriasGeneral(callbackSeleccion);
            ctrl.Cerrando += () => { consola.CerrarPestaña(ctrl); consola.SeleccionarPestaña(llamador); };

            consola.CerrarPestañaPorClave(clave);
            consola.AbrirPestaña(titulo, ctrl, clave);
        }

        // ─── Configurar modo (selector vs normal) ─────────────────────────────
        private void ConfigurarModo()
        {
            BtnSeleccionar.Visibility = ModoSelector             ? Visibility.Visible   : Visibility.Collapsed;
            BtnNuevo.Visibility       = ModoSelector             ? Visibility.Collapsed : Visibility.Visible;
            BtnEditar.Visibility      = ModoSelector             ? Visibility.Collapsed : Visibility.Visible;
            BtnEliminar.Visibility    = (ModoSelector || !AppState.EsAdmin) ? Visibility.Collapsed : Visibility.Visible;
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarIndustrias()
        {
            if (TxtBuscar == null || Grid1 == null) return;

            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<IndustriaFila>();
            int linea = 1;

            int uf = Sql.IndustriasObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.IndustriasObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc = Sql.IndustriasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string codigo = Sql.IndustriasObj.ObtenerItem("codigo", id)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    codigo.ToLower().Contains(busqueda))
                {
                    filas.Add(new IndustriaFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Codigo      = codigo,
                        Descripcion = desc
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<IndustriaFila> FilasGrid =>
            Grid1.ItemsSource as List<IndustriaFila> ?? new List<IndustriaFila>();

        private IndustriaFila ConstruirFila(string id, int linea)
        {
            return new IndustriaFila
            {
                Linea       = linea,
                Id          = id,
                Codigo      = Sql.IndustriasObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                Descripcion = Sql.IndustriasObj.ObtenerItem("descripcion", id)?.ToString() ?? ""
            };
        }

        private void Renumerar()
        {
            int n = 1;
            foreach (var f in FilasGrid) f.Linea = n++;
            Grid1.Items.Refresh();
        }

        // ─── Búsqueda ─────────────────────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarIndustrias();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarIndustrias();

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (ModoSelector) Seleccionar();
            else              AbrirEditar();
        }

        // ─── Tecla Enter ──────────────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            if (ModoSelector) Seleccionar();
            else              AbrirEditar();
        }

        // ─── Modo selector ─────────────────────────────────────────────────────
        private void BtnSeleccionar_Click(object sender, RoutedEventArgs e)
            => Seleccionar();

        private void Seleccionar()
        {
            if (Grid1.SelectedItem is not IndustriaFila fila) return;
            _callbackSeleccion?.Invoke(fila.Codigo);
            Cerrando?.Invoke();
        }

        // ─── Botones ───────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = "Nueva Industria";
            var dlg = new IndustriasDetalle(tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                if (dlg.ItemCreadoId == null) return;
                var nueva = ConstruirFila(dlg.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                Renumerar();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña(titulo, dlg, "nueva-industria");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not IndustriaFila fila) return;

            var res = MessageBox.Show("¿Eliminar esta industria?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.IndustriasObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.IndustriasObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.IndustriasObj.Ocultar(fila.Id);
                Sql.IndustriasObj.OrdenarData(("id", false));

                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0) lista.RemoveAt(idx);
                Renumerar();

                if (lista.Count > 0)
                {
                    var sel = lista[Math.Min(idx, lista.Count - 1)];
                    Grid1.SelectedItem = sel; Grid1.ScrollIntoView(sel);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            Sql.IndustriasObj.Actualizar();
            CargarIndustrias();
        }

        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not IndustriaFila fila) return;
            string idSel = fila.Id;
            int    linea = fila.Linea;
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = $"Industria {fila.Codigo}";
            var dlg = new IndustriasDetalle(idSel, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFila(idSel, linea);
                    lista[idx] = actualizada;
                    Renumerar();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña(titulo, dlg, $"industria-{idSel}");
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class IndustriaFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
