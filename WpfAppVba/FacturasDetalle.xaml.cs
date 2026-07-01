using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class FacturasDetalle : UserControl
    {
        public event Action? Cerrando;

        private static SqlData Sql => SqlData.Instance;

        private readonly FacturasGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;
        private List<FacturaItemFila> _items = new();
        // IDs de las líneas existentes al abrir para editar (para el guardado diferencial).
        private HashSet<string> _itemsOrig = new();

        private bool _iniciado = false;
        private readonly string _tituloTab;
        private string _codigoDocF = "";

        /// <summary>ID del documento de factura recién creado.</summary>
        public string? ItemCreadoId { get; private set; }

        public FacturasDetalle(FacturasGeneral? padre = null, string idEditar = "", string tituloTab = "")
        {
            InitializeComponent();
            _padre     = padre;
            _idEditar  = idEditar;
            _tituloTab = tituloTab;
            Loaded    += (_, _) => { if (_iniciado) return; _iniciado = true; CargarUserform(); };
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            if (!string.IsNullOrEmpty(_idEditar))
            {
                LblTitulo.Text = "Editar Factura";
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text = "Nueva Factura";
                CargarParaNuevo();
            }

            LblDocNum.Text = Box_DocumentoF.Text;
            _cargando   = false;
            _hayCambios = false;
        }

        private void CargarParaEditar()
        {
            string codigoDocEdit = Sql.DocumentosFObj.ObtenerItem("codigo", _idEditar)?.ToString() ?? "";
            Box_DocumentoF.Text = codigoDocEdit;

            var fechaObj = Sql.DocumentosFObj.ObtenerItem("fecha", _idEditar);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : DateTime.Now;
            Box_Fecha.SelectedDate = fecha;
            Box_Hora.Text          = fecha.ToString("HH:mm:ss");

            Box_Referencia.Text  = Sql.DocumentosFObj.ObtenerItem("referencia",  _idEditar)?.ToString() ?? "";
            Box_Observacion.Text = Sql.DocumentosFObj.ObtenerItem("observacion", _idEditar)?.ToString() ?? "";

            var emisionObj = Sql.DocumentosFObj.ObtenerItem("emision", _idEditar);
            var edicionObj = Sql.DocumentosFObj.ObtenerItem("edicion", _idEditar);
            Box_Emision.Text = emisionObj != null ? $"{Convert.ToDateTime(emisionObj):d} {Convert.ToDateTime(emisionObj):HH:mm:ss}" : "";
            Box_Edicion.Text = edicionObj != null ? $"{Convert.ToDateTime(edicionObj):d} {Convert.ToDateTime(edicionObj):HH:mm:ss}" : "";

            // Cargar líneas de la factura
            _items.Clear();
            int linea = 1;
            int uf    = Sql.FacturasObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.FacturasObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.FacturasObj.ObtenerItem("documentoF", id)?.ToString() != _idEditar) continue;

                string categoriaId = Sql.FacturasObj.ObtenerItem("categoria", id)?.ToString() ?? "";

                _items.Add(new FacturaItemFila
                {
                    FacturaId            = id,
                    Linea                = linea++,
                    Concepto             = Sql.FacturasObj.ObtenerItem("concepto", id)?.ToString() ?? "",
                    CategoriaId          = categoriaId,
                    CategoriaCodigo      = ObtenerCodigoCategoria(categoriaId),
                    CategoriaDescripcion = ObtenerDescripcionCategoria(categoriaId),
                    Importe              = Convert.ToDouble(Sql.FacturasObj.ObtenerItem("importe", id) ?? 0)
                });
            }

            _itemsOrig = new HashSet<string>(_items.Select(x => x.FacturaId));
            RefrescarGrid();
        }

        private void CargarParaNuevo()
        {
            string signo  = Sql.SucursalesObj.ObtenerItem("signo", AppState.SucursalActiva)?.ToString() ?? "";
            int    numero = Sql.DocumentosFObj.SiguienteNumeroDoc(signo, "sucursal", AppState.SucursalActiva);
            _codigoDocF          = $"{signo.ToUpper()}{numero}";
            Box_DocumentoF.Text  = _codigoDocF;
            Box_Fecha.SelectedDate = DateTime.Today;
            Box_Hora.Text          = DateTime.Now.ToString("HH:mm:ss");
            var ahora = DateTime.Now;
            Box_Emision.Text = $"{ahora:d} {ahora:HH:mm:ss}";
            Box_Edicion.Text = $"{ahora:d} {ahora:HH:mm:ss}";

            _items.Clear();
            _itemsOrig.Clear();
            RefrescarGrid();
        }

        // ─── Categoría (código ↔ id) ──────────────────────────────────────────
        private static string ObtenerCodigoCategoria(string categoriaId)
        {
            if (string.IsNullOrEmpty(categoriaId)) return "";
            return Sql.CategoriasObj.ObtenerItem("codigo", categoriaId)?.ToString() ?? "";
        }

        private static string ObtenerDescripcionCategoria(string categoriaId)
        {
            if (string.IsNullOrEmpty(categoriaId)) return "";
            return Sql.CategoriasObj.ObtenerItem("descripcion", categoriaId)?.ToString() ?? "";
        }

        // ─── Refrescar grid ───────────────────────────────────────────────────
        private void RefrescarGrid()
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i].Linea = i + 1;

            var seleccionado = GridItems.SelectedItem as FacturaItemFila;

            GridItems.ItemsSource = null;
            GridItems.ItemsSource = _items;

            if (seleccionado != null && _items.Contains(seleccionado))
                GridItems.SelectedItem = seleccionado;

            TxtTotalImporte.Text = _items.Sum(x => x.Importe).ToString("N2");
            TxtTotalLineas.Text  = _items.Count.ToString();
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

        // ─── Nueva línea vacía ────────────────────────────────────────────────
        private void BtnNuevaLinea_Click(object sender, RoutedEventArgs e)
        {
            _items.Add(new FacturaItemFila
            {
                FacturaId = "", Concepto = "", CategoriaId = "",
                CategoriaCodigo = "", CategoriaDescripcion = "", Importe = 0
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

        // ─── Duplicar línea seleccionada ──────────────────────────────────────
        private void BtnDuplicarLinea_Click(object sender, RoutedEventArgs e)
        {
            if (GridItems.SelectedItem is not FacturaItemFila fila) return;

            var copia = new FacturaItemFila
            {
                FacturaId = "", Concepto = fila.Concepto, CategoriaId = fila.CategoriaId,
                CategoriaCodigo = fila.CategoriaCodigo, CategoriaDescripcion = fila.CategoriaDescripcion,
                Importe = fila.Importe
            };
            _items.Add(copia);
            _hayCambios = true;
            RefrescarGrid();
            GridItems.SelectedItem = copia;
            GridItems.ScrollIntoView(copia);
            GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
        }

        // ─── Eliminar línea seleccionada ──────────────────────────────────────
        private void BtnEliminarLinea_Click(object sender, RoutedEventArgs e)
        {
            if (GridItems.SelectedItem is not FacturaItemFila fila) return;

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

        // ─── Insertar línea en la posición seleccionada ───────────────────────
        private void BtnInsertar_Click(object sender, RoutedEventArgs e)
        {
            int idx = GridItems.SelectedItem is FacturaItemFila sel
                      ? _items.IndexOf(sel)
                      : _items.Count;
            if (idx < 0) idx = _items.Count;

            var nueva = new FacturaItemFila
            {
                FacturaId = "", Concepto = "", CategoriaId = "",
                CategoriaCodigo = "", CategoriaDescripcion = "", Importe = 0
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

        // ─── Celda editada ────────────────────────────────────────────────────
        private void GridItems_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            _hayCambios = true;

            // Cuando se confirma la edición de la columna Categoría → buscar categoría
            if (e.EditAction == DataGridEditAction.Commit &&
                e.Column.Header?.ToString() == "Categoría" &&
                e.Row.Item is FacturaItemFila fila &&
                e.EditingElement is TextBox tb)
            {
                string codigo = tb.Text.Trim();
                string catId  = Sql.CategoriasObj.BuscarIdentificador("codigo", codigo);
                if (!string.IsNullOrEmpty(catId))
                {
                    fila.CategoriaId          = catId;
                    fila.CategoriaCodigo      = codigo;
                    fila.CategoriaDescripcion = ObtenerDescripcionCategoria(catId);
                }
                else
                {
                    fila.CategoriaId          = "";
                    fila.CategoriaCodigo      = codigo;
                    fila.CategoriaDescripcion = string.IsNullOrEmpty(codigo) ? "" : "⚠ Categoría no encontrada";
                }
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefrescarGrid();
                GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Seleccionar todo al entrar al campo Categoría / Importe ──────────
        private void GridItems_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
        {
            string col = e.Column.Header?.ToString() ?? "";
            if (col is "Categoría" or "Importe")
                GridFocusHelper.SeleccionarTodoEnEdicion(e.EditingElement);
            if (col == "Importe" && e.EditingElement is TextBox tb)
                FuncionesComunes.RestringirACantidad(tb);
        }

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            GridItems.CommitEdit(DataGridEditingUnit.Row, true);
            bool ok = Guardar();
            if (ok) { _hayCambios = false; Cerrando?.Invoke(); }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            _hayCambios = false; Cerrando?.Invoke();
        }

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

            return !string.IsNullOrEmpty(_idEditar)
                ? GuardarEditar()
                : GuardarNuevo();
        }

        private bool ValidarCabecera()
        {
            if (_items.Count == 0)
            {
                MessageBox.Show("Agregue al menos una línea.", "Consola",
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

                Sql.DocumentosFObj.Nuevo(docId);
                Sql.DocumentosFObj.EstablecerItem("codigo",      docId, _codigoDocF);
                Sql.DocumentosFObj.EstablecerItem("fecha",       docId, fecha);
                Sql.DocumentosFObj.EstablecerItem("sucursal",    docId, AppState.SucursalActiva);
                Sql.DocumentosFObj.EstablecerItem("referencia",  docId, Box_Referencia.Text.Trim());
                Sql.DocumentosFObj.EstablecerItem("observacion", docId, Box_Observacion.Text.Trim());
                Sql.DocumentosFObj.EstablecerItem("emision",     docId, DateTime.Now);
                Sql.DocumentosFObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosFObj.EstablecerItem("usuario",     docId, AppState.UsuarioActivo);
                Sql.DocumentosFObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                CrearLineas(docId);

                Sql.FacturasObj.OrdenarData(("documentoF", false), ("indice", false));
                Sql.DocumentosFObj.OrdenarData(("fecha", false));

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

                Sql.DocumentosFObj.EstablecerItem("fecha",       docId, fecha);
                Sql.DocumentosFObj.EstablecerItem("referencia",  docId, Box_Referencia.Text.Trim());
                Sql.DocumentosFObj.EstablecerItem("observacion", docId, Box_Observacion.Text.Trim());
                Sql.DocumentosFObj.EstablecerItem("edicion",     docId, DateTime.Now);
                Sql.DocumentosFObj.EstablecerItem("usuarioE",    docId, AppState.UsuarioActivo);

                // Guardado diferencial de líneas (inserta/actualiza/oculta)
                CrearLineas(docId);

                Sql.FacturasObj.OrdenarData(("documentoF", false), ("indice", false));
                Sql.DocumentosFObj.OrdenarData(("fecha", false));

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
                _items.Where(x => !string.IsNullOrEmpty(x.FacturaId)).Select(x => x.FacturaId));

            var reservados = Sql.FacturasObj.IndicesNoNormales("documentoF", docId);
            foreach (var idOrig in _itemsOrig)
                if (!vigentes.Contains(idOrig))
                {
                    var ix = Sql.FacturasObj.ObtenerItem("indice", idOrig);
                    if (ix != null && int.TryParse(ix.ToString(), out int n)) reservados.Add(n);
                    Sql.FacturasObj.Eliminar(idOrig);
                }

            int next = 1;
            foreach (var item in _items)
            {
                string id;
                if (string.IsNullOrEmpty(item.FacturaId))
                {
                    id = Guid.NewGuid().ToString();
                    Sql.FacturasObj.Nuevo(id);
                    Sql.FacturasObj.EstablecerItem("documentoF", id, docId);
                    item.FacturaId = id;
                }
                else id = item.FacturaId;

                while (reservados.Contains(next)) next++;
                Sql.FacturasObj.EstablecerItem("indice",    id, next);
                next++;
                Sql.FacturasObj.EstablecerItem("concepto",  id, item.Concepto);
                Sql.FacturasObj.EstablecerItem("categoria", id, item.CategoriaId);
                Sql.FacturasObj.EstablecerItem("importe",   id, item.Importe);
            }
            _itemsOrig = new HashSet<string>(_items.Select(x => x.FacturaId));
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
    public class FacturaItemFila
    {
        public string FacturaId            { get; set; } = ""; // vacío = nuevo sin guardar
        public int    Linea                { get; set; }
        public string Concepto             { get; set; } = "";
        public string CategoriaId          { get; set; } = "";
        public string CategoriaCodigo      { get; set; } = "";
        public string CategoriaDescripcion { get; set; } = "";
        public double Importe              { get; set; }
    }
}
