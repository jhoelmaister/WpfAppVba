using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class InventariosDetalle : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly InventariosGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios   = false;
        private bool _cargando     = true;
        private bool _iniciado     = false;
        // Solo la apertura más reciente (AppState.AperturaIdActiva) se puede editar/guardar;
        // las anteriores se abren igual, pero en modo lectura (ver InventariosGeneral.AbrirEditar).
        private bool _soloLectura  = false;
        private string _tituloTab = "";
        private string _codigoDocI = "";
        private List<InventarioItemFila> _items = new();

        // Modo de filtro activo: "todos" (carga inicial) | "busqueda" (TxtBuscar) | "familia" (Tree1)
        private string _modoFiltro = "todos";

        public event Action? Cerrando;

        /// <summary>ID del documento de inventario recién creado.</summary>
        public string? ItemCreadoId { get; private set; }

        public InventariosDetalle(InventariosGeneral? padre = null, string idEditar = "")
        {
            InitializeComponent();
            _padre     = padre;
            _idEditar  = idEditar;
            _tituloTab = string.IsNullOrEmpty(idEditar) ? "nuevo-inventario" : $"inventario-{idEditar}";
            Loaded    += (_, _) => { if (_iniciado) return; _iniciado = true; CargarUserform(); };
        }

        // ─── Al cerrar: preguntar si hay cambios ──────────────────────────────
        public void IntentarCerrar()
        {
            // Confirma cualquier celda en edición antes de chequear cambios pendientes
            Grid1.CommitEdit(DataGridEditingUnit.Row, true);

            if (!_hayCambios) { Cerrando?.Invoke(); return; }

            var res = MessageBox.Show("¿Guardar Cambios?", "Consola",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes && Guardar()) Cerrando?.Invoke();
            else if (res == MessageBoxResult.No)          Cerrando?.Invoke();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            if (AppState.EventoFormularioI == "editar")
            {
                LblTitulo.Text = "Editar Inventario";
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text = "Nuevo Inventario";
                CargarParaNuevo();
            }

            LblDocNum.Text = Box_DocumentoI.Text;
            _cargando   = false;
            _hayCambios = false;
        }

        private void CargarParaEditar()
        {
            Box_DocumentoI.IsEnabled = false;
            string codigoDocEdit = Sql.DocumentosIObj.ObtenerItem("codigo", _idEditar)?.ToString() ?? "";
            Box_DocumentoI.Text = codigoDocEdit;

            var fechaObj = Sql.DocumentosIObj.ObtenerItem("fecha", _idEditar);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : DateTime.Now;
            Box_Fecha.SelectedDate = fecha;
            Box_Hora.Text          = fecha.ToString("HH:mm:ss");
            Box_Observacion.Text   = Sql.DocumentosIObj.ObtenerItem("observacion", _idEditar)?.ToString() ?? "";
            Box_Referencia.Text    = Sql.DocumentosIObj.ObtenerItem("referencia",  _idEditar)?.ToString() ?? "";

            // Catálogo completo de artículos, con la cantidad existente (si la hay) de este inventario.
            CargarItems(_idEditar);
            CargarArbol();
            RefrescarGrid();

            _soloLectura = _idEditar != AppState.AperturaIdActiva;
            if (_soloLectura) AplicarModoSoloLectura();
        }

        // ─── Modo lectura: aperturas anteriores a la activa se pueden ver, no editar ──
        private void AplicarModoSoloLectura()
        {
            LblTitulo.Text            = "Ver Inventario (solo lectura)";
            Box_Fecha.IsEnabled       = false;
            Box_Hora.IsEnabled        = false;
            Box_Referencia.IsEnabled  = false;
            Box_Observacion.IsEnabled = false;
            Grid1.IsReadOnly          = true;
            BtnGuardar.IsEnabled      = false;
        }

        private void CargarParaNuevo()
        {
            Box_DocumentoI.IsEnabled = false;
            string signo  = Sql.SucursalesObj.ObtenerItem("signo", AppState.SucursalActiva)?.ToString() ?? "";
            int    numero = Sql.DocumentosIObj.SiguienteNumeroDoc(signo, "sucursal", AppState.SucursalActiva);
            _codigoDocI          = $"{signo.ToUpper()}{numero}";
            Box_DocumentoI.Text  = _codigoDocI;
            Box_Fecha.SelectedDate = DateTime.Today;
            Box_Hora.Text          = DateTime.Now.ToString("HH:mm:ss");

            CargarItems(null);
            CargarArbol();
            RefrescarGrid();
        }

        // ─── Carga unificada del catálogo de artículos + cantidades existentes ─
        private void CargarItems(string? docIdOrigen)
        {
            var existentes = new Dictionary<string, (string InventarioId, double Cantidad)>();
            if (!string.IsNullOrEmpty(docIdOrigen))
            {
                int uf = Sql.InventariosObj.ContarFilas;
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.InventariosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.InventariosObj.ObtenerItem("documentoI", id)?.ToString() != docIdOrigen) continue;

                    string artId    = Sql.InventariosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                    double cantidad = Convert.ToDouble(Sql.InventariosObj.ObtenerItem("cantidad", id) ?? 0);
                    existentes[artId] = (id, cantidad);
                }
            }

            _items = new List<InventarioItemFila>();
            int ufA = Sql.ArticulosObj.ContarFilas;
            for (int i = 1; i <= ufA; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string artId = idObj.ToString()!;

                existentes.TryGetValue(artId, out var ex);
                _items.Add(new InventarioItemFila
                {
                    InventarioId = ex.InventarioId ?? "",
                    ArticuloId   = artId,
                    Codigo       = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? "",
                    Categoria    = ObtenerCategoriaArticulo(artId),
                    Descripcion  = ObtenerDescripcionArticulo(artId),
                    Cantidad     = ex.Cantidad
                });
            }
        }

        // ─── Descripción de artículo ──────────────────────────────────────────
        private static string ObtenerDescripcionArticulo(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return "";
            string desc    = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
            string famId   = Sql.ArticulosObj.ObtenerItem("familia",     artId)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
            string modelo  = Sql.ArticulosObj.ObtenerItem("modelo",      artId)?.ToString() ?? "";
            return FuncionesComunes.UnirVariables(desc, famDesc, modelo);
        }

        // ─── Categoría de artículo ─────────────────────────────────────────────
        private static string ObtenerCategoriaArticulo(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return "";
            string catId = Sql.ArticulosObj.ObtenerItem("categoria", artId)?.ToString() ?? "";
            if (string.IsNullOrEmpty(catId)) return "";
            return Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "";
        }

        // ─── Árbol de productos/familias (mismo patrón que ArticulosGeneral) ──
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

        // ─── Refrescar grid (aplica filtro de árbol/búsqueda) ─────────────────
        private void RefrescarGrid()
        {
            // Confirma cualquier celda en edición antes de reemplazar el ItemsSource: hacerlo
            // a mitad de una transacción AddNew/EditItem dispara InvalidOperationException
            // ("'Refresh' no permitido...") al recrear la vista.
            Grid1.CommitEdit(DataGridEditingUnit.Row, true);

            string busqueda  = _modoFiltro == "busqueda" ? TxtBuscar.Text.Trim().ToLower() : "";
            string tagFiltro = _modoFiltro == "familia"  ? ObtenerTagFiltro()              : "";

            var visibles = new List<InventarioItemFila>();
            foreach (var item in _items)
            {
                string famId = Sql.ArticulosObj.ObtenerItem("familia", item.ArticuloId)?.ToString() ?? "";

                if (!string.IsNullOrEmpty(tagFiltro))
                {
                    if (tagFiltro.StartsWith("familia:"))
                    {
                        if (famId != tagFiltro.Substring("familia:".Length)) continue;
                    }
                    else if (tagFiltro.StartsWith("producto:"))
                    {
                        string prodFiltro = tagFiltro.Substring("producto:".Length);
                        string famProd    = Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "";
                        if (famProd != prodFiltro) continue;
                    }
                    // "todos" o vacío → sin filtro
                }

                if (!string.IsNullOrEmpty(busqueda) &&
                    !item.Codigo.ToLower().Contains(busqueda) &&
                    !item.Descripcion.ToLower().Contains(busqueda))
                    continue;

                visibles.Add(item);
            }

            var seleccionado = Grid1.SelectedItem as InventarioItemFila;

            Grid1.ItemsSource = visibles;

            if (seleccionado != null && visibles.Contains(seleccionado))
                Grid1.SelectedItem = seleccionado;

            ActualizarTotales();
            CargarTotalesCategoria();
        }

        // Recalcula los totales sobre TODO el catálogo (no solo lo visible tras el
        // filtro): representan el contenido real del inventario, no la vista.
        private void ActualizarTotales()
        {
            TxtTotalUnidades.Text      = _items.Sum(x => x.Cantidad).ToString("N2");
            TxtUnidadesDiferentes.Text = _items.Count(x => x.Cantidad > 0).ToString();
        }

        // ─── Totales por categoría ────────────────────────────────────────────
        private void CargarTotalesCategoria()
        {
            int ufCat = Sql.CategoriasObj.ContarFilas;
            var categoriaIds     = new List<string>();
            var categoriaDescs   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cantPorCategoria = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i <= ufCat; i++)
            {
                var idObj = Sql.CategoriasObj.Mover(i);
                if (idObj == null) continue;
                string catId   = idObj.ToString()!;
                string catDesc = Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? catId;
                categoriaIds.Add(catId);
                categoriaDescs[catId]   = catDesc;
                cantPorCategoria[catId] = 0;
            }

            double cantOtros = 0;
            foreach (var item in _items)
            {
                if (item.Cantidad <= 0) continue;

                string catId = Sql.ArticulosObj.ObtenerItem("categoria", item.ArticuloId)?.ToString() ?? "";
                if (!string.IsNullOrEmpty(catId) && cantPorCategoria.ContainsKey(catId))
                    cantPorCategoria[catId] += item.Cantidad;
                else
                    cantOtros += item.Cantidad;
            }

            var filas = categoriaIds
                .Select(id => new CategoriaCantFila
                {
                    Categoria = categoriaDescs[id],
                    Cantidad  = cantPorCategoria[id].ToString("N0")
                })
                .ToList();

            filas.Add(new CategoriaCantFila { Categoria = "Otros", Cantidad = cantOtros.ToString("N0") });
            GridCategorias.ItemsSource = filas;
        }

        // ─── Eventos árbol y búsqueda (mismo patrón que ArticulosGeneral) ─────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _modoFiltro = "familia";
            RefrescarGrid();
        }

        private void Tree1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null && source is not TreeViewItem)
                source = VisualTreeHelper.GetParent(source);

            if (source is TreeViewItem tvi && tvi.IsSelected)
            {
                _modoFiltro = "familia";
                RefrescarGrid();
            }
        }

        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _modoFiltro = "busqueda";
            RefrescarGrid();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            _modoFiltro = "busqueda";
            RefrescarGrid();
        }

        // ─── Detectar cambios ─────────────────────────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
            if (sender == Box_DocumentoI) LblDocNum.Text = Box_DocumentoI.Text;
        }

        private void Campo_DateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Validación de entrada ────────────────────────────────────────────
        private void Box_Numeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        // ─── Celda Cantidad editada ────────────────────────────────────────────
        private void Grid1_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            _hayCambios = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ActualizarTotales();
                CargarTotalesCategoria();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Seleccionar todo al entrar a la celda Cantidad ───────────────────
        private void Grid1_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.Column.Header?.ToString() != "Cantidad") return;
            GridFocusHelper.SeleccionarTodoEnEdicion(e.EditingElement);
            if (e.EditingElement is TextBox tb) FuncionesComunes.RestringirACantidad(tb);
        }

        // ─── Actualizar (recarga catálogo desde SQL conservando cantidades editadas) ─
        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            string artSelId = (Grid1.SelectedItem as InventarioItemFila)?.ArticuloId ?? "";

            AppState.ActualizarProductos();

            var porArticulo = _items.ToDictionary(x => x.ArticuloId, x => x);

            var actualizados = new List<InventarioItemFila>();
            int uf = Sql.ArticulosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string artId = idObj.ToString()!;

                if (porArticulo.TryGetValue(artId, out var existente))
                {
                    existente.Codigo      = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? "";
                    existente.Descripcion = ObtenerDescripcionArticulo(artId);
                    actualizados.Add(existente);
                }
                else
                {
                    actualizados.Add(new InventarioItemFila
                    {
                        InventarioId = "",
                        ArticuloId   = artId,
                        Codigo       = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? "",
                        Descripcion  = ObtenerDescripcionArticulo(artId),
                        Cantidad     = 0
                    });
                }
            }
            _items = actualizados;

            CargarArbol();
            RefrescarGrid();

            if (!string.IsNullOrEmpty(artSelId))
            {
                var fila = (Grid1.ItemsSource as List<InventarioItemFila>)?.Find(x => x.ArticuloId == artSelId);
                if (fila != null) { Grid1.SelectedItem = fila; Grid1.ScrollIntoView(fila); }
            }
        }

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            Grid1.CommitEdit(DataGridEditingUnit.Row, true);
            bool ok = Guardar();
            if (ok) { _hayCambios = false; Cerrando?.Invoke(); }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        { _hayCambios = false; Cerrando?.Invoke(); }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private bool Guardar()
        {
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return false;

            return AppState.EventoFormularioI == "editar"
                ? GuardarEditar()
                : GuardarNuevo();
        }

        private bool GuardarEditar()
        {
            string docId = _idEditar;
            try
            {
                // Combinar fecha y hora
                DateTime fecha = CombinarFechaHora();

                // Actualizar documento
                Sql.DocumentosIObj.EstablecerItem("fecha",       docId, fecha);
                Sql.DocumentosIObj.EstablecerItem("observacion", docId, Box_Observacion.Text);
                Sql.DocumentosIObj.EstablecerItem("referencia",  docId, Box_Referencia.Text.Trim());
                Sql.DocumentosIObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosIObj.EstablecerItem("usuario",     docId, AppState.UsuarioActivo);
                Sql.DocumentosIObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                GuardarLineasInventario(docId);

                Sql.InventariosObj.OrdenarData(("documentoI", false));
                Sql.DocumentosIObj.OrdenarData(("fecha", false));

                int periodo = string.IsNullOrEmpty(AppState.PeriodoActivo)
                    ? DateTime.Now.Year
                    : int.Parse(AppState.PeriodoActivo);
                AppState.ActualizarBase(periodo);
                AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);

                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool GuardarNuevo()
        {
            try
            {
                string docId = Guid.NewGuid().ToString();
                DateTime fecha = CombinarFechaHora();

                Sql.DocumentosIObj.Nuevo(docId);
                Sql.DocumentosIObj.EstablecerItem("codigo",      docId, _codigoDocI);
                Sql.DocumentosIObj.EstablecerItem("fecha",       docId, fecha);
                Sql.DocumentosIObj.EstablecerItem("observacion", docId, Box_Observacion.Text);
                Sql.DocumentosIObj.EstablecerItem("referencia",  docId, Box_Referencia.Text.Trim());
                Sql.DocumentosIObj.EstablecerItem("sucursal",    docId, AppState.SucursalActiva);
                Sql.DocumentosIObj.EstablecerItem("emision",     docId, DateTime.Now);
                Sql.DocumentosIObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosIObj.EstablecerItem("usuario",     docId, AppState.UsuarioActivo);
                Sql.DocumentosIObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                GuardarLineasInventario(docId);

                Sql.InventariosObj.OrdenarData(("documentoI", false));
                Sql.DocumentosIObj.OrdenarData(("fecha", false));

                int periodo = string.IsNullOrEmpty(AppState.PeriodoActivo)
                    ? DateTime.Now.Year
                    : int.Parse(AppState.PeriodoActivo);
                AppState.ActualizarBase(periodo);
                AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);

                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                ItemCreadoId = docId;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ─── Persistencia de líneas ────────────────────────────────────────────
        // Solo se registra en SQL un artículo SIN registro previo si su cantidad editada
        // es mayor a cero (nuevo inventario o ítem nuevo al editar). Un artículo CON
        // registro previo se actualiza siempre, incluso si su cantidad se editó a cero
        // (permanece "normal"). Nunca se elimina/oculta un registro aquí.
        private void GuardarLineasInventario(string docId)
        {
            foreach (var item in _items)
            {
                bool tieneRegistro = !string.IsNullOrEmpty(item.InventarioId);
                if (!tieneRegistro && item.Cantidad <= 0) continue;

                string id;
                if (!tieneRegistro)
                {
                    id = Guid.NewGuid().ToString();
                    Sql.InventariosObj.Nuevo(id);
                    Sql.InventariosObj.EstablecerItem("documentoI", id, docId);
                    item.InventarioId = id;
                }
                else id = item.InventarioId;

                Sql.InventariosObj.EstablecerItem("articulo", id, item.ArticuloId);
                Sql.InventariosObj.EstablecerItem("cantidad", id, item.Cantidad);
            }
        }

        // ─── Helper: combinar fecha del DatePicker y hora del TextBox ─────────
        private DateTime CombinarFechaHora()
        {
            DateTime fecha = Box_Fecha.SelectedDate ?? DateTime.Today;

            if (TimeSpan.TryParse(Box_Hora.Text, out TimeSpan hora))
                return fecha.Date + hora;

            return fecha.Date + DateTime.Now.TimeOfDay;
        }
    }

    // ─── Modelo de ítem ───────────────────────────────────────────────────────
    public class InventarioItemFila
    {
        public string InventarioId { get; set; } = ""; // vacío = sin registro en SQL
        public string ArticuloId   { get; set; } = "";
        public string Codigo       { get; set; } = "";
        public string Categoria    { get; set; } = "";
        public string Descripcion  { get; set; } = "";
        public double Cantidad     { get; set; }
    }
}
