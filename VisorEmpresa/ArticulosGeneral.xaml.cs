using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SistemaGestion;
using SistemaGestion.Data;

namespace VisorEmpresa
{
    /// <summary>
    /// Duplicado de SistemaGestion.ArticulosGeneral para el visor: mismo catálogo
    /// (árbol de familias, búsqueda, grilla, métricas, Informe Excel), pero de
    /// SOLO LECTURA (sin Nuevo/Insertar/Editar/Eliminar). Disponible/Stock se
    /// calculan a nivel de EMPRESA vía ConsultasEmpresa.ObtenerStockEmpresa en
    /// vez de StockCalculator (que depende de AppState.SucursalActiva/
    /// AperturaActiva/DataFechaFinal, nunca poblados en VisorEmpresa — ver la
    /// nota de la clase en ConsultasEmpresa.cs). Combo de Sucursal (Todas las
    /// sucursales o una puntual, igual que PedidosGeneral): como
    /// ObtenerStockEmpresa ya devuelve el desglose por sucursal en el mismo
    /// resultado (PorSucursal), filtrar no requiere una consulta SQL aparte.
    /// </summary>
    public partial class ArticulosGeneral : UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        // Modo de filtro activo: "todos" (carga inicial) | "busqueda" (TxtBuscar) | "familia" (Tree1)
        private string _modoFiltro = "todos";
        private string _sucursalFiltro = "";   // "" = Todas las sucursales

        private bool _iniciado = false;
        private bool _cargandoFiltros;

        private class Opcion
        {
            public string Id    { get; }
            public string Texto { get; }
            public Opcion(string id, string texto) { Id = id; Texto = texto; }
            public override string ToString() => Texto;
        }

        public ArticulosGeneral()
        {
            InitializeComponent();
            Loaded += async (_, _) =>
            {
                if (_iniciado) return;
                _iniciado = true;
                await IniciarAsync();
            };
        }

        // ─── Carga inicial: combo de sucursales + primera consulta ───────────
        private async Task IniciarAsync()
        {
            _cargandoFiltros = true;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                string emp = AppState.EmpresaActiva;
                var sucursales = await Task.Run(() => ConsultasEmpresa.CargarSucursalesEmpresa(emp));
                var opciones = new List<Opcion> { new("", "Todas las sucursales") };
                opciones.AddRange(sucursales.Select(s => new Opcion(s.Id, s.Descripcion)));
                CmbSucursal.ItemsSource   = opciones;
                CmbSucursal.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar las sucursales:\n{ex.Message}",
                                "Visor Empresa", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                _cargandoFiltros = false;
            }

