using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SistemaGestion;
using SistemaGestion.Data;

namespace VisorEmpresa
{
    /// <summary>
    /// Detalle de lista de precios del visor: misma UI/lógica que la versión
    /// original de SistemaGestion (árbol de familias, catálogo completo, panel de
    /// Stock por sucursal), pero el cálculo de Stock/Disponible de empresa usa
    /// ConsultasEmpresa.ObtenerStockEmpresa (cacheado en memoria) en vez de
    /// recalcular con 5 consultas SQL cada vez que se abre un documento — ver
    /// CalcularStockEmpresa() más abajo. Precios/Empresas/Sucursales/Regiones/
    /// Usuarios ya no están vinculados a SistemaGestion: cada uno tiene su propia
    /// copia física e independiente en este proyecto.
    /// </summary>
    public partial class PreciosDetalle : UserControl
    {
        public event Action? Cerrando;

        private static SqlData Sql => SqlData.Instance;

        // object en vez de PreciosGeneral: _padre no se usa dentro de esta
        // clase, así que object evita acoplar el tipo sin necesidad.
        private readonly object? _padre;
        private readonly string _idEditar;
        private readonly string _idCopiarDe;
        private bool _hayCambios = false;
        private bool _cargando   = true;
        private List<PrecioItemFila> _items = new();

        // Stock/Disponible por artículo, TOTAL de la empresa (todas las sucursales).
        // Se recalcula en carga inicial y en BtnActualizar_Click; RefrescarGrid solo
        // lee estos valores.
        private Dictionary<string, (double Disponible, double Stock)> _stockEmpresa = new();

        // Detalle por sucursal (clave "sucursal|articulo"), para el panel lateral
        // GridSucursales. Se llena junto con _stockEmpresa en CalcularStockEmpresa().
        private Dictionary<string, StockAcumuladoInfo> _stockPorSucursal = new();

        // Modo de filtro activo: "todos" (carga inicial) | "busqueda" (TxtBuscar) | "familia" (Tree1)
        private string _modoFiltro = "todos";

        private bool _iniciado = false;
        private readonly string _tituloTab;
        private string _codigoDocL = "";

        /// <summary>ID de la lista de precios recién creada.</summary>
        public string? ItemCreadoId { get; private set; }

        public PreciosDetalle(object? padre = null, string idEditar = "", string tituloTab = "", string idCopiarDe = "")
        {
            InitializeComponent();
            _padre      = padre;
            _idEditar   = idEditar;
            _tituloTab  = tituloTab;
            _idCopiarDe = idCopiarDe;
            Loaded     += (_, _) => { if (_iniciado) return; _iniciado = true; CargarUserform(); };
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;
            CargarRegiones();

            if (!string.IsNullOrEmpty(_idEditar))
            {
                LblTitulo.Text = "Editar Lista de Precios";
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text = string.IsNullOrEmpty(_idCopiarDe)
                    ? "Nueva Lista de Precios"
                    : "Nueva Lista de Precios (copia)";
                CargarParaNuevo();
            }

            LblDocNum.Text = Box_DocumentoL.Text;
            _cargando   = false;
            _hayCambios = !string.IsNullOrEmpty(_idCopiarDe);
        }

        private void CargarRegiones()
        {
            var lista = new List<RegionItem>();
            int uf = Sql.RegionesObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.RegionesObj.Mover(i);
                if (idObj == null) continue;
                string id      = idObj.ToString()!;
                string codigo  = Sql.RegionesObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc    = Sql.RegionesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                lista.Add(new RegionItem { Id = id, Descripcion = $"{codigo} - {desc}" });
            }
            CboRegion.DisplayMemberPath = "Descripcion";
            CboRegion.SelectedValuePath = "Id";
            CboRegion.ItemsSource       = lista;
        }

        private void CargarParaEditar()
        {
            Box_DocumentoL.IsEnabled = false;
            string codigoDocEdit = Sql.DocumentosLObj.ObtenerItem("codigo", _idEditar)?.ToString() ?? "";
            Box_DocumentoL.Text = codigoDocEdit;

            var fechaObj = Sql.DocumentosLObj.ObtenerItem("fecha", _idEditar);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : DateTime.Now;
            Box_Fecha.SelectedDate = fecha;
            Box_Hora.Text          = fecha.ToString("HH:mm:ss");

            string regionId = Sql.DocumentosLObj.ObtenerItem("region", _idEditar)?.ToString() ?? "";
            CboRegion.SelectedValue = regionId;

            Box_Referencia.Text  = Sql.DocumentosLObj.ObtenerItem("referencia",  _idEditar)?.ToString() ?? "";
            Box_Observacion.Text = Sql.DocumentosLObj.ObtenerItem("observacion", _idEditar)?.ToString() ?? "";
            CboEstado.SelectedIndex = Sql.DocumentosLObj.ObtenerItem("estado", _idEditar)?.ToString() == "pendiente" ? 0 : 1;

            // Catálogo completo de artículos, con el precio existente (si lo hay) de esta lista.
            CargarItems(_idEditar, esCopia: false);
            CargarArbol();
            CalcularStockEmpresa();
            RefrescarGrid();
        }

