using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class PreciosDetalle : UserControl
    {
        public event Action? Cerrando;

        private static SqlData Sql => SqlData.Instance;

        private readonly PreciosGeneral? _padre;
        private readonly string _idEditar;
        private readonly string _idCopiarDe;
        private bool _hayCambios = false;
        private bool _cargando   = true;
        private List<PrecioItemFila> _items = new();
        // IDs de las líneas existentes al abrir para editar (para el guardado diferencial).
        private HashSet<string> _itemsOrig = new();

        private bool _iniciado = false;
        private readonly string _tituloTab;
        private string _codigoDocL = "";

        /// <summary>ID de la lista de precios recién creada.</summary>
        public string? ItemCreadoId { get; private set; }

        public PreciosDetalle(PreciosGeneral? padre = null, string idEditar = "", string tituloTab = "", string idCopiarDe = "")
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

            if (AppState.EventoFormularioL == "editar")
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

            // Cargar líneas de la lista de precios
            _items.Clear();
            int linea = 1;
            int uf    = Sql.PreciosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PreciosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.PreciosObj.ObtenerItem("documentoL", id)?.ToString() != _idEditar) continue;

                string articuloId = Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                string codigo     = Sql.ArticulosObj.ObtenerItem("codigo", articuloId)?.ToString() ?? "";
                string desc       = ObtenerDescripcionArticulo(articuloId);
                double precio     = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", id) ?? 0);

                _items.Add(new PrecioItemFila
                {
                    PrecioId    = id,
                    Linea       = linea++,
                    ArticuloId  = articuloId,
                    Codigo      = codigo,
                    Descripcion = desc,
                    Precio      = precio
                });
            }

            _itemsOrig = new HashSet<string>(_items.Select(x => x.PrecioId));
            RefrescarGrid();
        }

        private void CargarParaNuevo()
        {
            Box_DocumentoL.IsEnabled = false;
            string signo  = Sql.EmpresasObj.ObtenerItem("signo", AppState.EmpresaActiva)?.ToString() ?? "";
            int    numero = Sql.DocumentosLObj.SiguienteNumeroDoc(signo, "empresa", AppState.EmpresaActiva);
            _codigoDocL          = $"{signo.ToUpper()}{numero}";
            Box_DocumentoL.Text  = _codigoDocL;
            Box_Fecha.SelectedDate = DateTime.Today;
            Box_Hora.Text          = DateTime.Now.ToString("HH:mm:ss");

            string regionPreferida = !string.IsNullOrEmpty(_idCopiarDe)
                ? Sql.DocumentosLObj.ObtenerItem("region", _idCopiarDe)?.ToString() ?? ""
                : AppState.RegionActiva;

            if (!string.IsNullOrEmpty(regionPreferida))
                CboRegion.SelectedValue = regionPreferida;
            else if (CboRegion.Items.Count > 0)
                CboRegion.SelectedIndex = 0;

            _items.Clear();
            _itemsOrig.Clear();

            // Copiar líneas (artículo + precio) del documento de origen, como base
            // editable de la lista nueva (quedan sin PrecioId → se insertan al guardar).
            if (!string.IsNullOrEmpty(_idCopiarDe))
            {
                int linea = 1;
                int uf = Sql.PreciosObj.ContarFilas;
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.PreciosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.PreciosObj.ObtenerItem("documentoL", id)?.ToString() != _idCopiarDe) continue;

                    string articuloId = Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                    double precio     = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", id) ?? 0);

                    _items.Add(new PrecioItemFila
                    {
                        PrecioId    = "",
                        Linea       = linea++,
                        ArticuloId  = articuloId,
                        Codigo      = Sql.ArticulosObj.ObtenerItem("codigo", articuloId)?.ToString() ?? "",
                        Descripcion = ObtenerDescripcionArticulo(articuloId),
                        Precio      = precio
                    });
                }
            }

            RefrescarGrid();
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

        // ─── Refrescar grid ───────────────────────────────────────────────────
        private void RefrescarGrid()
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i].Linea = i + 1;

            var seleccionado = GridItems.SelectedItem as PrecioItemFila;

            GridItems.ItemsSource = null;
            GridItems.ItemsSource = _items;

            if (seleccionado != null && _items.Contains(seleccionado))
                GridItems.SelectedItem = seleccionado;

            TxtTotalArticulos.Text = _items.Count.ToString("N0");
            TxtValorTotal.Text     = _items.Sum(x => x.Precio).ToString("N2");
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

        private void Campo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Buscar artículos (multi-select) ──────────────────────────────────
        private void BtnBuscarArticulos_Click(object sender, RoutedEventArgs e)
        {
            ArticulosGeneral.OpenAsTab(Window.GetWindow(this)!, arts =>
            {
                foreach (var art in arts)
                {
                    if (_items.Any(x => x.ArticuloId == art.Id)) continue;

                    _items.Add(new PrecioItemFila
                    {
                        PrecioId    = "",
                        Linea       = _items.Count + 1,
                        ArticuloId  = art.Id,
                        Codigo      = art.Codigo,
                        Descripcion = art.Descripcion,
                        Precio      = 0
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
            }, null, contexto: _tituloTab, llamador: this);
        }

        // ─── Buscar artículo (single-select) ─────────────────────────────────
        private void BtnBuscarArticulo_Click(object sender, RoutedEventArgs e)
        {
            var filaActual = GridItems.SelectedItem as PrecioItemFila;
            ArticulosGeneral.OpenAsTab(Window.GetWindow(this)!, null, art =>
            {
                PrecioItemFila filaEnfocar;

                if (filaActual != null && _items.Contains(filaActual))
                {
                    filaActual.ArticuloId  = art.Id;
                    filaActual.Codigo      = art.Codigo;
                    filaActual.Descripcion = art.Descripcion;
                    filaEnfocar = filaActual;
                }
                else
                {
                    var nueva = new PrecioItemFila
                    {
                        PrecioId    = "",
                        ArticuloId  = art.Id,
                        Codigo      = art.Codigo,
                        Descripcion = art.Descripcion,
                        Precio      = 0
                    };
                    _items.Add(nueva);
                    filaEnfocar = nueva;
                }
                _hayCambios = true;
                RefrescarGrid();
                EnfocarColumnaPrecio(filaEnfocar);
            }, contexto: _tituloTab, llamador: this);
        }

        // Posiciona el cursor en la celda Precio de la fila indicada e inicia edición
        private void EnfocarColumnaPrecio(PrecioItemFila fila)
        {
            var colPrecio = GridItems.Columns
                .FirstOrDefault(c => c.Header?.ToString() == "Precio");
            if (colPrecio == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                GridItems.SelectedItem = fila;
                GridItems.CurrentCell  = new DataGridCellInfo(fila, colPrecio);
                GridItems.ScrollIntoView(fila, colPrecio);
                GridItems.Focus();
                GridItems.BeginEdit();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Nueva línea vacía ────────────────────────────────────────────────
        private void BtnNuevaLinea_Click(object sender, RoutedEventArgs e)
        {
            _items.Add(new PrecioItemFila
            {
                PrecioId    = "",
                Linea       = _items.Count + 1,
                ArticuloId  = "",
                Codigo      = "",
                Descripcion = "",
                Precio      = 0
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
            if (GridItems.SelectedItem is not PrecioItemFila fila) return;

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
                e.Row.Item is PrecioItemFila fila &&
                e.EditingElement is TextBox tb)
            {
                string codigo = tb.Text.Trim();
                string artId  = Sql.ArticulosObj.BuscarIdentificador("codigo", codigo);
                if (!string.IsNullOrEmpty(artId))
                {
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

        // ─── Seleccionar todo al entrar al campo Código / Precio ──────────────
        private void GridItems_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            string col = e.Column.Header?.ToString() ?? "";
            if (col is "Código" or "Precio")
                GridFocusHelper.SeleccionarTodoEnEdicion(e.EditingElement);
            if (col == "Precio" && e.EditingElement is TextBox tb)
                FuncionesComunes.RestringirACantidad(tb);
        }

        // ─── Insertar línea en la posición seleccionada ───────────────────────
        private void BtnInsertar_Click(object sender, RoutedEventArgs e)
        {
            int idx = GridItems.SelectedItem is PrecioItemFila sel
                      ? _items.IndexOf(sel)
                      : _items.Count;
            if (idx < 0) idx = _items.Count;

            var nueva = new PrecioItemFila
            {
                PrecioId = "", ArticuloId = "", Codigo = "",
                Descripcion = "", Precio = 0
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

        public void IntentarCerrar()
        {
            GridItems.CommitEdit(DataGridEditingUnit.Row, true);
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

            return AppState.EventoFormularioL == "editar"
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
            if (_items.Count == 0)
            {
                MessageBox.Show("Agregue al menos un artículo.", "Consola",
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
                Sql.DocumentosLObj.EstablecerItem("emision",     docId, DateTime.Now);
                Sql.DocumentosLObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosLObj.EstablecerItem("usuario",     docId, AppState.UsuarioActivo);
                Sql.DocumentosLObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                CrearLineas(docId);

                Sql.PreciosObj.OrdenarData(("documentoL", false), ("indice", false));
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
                Sql.DocumentosLObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosLObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                // Guardado diferencial de líneas (inserta/actualiza/oculta)
                CrearLineas(docId);

                Sql.PreciosObj.OrdenarData(("documentoL", false), ("indice", false));
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

        // ─── Guardado diferencial de líneas (inserta/actualiza/oculta) ──
        private void CrearLineas(string docId)
        {
            var vigentes = new HashSet<string>(
                _items.Where(x => !string.IsNullOrEmpty(x.PrecioId)).Select(x => x.PrecioId));

            var reservados = Sql.PreciosObj.IndicesNoNormales("documentoL", docId);
            foreach (var idOrig in _itemsOrig)
                if (!vigentes.Contains(idOrig))
                {
                    var ix = Sql.PreciosObj.ObtenerItem("indice", idOrig);
                    if (ix != null && int.TryParse(ix.ToString(), out int n)) reservados.Add(n);
                    Sql.PreciosObj.Eliminar(idOrig);
                }

            int next = 1;
            foreach (var item in _items)
            {
                string id;
                if (string.IsNullOrEmpty(item.PrecioId))
                {
                    id = Guid.NewGuid().ToString();
                    Sql.PreciosObj.Nuevo(id);
                    Sql.PreciosObj.EstablecerItem("documentoL", id, docId);
                    item.PrecioId = id;
                }
                else id = item.PrecioId;

                while (reservados.Contains(next)) next++;
                Sql.PreciosObj.EstablecerItem("indice",   id, next);
                next++;
                Sql.PreciosObj.EstablecerItem("articulo", id, item.ArticuloId);
                Sql.PreciosObj.EstablecerItem("precio",   id, item.Precio);
            }
            _itemsOrig = new HashSet<string>(_items.Select(x => x.PrecioId));
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
        public string PrecioId    { get; set; } = ""; // vacío = nuevo sin guardar
        public int    Linea       { get; set; }
        public string ArticuloId  { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Precio      { get; set; }
    }
}
