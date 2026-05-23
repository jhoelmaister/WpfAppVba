using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ArticulosGeneral : Window
    {
        private static SqlData Sql => SqlData.Instance;

        // Modo exportar: cuando se abre desde Traspasos/Inventarios/Pedidos (multi)
        private readonly Action<List<ArticuloExportado>>? _callbackExportar;
        // Modo single: buscar un solo artículo con doble clic (sin checkbox ni #)
        private readonly Action<ArticuloExportado>? _callbackSingle;
        // List en lugar de HashSet para conservar el orden de selección
        private readonly List<string> _seleccionados = new();

        public bool ModoExportar => _callbackExportar != null;
        public bool ModoSingle   => _callbackSingle   != null;

        public ArticulosGeneral(Action<List<ArticuloExportado>>? callbackExportar = null,
                                 Action<ArticuloExportado>?       callbackSingle   = null)
        {
            InitializeComponent();
            _callbackExportar = callbackExportar;
            _callbackSingle   = callbackSingle;
            Loaded += (_, _) => { CargarArbol(); CargarArticulos(); ConfigurarModo(); };
        }

        // ─── Configurar modo exportar ─────────────────────────────────────────
        private void ConfigurarModo()
        {
            PanelExportar.Visibility = ModoExportar ? Visibility.Visible : Visibility.Collapsed;

            // Columna checkbox (✓) y columna "#" solo visibles en modo importar
            var visibilidad = ModoExportar ? Visibility.Visible : Visibility.Collapsed;
            Grid1.Columns[0].Visibility = visibilidad;   // ✓ checkbox
            Grid1.Columns[1].Visibility = visibilidad;   // #  orden selección
        }

        // ─── Árbol de productos/familias ──────────────────────────────────────
        private void CargarArbol()
        {
            Tree1.Items.Clear();

            // Nodo raíz "Todos"
            var nodoTodos = new TreeViewItem { Header = "Todos", Tag = "todos" };

            // Productos
            int ufProd = Sql.ProductosObj.ContarFilas;
            for (int i = 1; i <= ufProd; i++)
            {
                var idObj = Sql.ProductosObj.Mover(i);
                if (idObj == null) continue;
                string prodId = idObj.ToString()!;
                string prodDesc = Sql.ProductosObj.ObtenerItem("descripcion", prodId)?.ToString() ?? prodId;

                var nodoProd = new TreeViewItem
                {
                    Header = prodDesc,
                    Tag    = $"producto:{prodId}"
                };

                // Familias de este producto
                int ufFam = Sql.FamiliasObj.ContarFilas;
                for (int j = 1; j <= ufFam; j++)
                {
                    var famIdObj = Sql.FamiliasObj.Mover(j);
                    if (famIdObj == null) continue;
                    string famId = famIdObj.ToString()!;
                    string famProd = Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "";
                    if (famProd != prodId) continue;

                    string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? famId;
                    var nodoFam = new TreeViewItem
                    {
                        Header = famDesc,
                        Tag    = $"familia:{famId}"
                    };
                    nodoProd.Items.Add(nodoFam);
                }

                nodoProd.IsExpanded = true;
                nodoTodos.Items.Add(nodoProd);
            }

            // Nodo "Sin Clasificar"
            var nodoSin = new TreeViewItem { Header = "Sin Clasificar", Tag = "sinclasificar" };
            var nodoSinFam = new TreeViewItem { Header = "Sin Familia", Tag = "familia:" };
            nodoSin.Items.Add(nodoSinFam);
            nodoSin.IsExpanded = true;
            nodoTodos.Items.Add(nodoSin);

            nodoTodos.IsExpanded = true;
            Tree1.Items.Add(nodoTodos);

            // Seleccionar primer nodo
            nodoTodos.IsSelected = true;
        }

        // ─── Carga la lista de artículos ──────────────────────────────────────
        public void CargarArticulos()
        {
            var lista = new List<ArticuloFila>();
            int linea = 1;
            double totalDisp = 0, totalStock = 0;

            string busqueda      = TxtBuscar.Text.Trim().ToLower();
            string tagFiltro     = ObtenerTagFiltro();

            int uf = Sql.ArticulosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string famId = Sql.ArticulosObj.ObtenerItem("familia", id)?.ToString() ?? "";

                // Filtro de árbol
                if (!string.IsNullOrEmpty(tagFiltro))
                {
                    if (tagFiltro.StartsWith("familia:"))
                    {
                        string famFiltro = tagFiltro.Substring("familia:".Length);
                        if (famId != famFiltro) continue;
                    }
                    else if (tagFiltro.StartsWith("producto:"))
                    {
                        string prodFiltro = tagFiltro.Substring("producto:".Length);
                        string famProd    = Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "";
                        if (famProd != prodFiltro) continue;
                    }
                    // "todos" o vacío → sin filtro
                }

                string codigo    = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc      = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo    = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
                string famDesc   = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
                string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

                // Filtro de búsqueda
                if (!string.IsNullOrEmpty(busqueda))
                {
                    if (!codigo.ToLower().Contains(busqueda) &&
                        !descCompleta.ToLower().Contains(busqueda))
                        continue;
                }

                double stock  = StockCalculator.ContarStock(id,  DateTime.Now);
                double stock2 = StockCalculator.ContarStock2(id, DateTime.Now);

                int ordenIdx = _seleccionados.IndexOf(id);   // -1 si no está

                lista.Add(new ArticuloFila
                {
                    Linea          = linea++,
                    Id             = id,
                    Codigo         = codigo,
                    Descripcion    = descCompleta,
                    Disponible     = stock2,
                    Stock          = stock,
                    Seleccionado   = ordenIdx >= 0,
                    OrdenSeleccion = ordenIdx >= 0 ? ordenIdx + 1 : 0
                });

                totalDisp  += stock2;
                totalStock += stock;
            }

            Grid1.ItemsSource      = lista;
            TxtTotalDisponible.Text = totalDisp.ToString("F0");
            TxtTotalStock.Text      = totalStock.ToString("F0");

            if (ModoExportar)
                LblSeleccionados.Text = $"Seleccionados: {_seleccionados.Count}";
        }

        // ─── Obtener filtro del árbol ─────────────────────────────────────────
        private string ObtenerTagFiltro()
        {
            if (Tree1.SelectedItem is TreeViewItem item)
                return item.Tag?.ToString() ?? "todos";
            return "todos";
        }

        // ─── Eventos árbol y búsqueda ─────────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
            => CargarArticulos();

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarArticulos();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarArticulos();

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;   // evita que eventos de ratón pendientes quiten el foco al cerrar el diálogo
            if (ModoSingle)
            {
                // Retorna el artículo seleccionado al llamador y cierra
                if (Grid1.SelectedItem is not ArticuloFila fila) return;
                _callbackSingle!(new ArticuloExportado
                {
                    Id          = fila.Id,
                    Codigo      = fila.Codigo,
                    Descripcion = fila.Descripcion
                });
                Close();
            }
            else if (ModoExportar)
                ToggleSeleccion();
            else
                AbrirEditar();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioA = "nuevo";
            var detalle = new ArticulosDetalle(this);
            detalle.ShowDialog();
            CargarArbol();
            CargarArticulos();
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;

            var res = MessageBox.Show("¿Eliminar este artículo?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.ArticulosObj.Ocultar(fila.Id);
                AppState.ActualizarStocks();
                Sql.ArticulosObj.OrdenarData(("familia", false), ("indice", false));
                CargarArticulos();
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            AppState.ActualizarProductos();
            CargarArbol();
            CargarArticulos();
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            if (_callbackExportar == null) return;

            var exportados = new List<ArticuloExportado>();
            foreach (string id in _seleccionados)
            {
                string codigo = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc   = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
                string famId  = Sql.ArticulosObj.ObtenerItem("familia",     id)?.ToString() ?? "";
                string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
                exportados.Add(new ArticuloExportado
                {
                    Id          = id,
                    Codigo      = codigo,
                    Descripcion = FuncionesComunes.UnirVariables(desc, famDesc, modelo)
                });
            }

            _callbackExportar(exportados);
            Close();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;
            string idSel = fila.Id;
            AppState.EventoFormularioA = "modificar";
            var detalle = new ArticulosDetalle(this, fila.Id);
            detalle.ShowDialog();
            CargarArticulos();
            // Restaurar selección y foco después de recargar la lista
            var item = (Grid1.ItemsSource as System.Collections.Generic.List<ArticuloFila>)
                       ?.Find(x => x.Id == idSel);
            if (item != null) { Grid1.SelectedItem = item; Grid1.ScrollIntoView(item); }
            Grid1.Focus();
        }

        private void ToggleSeleccion()
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;

            if (_seleccionados.Contains(fila.Id))
                _seleccionados.Remove(fila.Id);   // deselecciona y reordena el resto
            else
                _seleccionados.Add(fila.Id);      // agrega al final de la cola

            CargarArticulos();
        }
    }

    // ─── Modelos ──────────────────────────────────────────────────────────────
    public class ArticuloFila
    {
        public int    Linea          { get; set; }
        public string Id             { get; set; } = "";
        public string Codigo         { get; set; } = "";
        public string Descripcion    { get; set; } = "";
        public double Disponible     { get; set; }
        public double Stock          { get; set; }
        public bool   Seleccionado   { get; set; }
        public int    OrdenSeleccion { get; set; }         // 0 = no seleccionado
        public string OrdenStr       => OrdenSeleccion > 0 ? OrdenSeleccion.ToString() : "";
    }

    public class ArticuloExportado
    {
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