        private void CargarParaNuevo()
        {
            Box_DocumentoL.IsEnabled = false;
            string signo  = Sql.EmpresasObj.ObtenerItem("signo", AppState.EmpresaActiva)?.ToString() ?? "";
            int    numero = Sql.DocumentosLObj.SiguienteNumeroDocPorRegion(signo, AppState.EmpresaActiva);
            _codigoDocL          = $"{signo.ToUpper()}{numero}";
            Box_DocumentoL.Text  = _codigoDocL;
            Box_Fecha.SelectedDate = DateTime.Today;
            Box_Hora.Text          = DateTime.Now.ToString("HH:mm:ss");
            CboEstado.SelectedIndex = 0;

            string regionPreferida = !string.IsNullOrEmpty(_idCopiarDe)
                ? Sql.DocumentosLObj.ObtenerItem("region", _idCopiarDe)?.ToString() ?? ""
                : AppState.RegionActiva;

            if (!string.IsNullOrEmpty(regionPreferida))
                CboRegion.SelectedValue = regionPreferida;
            else if (CboRegion.Items.Count > 0)
                CboRegion.SelectedIndex = 0;

            // Catálogo completo de artículos. Si viene de "copiar", los precios del
            // documento de origen quedan como base editable (PrecioId vacío → se
            // insertan como líneas nuevas al guardar, sin tocar el documento copiado).
            string? docOrigen = string.IsNullOrEmpty(_idCopiarDe) ? null : _idCopiarDe;
            CargarItems(docOrigen, esCopia: true);
            CargarArbol();
            CalcularStockEmpresa();
            RefrescarGrid();
        }

