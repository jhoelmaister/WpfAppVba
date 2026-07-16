using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SistemaGestion;
using VisorEmpresa.Data;

namespace VisorEmpresa
{
    public partial class ProductosGeneral : UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        public event Action? Cerrando;
        public void IntentarCerrar() => Cerrando?.Invoke();

        // Modo selector: cuando se abre desde un detalle para elegir un producto.
        private readonly Action<string>? _callbackSeleccion;
        public bool ModoSelector => _callbackSeleccion != null;

        private bool _iniciado = false;

        /// <summary>Constructor sin parámetros requerido por el compilador XAML y el panel del sidebar.</summary>
        public ProductosGeneral() : this(null) { }

        public ProductosGeneral(Action<string>? callbackSeleccion)
        {
            InitializeComponent();
            _callbackSeleccion = callbackSeleccion;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarProductos(); ConfigurarModo(); };
        }

        /// <summary>Abre ProductosGeneral como pestaña selector dentro de ConsolaMovimientos.</summary>
        public static void OpenAsTab(Window owner, Action<string>? callbackSeleccion = null,
                                     string contexto = "", UIElement? llamador = null)
        {
            var consola = owner as ConsolaMovimientos;
            if (consola == null) return;

            string titulo = string.IsNullOrEmpty(contexto) ? "Seleccionar Producto" : $"Seleccionar Producto ({contexto})";
            string clave  = $"seleccionar-producto|{contexto}";

            var ctrl = new ProductosGeneral(callbackSeleccion);
            ctrl.Cerrando += () => { consola.CerrarPestaña(ctrl); consola.SeleccionarPestaña(llamador); };

            consola.CerrarPestañaPorClave(clave);
            consola.AbrirPestaña(titulo, ctrl, clave);
        }

        // ─── Configurar modo (selector vs normal) ─────────────────────────────
        private void ConfigurarModo()
        {
            bool ocultarCrud = ModoSelector || !AppState.EsAdmin;
            BtnSeleccionar.Visibility = ModoSelector   ? Visibility.Visible   : Visibility.Collapsed;
            BtnNuevo.Visibility       = ocultarCrud    ? Visibility.Collapsed : Visibility.Visible;
            BtnEditar.Visibility      = ocultarCrud    ? Visibility.Collapsed : Visibility.Visible;
            BtnEliminar.Visibility    = ocultarCrud    ? Visibility.Collapsed : Visibility.Visible;
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarProductos()
        {
            if (TxtBuscar == null || Grid1 == null) return;

            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<ProductoFila>();
            int linea = 1;

            int uf = Sql.ProductosObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.ProductosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc = Sql.ProductosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string codigo = Sql.ProductosObj.ObtenerItem("codigo", id)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    codigo.ToLower().Contains(busqueda))
                {
                    filas.Add(new ProductoFila
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
        private List<ProductoFila> FilasGrid =>
            Grid1.ItemsSource as List<ProductoFila> ?? new List<ProductoFila>();

        private ProductoFila ConstruirFila(string id, int linea)
        {
            return new ProductoFila
            {
                Linea       = linea,
                Id          = id,
                Codigo      = Sql.ProductosObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                Descripcion = Sql.ProductosObj.ObtenerItem("descripcion", id)?.ToString() ?? ""
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
            => CargarProductos();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarProductos();

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
            if (Grid1.SelectedItem is not ProductoFila fila) return;
            _callbackSeleccion?.Invoke(fila.Codigo);
            Cerrando?.Invoke();
        }

        // ─── Botones ───────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = "Nuevo Producto";
            var dlg = new ProductosDetalle(tituloTab: titulo);
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
            consola.AbrirPestaña(titulo, dlg, "nuevo-producto");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not ProductoFila fila) return;

            // Verificación de conexión en 2 capas antes de persistir el borrado.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            var res = MessageBox.Show("¿Eliminar este producto?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.ProductosObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.ProductosObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.ProductosObj.Ocultar(fila.Id);
                Sql.ProductosObj.OrdenarData(("id", false));

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
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            Sql.ProductosObj.Actualizar();
            CargarProductos();
        }

        private void AbrirEditar()
        {
            if (!AppState.EsAdmin) return;
            if (Grid1.SelectedItem is not ProductoFila fila) return;
            string idSel = fila.Id;
            int    linea = fila.Linea;
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = $"Producto {fila.Codigo}";
            var dlg = new ProductosDetalle(idSel, tituloTab: titulo);
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
            consola.AbrirPestaña(titulo, dlg, $"producto-{idSel}");
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class ProductoFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