            CargarArbol();
            CargarArticulos();
        }

        private void Filtro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoFiltros || !_iniciado) return;
            _sucursalFiltro = (CmbSucursal.SelectedItem as Opcion)?.Id ?? "";
            CargarArticulos();
        }

        // ─── Árbol de productos/familias (mismo patrón que SistemaGestion.ArticulosGeneral) ──
        private void CargarArbol()
        {
            Tree1.Items.Clear();
            var nodoTodos = new TreeViewItem { Header = "Todos", Tag = "todos" };

            int ufProd = Sql.ProductosObj.ContarFilas;
            for (int i = 1; i <= ufProd; i++)
            {
                var idObj = Sql.ProductosObj.Mover(i);
                if (idObj == null) continue;
                string prodId   = idObj.ToString()!;
                string prodDesc = Sql.ProductosObj.ObtenerItem("descripcion", prodId)?.ToString() ?? prodId;

                var nodoProd = new TreeViewItem { Header = prodDesc, Tag = $"producto:{prodId}" };

                int ufFam = Sql.FamiliasObj.ContarFilas;
                for (int j = 1; j <= ufFam; j++)
                {
                    var famIdObj = Sql.FamiliasObj.Mover(j);
                    if (famIdObj == null) continue;
                    string famId = famIdObj.ToString()!;
                    if ((Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "") != prodId) continue;

                    string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? famId;
                    nodoProd.Items.Add(new TreeViewItem { Header = famDesc, Tag = $"familia:{famId}" });
                }

                if (nodoProd.Items.Count > 0) nodoProd.IsExpanded = true;
                nodoTodos.Items.Add(nodoProd);
            }

            Tree1.Items.Add(nodoTodos);
            nodoTodos.IsExpanded = true;
            nodoTodos.IsSelected = true;
        }

        private string ObtenerTagFiltro()
        {
            if (Tree1.SelectedItem is TreeViewItem item)
                return item.Tag?.ToString() ?? "todos";
            return "todos";
        }

        // ─── Carga la lista de artículos ──────────────────────────────────────
        public void CargarArticulos()
        {
            var resultado = ConsultasEmpresa.ObtenerStockEmpresa(AppState.EmpresaActiva);

            var lista = new List<ArticuloFila>();
            int linea = 1;

            string busqueda  = _modoFiltro == "busqueda" ? TxtBuscar.Text.Trim().ToLower() : "";
            string tagFiltro = _modoFiltro == "familia"  ? ObtenerTagFiltro()              : "";

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
                string catDesc   = ObtenerDescripcionCategoria(id);
                string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

                // Filtro de búsqueda (código, descripción completa o categoría)
                if (!string.IsNullOrEmpty(busqueda))
                {
                    if (!codigo.ToLower().Contains(busqueda) &&
                        !descCompleta.ToLower().Contains(busqueda) &&
                        !catDesc.ToLower().Contains(busqueda))
                        continue;
                }

                double disponible, stockVal;
                if (string.IsNullOrEmpty(_sucursalFiltro))
                {
                    resultado.Totales.TryGetValue(id, out var totales);
                    disponible = totales.Disponible;
                    stockVal   = totales.Stock;
                }
                else
                {
                    resultado.PorSucursal.TryGetValue(_sucursalFiltro + "|" + id, out var acumulado);
                    disponible = acumulado?.Disponible ?? 0;
                    stockVal   = acumulado?.Stock ?? 0;
                }

                lista.Add(new ArticuloFila
                {
                    Linea       = linea++,
                    Id          = id,
                    Codigo      = codigo,
                    Categoria   = catDesc,
                    Descripcion = descCompleta,
                    Disponible  = disponible,
                    Stock       = stockVal
                });
            }

            Grid1.ItemsSource = lista;
            ActualizarTotales(lista);
        }

        // Descripción de la categoría de un artículo (columna 'Categoria' del artículo).
        private static string ObtenerDescripcionCategoria(string id)
        {
            string catId = Sql.ArticulosObj.ObtenerItem("Categoria", id)?.ToString() ?? "";
            if (string.IsNullOrEmpty(catId)) return "";
            return Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "";
        }

        private void ActualizarTotales(List<ArticuloFila> lista)
        {
            double totalDisp = 0, totalStock = 0;
            foreach (var f in lista) { totalDisp += f.Disponible; totalStock += f.Stock; }
            TxtTotalArticulos.Text  = lista.Count.ToString("N0");
            TxtTotalDisponible.Text = totalDisp.ToString("N0");
            TxtTotalStock.Text      = totalStock.ToString("N0");
            LblSubtitulo.Text       = $"{lista.Count:N0} artículos";
        }

        // ─── Eventos árbol y búsqueda ─────────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _modoFiltro = "familia";
            CargarArticulos();
        }

        private void Tree1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Si el usuario hace clic en el item ya seleccionado, SelectedItemChanged no se dispara.
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null && source is not TreeViewItem)
                source = VisualTreeHelper.GetParent(source);

            if (source is TreeViewItem tvi && tvi.IsSelected)
            {
                _modoFiltro = "familia";
                CargarArticulos();
            }
        }

        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _modoFiltro = "busqueda";
            CargarArticulos();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            _modoFiltro = "busqueda";
            CargarArticulos();
        }

        // ─── Doble clic / Enter → ver artículo (solo lectura) ─────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AbrirVerDetalle();
        }

        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AbrirVerDetalle();
        }

        private void AbrirVerDetalle()
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;

            string titulo = $"Artículo {fila.Codigo}";
            var dlg = new ArticulosDetalle(fila.Id);
            dlg.Cerrando += () => consola.CerrarPestaña(dlg);
            consola.AbrirPestaña(titulo, dlg, $"articulo-{fila.Id}");
        }

        // ─── Informe Excel ──────────────────────────────────────────────────────
        private void BtnInformeExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InformeExcelArticulos { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
        }

        // ─── Actualizar ─────────────────────────────────────────────────────────
        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            string tagArbolSel = (Tree1.SelectedItem as TreeViewItem)?.Tag?.ToString() ?? "todos";
            string artSelId    = (Grid1.SelectedItem as ArticuloFila)?.Id ?? "";

            AppState.ActualizarProductos();
            ConsultasEmpresa.ObtenerStockEmpresa(AppState.EmpresaActiva, forzarRecarga: true);

            CargarArbol();
            RestaurarSeleccionArbol(tagArbolSel);
            CargarArticulos();
            RestaurarSeleccionGrid(artSelId);
        }

        private void RestaurarSeleccionArbol(string tag)
        {
            var nodo = BuscarNodoPorTag(Tree1.Items, tag) ?? BuscarNodoPorTag(Tree1.Items, "todos");
            if (nodo != null && !nodo.IsSelected) nodo.IsSelected = true;
        }

        private static TreeViewItem? BuscarNodoPorTag(ItemCollection items, string tag)
        {
            foreach (var obj in items)
            {
                if (obj is not TreeViewItem t) continue;
                if (t.Tag is string tg && tg == tag) return t;
                var hijo = BuscarNodoPorTag(t.Items, tag);
                if (hijo != null) return hijo;
            }
            return null;
        }

        private void RestaurarSeleccionGrid(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return;
            if (Grid1.ItemsSource is not List<ArticuloFila> lista) return;
            var item = lista.Find(x => x.Id == artId);
            if (item == null) return;
            Grid1.SelectedItem = item;
            Grid1.ScrollIntoView(item);
        }
    }

    // ─── Modelo ─────────────────────────────────────────────────────────────────
    public class ArticuloFila
    {
        public int    Linea       { get; set; }
        // Zebra de Grid1 calculado desde el dato (no desde AlternationIndex): ver
        // nota equivalente en SistemaGestion.ArticuloFila.
        public bool   FilaPar     => Linea % 2 == 0;
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Categoria   { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Disponible  { get; set; }
        public double Stock       { get; set; }
    }
}
