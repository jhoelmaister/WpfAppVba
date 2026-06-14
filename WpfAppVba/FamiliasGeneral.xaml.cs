using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class FamiliasGeneral : UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        public event Action? Cerrando;
        public void IntentarCerrar() => Cerrando?.Invoke();

        // Modo selector: cuando se abre desde un detalle para elegir una familia.
        private readonly Action<string>? _callbackSeleccion;
        public bool ModoSelector => _callbackSeleccion != null;

        private bool _iniciado = false;

        /// <summary>Constructor sin parámetros requerido por el compilador XAML y el panel del sidebar.</summary>
        public FamiliasGeneral() : this(null) { }

        public FamiliasGeneral(Action<string>? callbackSeleccion)
        {
            InitializeComponent();
            _callbackSeleccion = callbackSeleccion;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarFamilias(); ConfigurarModo(); };
        }

        /// <summary>Abre FamiliasGeneral como pestaña selector dentro de ConsolaMovimientos.</summary>
        public static void OpenAsTab(Window owner, Action<string>? callbackSeleccion = null,
                                     string contexto = "", UIElement? llamador = null)
        {
            var consola = owner as ConsolaMovimientos;
            if (consola == null) return;

            string titulo = string.IsNullOrEmpty(contexto) ? "Seleccionar Familia" : $"Seleccionar Familia ({contexto})";
            string clave  = $"seleccionar-familia|{contexto}";

            var ctrl = new FamiliasGeneral(callbackSeleccion);
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
        public void CargarFamilias()
        {
            if (TxtBuscar == null || Grid1 == null) return;

            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<FamiliaFila>();
            int linea = 1;

            int uf = Sql.FamiliasObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.FamiliasObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc = Sql.FamiliasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string codigo = Sql.FamiliasObj.ObtenerItem("codigo", id)?.ToString() ?? "";
                string productoId = Sql.FamiliasObj.ObtenerItem("producto", id)?.ToString() ?? "";
                string productoDesc = Sql.ProductosObj.ObtenerItem("descripcion", productoId)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    codigo.ToLower().Contains(busqueda) ||
                    productoDesc.ToLower().Contains(busqueda))
                {
                    filas.Add(new FamiliaFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Codigo      = codigo,
                        Descripcion = desc,
                        Producto    = productoDesc
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<FamiliaFila> FilasGrid =>
            Grid1.ItemsSource as List<FamiliaFila> ?? new List<FamiliaFila>();

        private FamiliaFila ConstruirFilaFamilia(string id, int linea)
        {
            string productoId   = Sql.FamiliasObj.ObtenerItem("producto",    id)?.ToString() ?? "";
            string productoDesc = Sql.ProductosObj.ObtenerItem("descripcion", productoId)?.ToString() ?? "";
            return new FamiliaFila
            {
                Linea       = linea,
                Id          = id,
                Codigo      = Sql.FamiliasObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                Descripcion = Sql.FamiliasObj.ObtenerItem("descripcion", id)?.ToString() ?? "",
                Producto    = productoDesc
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
            => CargarFamilias();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarFamilias();

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
            if (Grid1.SelectedItem is not FamiliaFila fila) return;
            _callbackSeleccion?.Invoke(fila.Codigo);
            Cerrando?.Invoke();
        }

        // ─── Botones ───────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioF = "nuevo";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = "Nueva Familia";
            var dlg = new FamiliasDetalle(this, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                if (dlg.ItemCreadoId == null) return;
                var nueva = ConstruirFilaFamilia(dlg.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                Renumerar();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña(titulo, dlg, "nueva-familia");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not FamiliaFila fila) return;

            var res = MessageBox.Show("¿Eliminar esta familia?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.FamiliasObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.FamiliasObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.FamiliasObj.Ocultar(fila.Id);
                Sql.FamiliasObj.OrdenarData(("id", false));

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
            Sql.FamiliasObj.Actualizar();
            CargarFamilias();
        }

        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not FamiliaFila fila) return;
            string idSel = fila.Id;
            int    linea = fila.Linea;
            AppState.EventoFormularioF = "modificar";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = $"Familia {fila.Codigo}";
            var dlg = new FamiliasDetalle(this, idSel, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFilaFamilia(idSel, linea);
                    lista[idx] = actualizada;
                    Renumerar();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña(titulo, dlg, $"familia-{idSel}");
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class FamiliaFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Producto    { get; set; } = "";
    }
}
