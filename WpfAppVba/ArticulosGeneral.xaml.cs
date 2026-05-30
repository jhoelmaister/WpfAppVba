using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ArticulosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        // Modo exportar: cuando se abre desde Traspasos/Inventarios/Pedidos (multi)
        private readonly Action<List<ArticuloExportado>>? _callbackExportar;
        // Modo single: buscar un solo artículo con doble clic (sin checkbox ni #)
        private readonly Action<ArticuloExportado>? _callbackSingle;
        // List en lugar de HashSet para conservar el orden de selección
        private readonly List<string> _seleccionados = new();

        // Modo de filtro activo: "todos" (carga inicial) | "busqueda" (TxtBuscar) | "familia" (Tree1)
        private string _modoFiltro = "todos";

        public bool ModoExportar => _callbackExportar != null;
        public bool ModoSingle   => _callbackSingle   != null;

        /// <summary>Constructor sin parámetros requerido por el compilador XAML.</summary>
        public ArticulosGeneral() : this(null, null) { }

        public ArticulosGeneral(Action<List<ArticuloExportado>>? callbackExportar = null,
                                 Action<ArticuloExportado>?       callbackSingle   = null)
        {
            InitializeComponent();
            _callbackExportar = callbackExportar;
            _callbackSingle   = callbackSingle;
            Loaded += (_, _) => { CargarArbol(); CargarArticulos(); ConfigurarModo(); };
        }

        /// <summary>Abre ArticulosGeneral como diálogo modal dentro de una ventana temporal.</summary>
        public static void OpenAsDialog(Window owner,
                                        Action<List<ArticuloExportado>>? callbackExportar = null,
                                        Action<ArticuloExportado>?        callbackSingle   = null)
        {
            var ctrl = new ArticulosGeneral(callbackExportar, callbackSingle);
            var win  = new Window
            {
                Content               = ctrl,
                Title                 = "Artículos",
                Width                 = 900,
                Height                = 560,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = owner,
                ShowInTaskbar         = false,
                Background            = System.Windows.Media.Brushes.WhiteSmoke,
                ResizeMode            = ResizeMode.CanResize
            };
            WindowHelper.AjustarAlEcran(win);
            win.ShowDialog();
        }

        // ─── Configurar modo exportar ─────────────────────────────────────────
        private void ConfigurarModo()
        {
            bool esDialog = ModoExportar || ModoSingle;

            PanelExportar.Visibility  = ModoExportar ? Visibility.Visible  : Visibility.Collapsed;
            BtnInformeExcel.Visibility = esDialog     ? Visibility.Collapsed : Visibility.Visible;

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
            var nodoSin = new TreeViewItem { Header = "Sin Clasificar", Tag = "familia:" };
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

            // Filtros excluyentes: solo se aplica el del modo activo
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
            TxtTotalDisponible.Text = totalDisp.ToString("N0");
            TxtTotalStock.Text      = totalStock.ToString("N0");

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

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<ArticuloFila> FilasGrid =>
            Grid1.ItemsSource as List<ArticuloFila> ?? new List<ArticuloFila>();

        // Construye una sola fila para un artículo (incluye cálculo de stock).
        private ArticuloFila ConstruirFila(string id, int linea)
        {
            string famId  = Sql.ArticulosObj.ObtenerItem("familia",     id)?.ToString() ?? "";
            string codigo = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
            string desc   = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
            string modelo = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
            string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

            double stock  = StockCalculator.ContarStock(id,  DateTime.Now);
            double stock2 = StockCalculator.ContarStock2(id, DateTime.Now);
            int ordenIdx  = _seleccionados.IndexOf(id);

            return new ArticuloFila
            {
                Linea          = linea,
                Id             = id,
                Codigo         = codigo,
                Descripcion    = descCompleta,
                Disponible     = stock2,
                Stock          = stock,
                Seleccionado   = ordenIdx >= 0,
                OrdenSeleccion = ordenIdx >= 0 ? ordenIdx + 1 : 0
            };
        }

        // Renumera la columna Línea, recalcula totales desde el grid y refresca.
        private void RenumerarYActualizarTotales()
        {
            var lista = FilasGrid;
            int n = 1;
            double totalDisp = 0, totalStock = 0;
            foreach (var f in lista)
            {
                f.Linea     = n++;
                totalDisp  += f.Disponible;
                totalStock += f.Stock;
            }
            TxtTotalDisponible.Text = totalDisp.ToString("N0");
            TxtTotalStock.Text      = totalStock.ToString("N0");
            if (ModoExportar)
                LblSeleccionados.Text = $"Seleccionados: {_seleccionados.Count}";
            Grid1.Items.Refresh();
        }

        private void EnfocarFila(ArticuloFila item)
        {
            Grid1.SelectedItem = item;
            Grid1.ScrollIntoView(item);
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
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
            // Detectamos ese caso y recargamos manualmente.
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

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;   // evita que eventos de ratón pendientes quiten el foco al cerrar el diálogo
            EjecutarAccionFila();
        }

        // ─── Tecla Enter ──────────────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            EjecutarAccionFila();
        }

        private void EjecutarAccionFila()
        {
            if (ModoSingle)
            {
                if (Grid1.SelectedItem is not ArticuloFila fila) return;
                _callbackSingle!(new ArticuloExportado
                {
                    Id          = fila.Id,
                    Codigo      = fila.Codigo,
                    Descripcion = fila.Descripcion
                });
                CerrarDialogo();
            }
            else if (ModoExportar)
                ToggleSeleccion();
            else
                AbrirEditar();
        }

        // Cierra el diálogo padre (solo si no estamos embebidos en ConsolaMovimientos)
        private void CerrarDialogo()
        {
            var w = Window.GetWindow(this);
            if (w is not ConsolaMovimientos) w?.Close();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioA = "nuevo";
            var detalle = new ArticulosDetalle(this) { Owner = Window.GetWindow(this) };
            detalle.ShowDialog();
            if (detalle.ItemCreadoId == null) return;   // cancelado

            var nueva = ConstruirFila(detalle.ItemCreadoId, 0);
            FilasGrid.Add(nueva);
            RenumerarYActualizarTotales();
            EnfocarFila(nueva);
        }

        private void BtnInsertar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;
            AppState.EventoFormularioA = "insertar";
            var detalle = new ArticulosDetalle(this, fila.Id) { Owner = Window.GetWindow(this) };
            detalle.ShowDialog();
            if (detalle.ItemCreadoId == null) return;   // cancelado

            var lista = FilasGrid;
            int idx   = lista.IndexOf(fila);
            var nueva = ConstruirFila(detalle.ItemCreadoId, 0);
            if (idx >= 0) lista.Insert(idx + 1, nueva); else lista.Add(nueva);
            RenumerarYActualizarTotales();
            EnfocarFila(nueva);
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;

            var res = MessageBox.Show("¿Eliminar este artículo?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            // Capturar familia e indice antes de ocultar
            string famEliminada = Sql.ArticulosObj.ObtenerItem("familia", fila.Id)?.ToString() ?? "";
            int    indEliminado = Convert.ToInt32(Sql.ArticulosObj.ObtenerItem("indice", fila.Id) ?? 0);

            Sql.ArticulosObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
            Sql.ArticulosObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
            Sql.ArticulosObj.Ocultar(fila.Id);

            // Rellenar el hueco: restar 1 a los índices > indEliminado dentro de la misma familia
            int uf = Sql.ArticulosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string fam = Sql.ArticulosObj.ObtenerItem("familia", id)?.ToString() ?? "";
                if (fam != famEliminada) continue;

                int ind = Convert.ToInt32(Sql.ArticulosObj.ObtenerItem("indice", id) ?? 0);
                if (ind > indEliminado)
                    Sql.ArticulosObj.EstablecerItem("indice", id, ind - 1);
            }

            AppState.ActualizarStocks();
            Sql.ArticulosObj.OrdenarData(("familia", false), ("indice", false));

            // Quitar solo la fila eliminada del grid (sin recargar todo)
            _seleccionados.Remove(fila.Id);
            var lista = FilasGrid;
            int idx   = lista.IndexOf(fila);
            if (idx >= 0) lista.RemoveAt(idx);
            RenumerarYActualizarTotales();

            if (lista.Count > 0)
                EnfocarFila(lista[Math.Min(idx, lista.Count - 1)]);
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            AppState.ActualizarProductos();
            CargarArbol();
            CargarArticulos();
        }

        private void BtnInformeExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InformeExcelArticulos
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
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
            CerrarDialogo();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;
            string idSel = fila.Id;
            AppState.EventoFormularioA = "modificar";
            var detalle = new ArticulosDetalle(this, fila.Id) { Owner = Window.GetWindow(this) };
            detalle.ShowDialog();

            // Reconstruir solo esta fila en su lugar (el Id interno no cambia)
            var lista = FilasGrid;
            int idx   = lista.IndexOf(fila);
            if (idx >= 0)
            {
                var actualizada = ConstruirFila(idSel, fila.Linea);
                lista[idx] = actualizada;
                RenumerarYActualizarTotales();
                EnfocarFila(actualizada);
            }
        }

        private void ToggleSeleccion()
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;

            string idActual = fila.Id;   // guardar antes de recargar

            if (_seleccionados.Contains(idActual))
                _seleccionados.Remove(idActual);
            else
                _seleccionados.Add(idActual);

            CargarArticulos();

            // Restaurar selección y foco al mismo ítem
            var item = (Grid1.ItemsSource as System.Collections.Generic.List<ArticuloFila>)
                       ?.Find(x => x.Id == idActual);
            if (item != null) { Grid1.SelectedItem = item; Grid1.ScrollIntoView(item); }
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
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
