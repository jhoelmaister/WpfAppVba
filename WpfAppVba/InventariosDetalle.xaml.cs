using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class InventariosDetalle : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly InventariosGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;
        private bool _iniciado   = false;
        private string _tituloTab = "";
        private List<InventarioItemFila> _items = new();

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
            GridItems.CommitEdit(DataGridEditingUnit.Row, true);

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

            _cargando   = false;
            _hayCambios = false;
        }

        private void CargarParaEditar()
        {
            Box_DocumentoI.IsEnabled = false;
            Box_DocumentoI.Text      = _idEditar;

            var fechaObj = Sql.DocumentosIObj.ObtenerItem("fecha", _idEditar);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : DateTime.Now;
            Box_Fecha.SelectedDate = fecha;
            Box_Hora.Text          = fecha.ToString("HH:mm:ss");
            Box_Observacion.Text   = Sql.DocumentosIObj.ObtenerItem("observacion", _idEditar)?.ToString() ?? "";

            // Cargar ítems del inventario
            _items.Clear();
            int linea = 1;
            int uf    = Sql.InventariosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.InventariosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.InventariosObj.ObtenerItem("documentoI", id)?.ToString() != _idEditar) continue;

                string articuloId = Sql.InventariosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                string codigo     = Sql.ArticulosObj.ObtenerItem("codigo", articuloId)?.ToString() ?? "";
                string desc       = ObtenerDescripcionArticulo(articuloId);
                double cantidad   = Convert.ToDouble(Sql.InventariosObj.ObtenerItem("cantidad", id) ?? 0);

                _items.Add(new InventarioItemFila
                {
                    InventarioId = id,
                    Linea        = linea++,
                    ArticuloId   = articuloId,
                    Codigo       = codigo,
                    Descripcion  = desc,
                    Cantidad     = cantidad
                });
            }

            RefrescarGrid();
        }

        private void CargarParaNuevo()
        {
            Box_DocumentoI.IsEnabled = true;
            long siguiente = Convert.ToInt64(Sql.DocumentosIObj.Maximo("id") ?? 0) + 1;
            Box_DocumentoI.Text    = siguiente.ToString();
            Box_Fecha.SelectedDate = DateTime.Today;
            Box_Hora.Text          = DateTime.Now.ToString("HH:mm:ss");
            _items.Clear();
            RefrescarGrid();
        }

        // ─── Descripción de artículo ──────────────────────────────────────────
        private string ObtenerDescripcionArticulo(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return "";
            string desc    = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
            string famId   = Sql.ArticulosObj.ObtenerItem("familia",     artId)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
            string modelo  = Sql.ArticulosObj.ObtenerItem("modelo",      artId)?.ToString() ?? "";
            return FuncionesComunes.UnirVariables(desc, famDesc, modelo);
        }

        // ─── Refrescar grid ───────────────────────────────────────────────────
        private void RefrescarGrid()
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i].Linea = i + 1;

            // Preservar selección antes de resetear el ItemsSource
            var seleccionado = GridItems.SelectedItem as InventarioItemFila;

            GridItems.ItemsSource = null;
            GridItems.ItemsSource = _items;

            // Restaurar selección si el ítem aún existe en la lista
            if (seleccionado != null && _items.Contains(seleccionado))
                GridItems.SelectedItem = seleccionado;

            TxtTotalUnidades.Text = _items.Sum(x => x.Cantidad).ToString("N2");
        }

        // ─── Detectar cambios ─────────────────────────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void Campo_DateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Validación de entrada ────────────────────────────────────────────
        private void Box_Numeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        // ─── Buscar artículos (modo exportar) ─────────────────────────────────
        private void BtnBuscarArticulos_Click(object sender, RoutedEventArgs e)
        {
            ArticulosGeneral.OpenAsTab(Window.GetWindow(this)!, callbackExportar: arts =>
            {
                foreach (var art in arts)
                {
                    // No agregar duplicados
                    if (_items.Any(x => x.ArticuloId == art.Id)) continue;

                    _items.Add(new InventarioItemFila
                    {
                        InventarioId = "",
                        Linea        = _items.Count + 1,
                        ArticuloId   = art.Id,
                        Codigo       = art.Codigo,
                        Descripcion  = art.Descripcion,
                        Cantidad     = 1
                    });
                }
                _hayCambios = true;
                RefrescarGrid();
                if (_items.Count > 0)
                {
                    var ultimo = _items[_items.Count - 1];
                    GridItems.SelectedItem = ultimo;
                    GridItems.ScrollIntoView(ultimo);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
            }, contexto: _tituloTab, llamador: this);
        }

        // ─── Buscar artículo (single-select) ─────────────────────────────────
        private void BtnBuscarArticulo_Click(object sender, RoutedEventArgs e)
        {
            var filaActual = GridItems.SelectedItem as InventarioItemFila;
            ArticulosGeneral.OpenAsTab(Window.GetWindow(this)!, callbackSingle: art =>
            {
                InventarioItemFila filaEnfocar;

                if (filaActual != null && _items.Contains(filaActual))
                {
                    filaActual.ArticuloId  = art.Id;
                    filaActual.Codigo      = art.Codigo;
                    filaActual.Descripcion = art.Descripcion;
                    filaEnfocar = filaActual;
                }
                else
                {
                    var nueva = new InventarioItemFila
                    {
                        InventarioId = "",
                        ArticuloId   = art.Id,
                        Codigo       = art.Codigo,
                        Descripcion  = art.Descripcion,
                        Cantidad     = 1
                    };
                    _items.Add(nueva);
                    filaEnfocar = nueva;
                }
                _hayCambios = true;
                RefrescarGrid();
                EnfocarColumnaCantidad(filaEnfocar);
            }, contexto: _tituloTab, llamador: this);
        }

        // Posiciona el cursor en la celda Cantidad de la fila indicada e inicia edición
        private void EnfocarColumnaCantidad(InventarioItemFila fila)
        {
            var colCantidad = GridItems.Columns
                .FirstOrDefault(c => c.Header?.ToString() == "Cantidad");
            if (colCantidad == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                GridItems.SelectedItem = fila;
                GridItems.CurrentCell  = new DataGridCellInfo(fila, colCantidad);
                GridItems.ScrollIntoView(fila, colCantidad);
                GridItems.Focus();
                GridItems.BeginEdit();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Nueva línea vacía ────────────────────────────────────────────────
        private void BtnNuevaLinea_Click(object sender, RoutedEventArgs e)
        {
            _items.Add(new InventarioItemFila
            {
                InventarioId = "",
                Linea        = _items.Count + 1,
                ArticuloId   = "",
                Codigo       = "",
                Descripcion  = "",
                Cantidad     = 1
            });
            _hayCambios = true;
            RefrescarGrid();
            int lastIdx = GridItems.Items.Count - 1;
            if (lastIdx >= 0)
            {
                GridItems.SelectedIndex = lastIdx;
                GridItems.ScrollIntoView(GridItems.SelectedItem);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
        }

        // ─── Eliminar línea seleccionada ──────────────────────────────────────
        private void BtnEliminarLinea_Click(object sender, RoutedEventArgs e)
        {
            if (GridItems.SelectedItem is not InventarioItemFila fila) return;

            var res = MessageBox.Show("¿Eliminar esta línea?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                int idx = _items.IndexOf(fila);
                _items.Remove(fila);
                _hayCambios = true;
                RefrescarGrid();
                if (_items.Count > 0)
                {
                    var siguiente = _items[Math.Min(idx, _items.Count - 1)];
                    GridItems.SelectedItem = siguiente;
                    GridItems.ScrollIntoView(siguiente);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
            }
        }

        // ─── Celda editada ────────────────────────────────────────────────────
        private void GridItems_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            _hayCambios = true;

            // Cuando se confirma la edición de la columna Código → buscar artículo
            if (e.EditAction == DataGridEditAction.Commit &&
                e.Column.Header?.ToString() == "Código" &&
                e.Row.Item is InventarioItemFila fila &&
                e.EditingElement is TextBox tb)
            {
                string codigo = tb.Text.Trim();
                long artIdNum = Sql.ArticulosObj.BuscarIdentificador("codigo", codigo);
                if (artIdNum > 0)
                {
                    string artId     = artIdNum.ToString();
                    fila.ArticuloId  = artId;
                    fila.Codigo      = codigo;
                    fila.Descripcion = ObtenerDescripcionArticulo(artId);
                }
                else
                {
                    fila.ArticuloId  = "";
                    fila.Codigo      = codigo;
                    fila.Descripcion = string.IsNullOrEmpty(codigo) ? "" : "⚠ Artículo no encontrado";
                }
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefrescarGrid();
                GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Seleccionar todo al entrar al campo Código ───────────────────────
        private void GridItems_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.Column.Header?.ToString() is "Código" or "Cantidad" &&
                e.EditingElement is TextBox tb)
            {
                tb.SelectAll();
                tb.Focus();
            }
        }

        // ─── Insertar línea en la posición seleccionada ───────────────────────
        private void BtnInsertar_Click(object sender, RoutedEventArgs e)
        {
            int idx = GridItems.SelectedItem is InventarioItemFila sel
                      ? _items.IndexOf(sel)
                      : _items.Count;
            if (idx < 0) idx = _items.Count;

            var nueva = new InventarioItemFila
            {
                InventarioId = "", ArticuloId = "", Codigo = "",
                Descripcion = "", Cantidad = 1
            };
            _items.Insert(idx, nueva);
            _hayCambios = true;
            RefrescarGrid();
            if (idx < GridItems.Items.Count)
            {
                GridItems.SelectedIndex = idx;
                GridItems.ScrollIntoView(GridItems.SelectedItem);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
        }

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            GridItems.CommitEdit(DataGridEditingUnit.Row, true);
            bool ok = Guardar();
            if (ok) { _hayCambios = false; Cerrando?.Invoke(); }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        { _hayCambios = false; Cerrando?.Invoke(); }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private bool Guardar()
        {
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
                Sql.DocumentosIObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosIObj.EstablecerItem("usuario",     docId, AppState.UsuarioActivo);
                Sql.DocumentosIObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                // Eliminar inventarios existentes de esta apertura
                int uf = Sql.InventariosObj.ContarFilas;
                var idsEliminar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.InventariosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.InventariosObj.ObtenerItem("documentoI", id)?.ToString() == docId)
                        idsEliminar.Add(id);
                }
                foreach (string id in idsEliminar)
                    Sql.InventariosObj.Eliminar(id);

                // Re-crear inventarios desde _items
                long maxInv = Convert.ToInt64(Sql.InventariosObj.Maximo("id") ?? 0);
                for (int i = 0; i < _items.Count; i++)
                {
                    var item   = _items[i];
                    string nid = (maxInv + i + 1).ToString();
                    Sql.InventariosObj.Nuevo(nid);
                    Sql.InventariosObj.EstablecerItem("documentoI", nid, docId);
                    Sql.InventariosObj.EstablecerItem("articulo",   nid, item.ArticuloId);
                    Sql.InventariosObj.EstablecerItem("cantidad",   nid, item.Cantidad);
                    Sql.InventariosObj.EstablecerItem("indice",     nid, i + 1);
                }

                Sql.InventariosObj.OrdenarData(("documentoI", false), ("indice", false));
                Sql.DocumentosIObj.OrdenarData(("id", false));

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
            string docId = Box_DocumentoI.Text.Trim();
            try
            {
                if (!Sql.DocumentosIObj.VerificarId(docId, "id"))
                {
                    MessageBox.Show("El código de inventario ya existe.", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                DateTime fecha = CombinarFechaHora();

                Sql.DocumentosIObj.Nuevo(docId);
                Sql.DocumentosIObj.EstablecerItem("fecha",       docId, fecha);
                Sql.DocumentosIObj.EstablecerItem("observacion", docId, Box_Observacion.Text);
                Sql.DocumentosIObj.EstablecerItem("sucursal",    docId, AppState.SucursalActiva);
                Sql.DocumentosIObj.EstablecerItem("emision",     docId, DateTime.Now);
                Sql.DocumentosIObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosIObj.EstablecerItem("usuario",     docId, AppState.UsuarioActivo);
                Sql.DocumentosIObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                long maxInv = Convert.ToInt64(Sql.InventariosObj.Maximo("id") ?? 0);
                for (int i = 0; i < _items.Count; i++)
                {
                    var item   = _items[i];
                    string nid = (maxInv + i + 1).ToString();
                    Sql.InventariosObj.Nuevo(nid);
                    Sql.InventariosObj.EstablecerItem("documentoI", nid, docId);
                    Sql.InventariosObj.EstablecerItem("articulo",   nid, item.ArticuloId);
                    Sql.InventariosObj.EstablecerItem("cantidad",   nid, item.Cantidad);
                    Sql.InventariosObj.EstablecerItem("indice",     nid, i + 1);
                }

                Sql.InventariosObj.OrdenarData(("documentoI", false), ("indice", false));
                Sql.DocumentosIObj.OrdenarData(("id", false));

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
        public string InventarioId { get; set; } = ""; // vacío = nuevo sin guardar
        public int    Linea        { get; set; }
        public string ArticuloId   { get; set; } = "";
        public string Codigo       { get; set; } = "";
        public string Descripcion  { get; set; } = "";
        public double Cantidad     { get; set; }
    }
}