        // ─── Carga unificada del catálogo de artículos + precios existentes ───
        private void CargarItems(string? docIdOrigen, bool esCopia)
        {
            var existentes = new Dictionary<string, (string PrecioId, double Precio)>();
            if (!string.IsNullOrEmpty(docIdOrigen))
            {
                int uf = Sql.PreciosObj.ContarFilas;
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.PreciosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.PreciosObj.ObtenerItem("documentoL", id)?.ToString() != docIdOrigen) continue;

                    string artId  = Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                    double precio = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", id) ?? 0);
                    existentes[artId] = (esCopia ? "" : id, precio);
                }
            }

            _items = new List<PrecioItemFila>();
            int ufA = Sql.ArticulosObj.ContarFilas;
            for (int i = 1; i <= ufA; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string artId = idObj.ToString()!;

                existentes.TryGetValue(artId, out var ex);
                _items.Add(new PrecioItemFila
                {
                    PrecioId    = ex.PrecioId ?? "",
                    ArticuloId  = artId,
                    Codigo      = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? "",
                    Descripcion = ObtenerDescripcionArticulo(artId),
                    Precio      = ex.Precio
                });
            }
        }

        // ─── Stock/Disponible de TODA la empresa (todas las sucursales) ───────
        // A diferencia de SistemaGestion.PreciosDetalle (que recalcula con 5 consultas
        // SQL cada vez), acá se reusa ConsultasEmpresa.ObtenerStockEmpresa: el
        // resultado queda cacheado en memoria entre aperturas de este formulario y
        // solo se recalcula de verdad al pedir "forzarRecarga" (BtnActualizar_Click).
        private void CalcularStockEmpresa(bool forzarRecarga = false)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var resultado = ConsultasEmpresa.ObtenerStockEmpresa(AppState.EmpresaActiva, forzarRecarga: forzarRecarga);
                _stockEmpresa     = resultado.Totales;
                _stockPorSucursal = resultado.PorSucursal;
            }
            finally
            {
                Mouse.OverrideCursor = null;
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

        // ─── Refrescar grid (aplica filtro de árbol/búsqueda + calcula stock) ─
        private void RefrescarGrid()
        {
            // Confirma cualquier edición de Precio en curso antes de reemplazar el
            // ItemsSource: hacerlo a mitad de una transacción AddNew/EditItem dispara
            // InvalidOperationException ("'Refresh' no permitido...") al recrear la vista.
            Grid1.CommitEdit(DataGridEditingUnit.Row, true);

            string busqueda  = _modoFiltro == "busqueda" ? TxtBuscar.Text.Trim().ToLower() : "";
            string tagFiltro = _modoFiltro == "familia"  ? ObtenerTagFiltro()              : "";

            var visibles = new List<PrecioItemFila>();
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

                if (_stockEmpresa.TryGetValue(item.ArticuloId, out var totales))
                {
                    item.Stock      = totales.Stock;
                    item.Disponible = totales.Disponible;
                }
                else
                {
                    item.Stock      = 0;
                    item.Disponible = 0;
                }
                visibles.Add(item);
            }

            var seleccionado = Grid1.SelectedItem as PrecioItemFila;

            Grid1.ItemsSource = visibles;

            if (seleccionado != null && visibles.Contains(seleccionado))
                Grid1.SelectedItem = seleccionado;

            ActualizarTotales();
            ActualizarGridSucursales();
        }

        // Recalcula los totales sobre TODO el catálogo (no solo lo visible tras el
        // filtro): representan el contenido real de la lista de precios, no la vista.
        private void ActualizarTotales()
        {
            TxtTotalArticulos.Text = _items.Count(x => x.Precio > 0).ToString("N0");
        }

        // ─── Desglose por sucursal del artículo seleccionado en Grid1 ─────────
        private void Grid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => ActualizarGridSucursales();

        private void ActualizarGridSucursales()
        {
            var fila  = Grid1.SelectedItem as PrecioItemFila;
            var lista = new List<SucursalStockFila>();

            if (fila != null)
            {
                int ufSuc = Sql.SucursalesObj.ContarFilas;
                for (int i = 1; i <= ufSuc; i++)
                {
                    var sucIdObj = Sql.SucursalesObj.Mover(i);
                    if (sucIdObj == null) continue;
                    string sucId = sucIdObj.ToString()!;

                    _stockPorSucursal.TryGetValue(sucId + "|" + fila.ArticuloId, out var acumulado);

                    lista.Add(new SucursalStockFila
                    {
                        Sucursal   = Sql.SucursalesObj.ObtenerItem("descripcion", sucId)?.ToString() ?? "",
                        Disponible = acumulado?.Disponible ?? 0,
                        Stock      = acumulado?.Stock ?? 0
                    });
                }
            }

            GridSucursales.ItemsSource = lista;
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
            if (sender == Box_DocumentoL) LblDocNum.Text = Box_DocumentoL.Text;
        }

        private void Campo_DateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void CboEstado_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void Campo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Celda Precio editada ─────────────────────────────────────────────
        private void Grid1_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            _hayCambios = true;
            Dispatcher.BeginInvoke(new Action(ActualizarTotales),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Seleccionar todo al entrar a la celda Precio ─────────────────────
        private void Grid1_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.Column.Header?.ToString() != "Precio") return;
            GridFocusHelper.SeleccionarTodoEnEdicion(e.EditingElement);
            if (e.EditingElement is TextBox tb) FuncionesComunes.RestringirACantidad(tb);
        }

        // ─── Actualizar (recarga catálogo desde SQL conservando precios editados) ─
        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            string artSelId = (Grid1.SelectedItem as PrecioItemFila)?.ArticuloId ?? "";

            AppState.ActualizarProductos();

            var porArticulo = _items.ToDictionary(x => x.ArticuloId, x => x);

            var actualizados = new List<PrecioItemFila>();
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
                    actualizados.Add(new PrecioItemFila
                    {
                        PrecioId    = "",
                        ArticuloId  = artId,
                        Codigo      = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? "",
                        Descripcion = ObtenerDescripcionArticulo(artId),
                        Precio      = 0
                    });
                }
            }
            _items = actualizados;

            CargarArbol();
            CalcularStockEmpresa(forzarRecarga: true);
            RefrescarGrid();

            if (!string.IsNullOrEmpty(artSelId))
            {
                var fila = (Grid1.ItemsSource as List<PrecioItemFila>)?.Find(x => x.ArticuloId == artSelId);
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

        public void IntentarCerrar()
        {
            Grid1.CommitEdit(DataGridEditingUnit.Row, true);
            if (!_hayCambios) { Cerrando?.Invoke(); return; }

            var res = MessageBox.Show("¿Guardar cambios?", "Consola",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes && Guardar()) Cerrando?.Invoke();
            else if (res == MessageBoxResult.No) Cerrando?.Invoke();
        }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private bool Guardar()
        {
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return false;

            return !string.IsNullOrEmpty(_idEditar)
                ? GuardarEditar()
                : GuardarNuevo();
        }

        private bool ValidarCabecera()
        {
            if (CboRegion.SelectedItem == null)
            {
                MessageBox.Show("Seleccione una región.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (_items.All(x => x.Precio <= 0))
            {
                MessageBox.Show("Edite al menos un precio mayor a cero.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private bool GuardarNuevo()
        {
            if (!ValidarCabecera()) return false;

            try
            {
                string docId = Guid.NewGuid().ToString();
                DateTime fecha = CombinarFechaHora();

                Sql.DocumentosLObj.Nuevo(docId);
                Sql.DocumentosLObj.EstablecerItem("codigo",      docId, _codigoDocL);
                Sql.DocumentosLObj.EstablecerItem("empresa",     docId, AppState.EmpresaActiva);
                Sql.DocumentosLObj.EstablecerItem("fecha",       docId, fecha);
                Sql.DocumentosLObj.EstablecerItem("region",      docId, CboRegion.SelectedValue?.ToString() ?? "");
                Sql.DocumentosLObj.EstablecerItem("referencia",  docId, Box_Referencia.Text.Trim());
                Sql.DocumentosLObj.EstablecerItem("observacion", docId, Box_Observacion.Text.Trim());
                Sql.DocumentosLObj.EstablecerItem("estado",      docId, CboEstado.SelectedIndex == 0 ? "pendiente" : "valido");
                Sql.DocumentosLObj.EstablecerItem("emision",     docId, DateTime.Now);
                Sql.DocumentosLObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosLObj.EstablecerItem("usuario",     docId, AppState.UsuarioActivo);
                Sql.DocumentosLObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                CrearLineas(docId);

                Sql.PreciosObj.OrdenarData(("documentoL", false));
                Sql.DocumentosLObj.OrdenarData(("fecha", false));

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

        private bool GuardarEditar()
        {
            if (!ValidarCabecera()) return false;

            string docId = _idEditar;
            try
            {
                DateTime fecha = CombinarFechaHora();

                Sql.DocumentosLObj.EstablecerItem("fecha",       docId, fecha);
                Sql.DocumentosLObj.EstablecerItem("region",      docId, CboRegion.SelectedValue?.ToString() ?? "");
                Sql.DocumentosLObj.EstablecerItem("referencia",  docId, Box_Referencia.Text.Trim());
                Sql.DocumentosLObj.EstablecerItem("observacion", docId, Box_Observacion.Text.Trim());
                Sql.DocumentosLObj.EstablecerItem("estado",      docId, CboEstado.SelectedIndex == 0 ? "pendiente" : "valido");
                Sql.DocumentosLObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosLObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                CrearLineas(docId);

                Sql.PreciosObj.OrdenarData(("documentoL", false));
                Sql.DocumentosLObj.OrdenarData(("fecha", false));

                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ─── Persistencia de líneas ────────────────────────────────────────────
        // Solo se registra en SQL un artículo SIN registro previo si su precio editado
        // es mayor a cero (nueva línea). Un artículo CON registro previo se actualiza
        // siempre, incluso si su precio se editó a cero (permanece "normal", solo deja
        // de mostrarse en PDF/PedidosDetalle). Nunca se elimina/oculta un registro aquí.
        private void CrearLineas(string docId)
        {
            foreach (var item in _items)
            {
                bool tieneRegistro = !string.IsNullOrEmpty(item.PrecioId);
                if (!tieneRegistro && item.Precio <= 0) continue;

                string id;
                if (!tieneRegistro)
                {
                    id = Guid.NewGuid().ToString();
                    Sql.PreciosObj.Nuevo(id);
                    Sql.PreciosObj.EstablecerItem("documentoL", id, docId);
                    item.PrecioId = id;
                }
                else id = item.PrecioId;

                Sql.PreciosObj.EstablecerItem("articulo", id, item.ArticuloId);
                Sql.PreciosObj.EstablecerItem("precio",   id, item.Precio);
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
    public class PrecioItemFila
    {
        public string PrecioId    { get; set; } = ""; // vacío = sin registro en SQL
        public string ArticuloId  { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Stock       { get; set; }
        public double Disponible  { get; set; }
        public double Precio      { get; set; }
    }

    // ─── Fila del desglose por sucursal (panel lateral) ────────────────────────
    public class SucursalStockFila
    {
        public string Sucursal   { get; set; } = "";
        public double Disponible { get; set; }
        public double Stock      { get; set; }
    }
}
