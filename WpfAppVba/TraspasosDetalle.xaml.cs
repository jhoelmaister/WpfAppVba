using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class TraspasosDetalle : Window
    {
        private static SqlData Sql => SqlData.Instance;
        private readonly TraspasosGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios      = false;
        private bool _cargando        = true;
        private bool _editarFormulario = false;
        private List<TraspasoItemFila> _items = new();

        public TraspasosDetalle(TraspasosGeneral? padre = null, string idEditar = "")
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            _padre    = padre;
            _idEditar = idEditar;
            Loaded   += (_, _) => CargarUserform();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            string tipo = AppState.TipoMovimiento.ToLower();
            LblTitulo.Text = tipo == "salida"
                ? "Salida de Productos Detalle"
                : "Entrada de Productos Detalle";

            if (AppState.EventoFormularioM == "editar")
                CargarParaEditar();
            else
                CargarParaNuevo();

            _cargando   = false;
            _hayCambios = false;
        }

        // ─── Modo editar ──────────────────────────────────────────────────────
        private void CargarParaEditar()
        {
            Box_DocumentoT.IsEnabled = false;
            Box_DocumentoT.Text = _idEditar;

            var fechaObj = Sql.DocumentosTObj.ObtenerItem("fecha", _idEditar);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : DateTime.Now;
            Box_Fecha.SelectedDate = fecha.Date;
            Box_Hora.Text = fecha.ToString("HH:mm:ss");

            // Sucursal opuesta
            string tipo    = AppState.TipoMovimiento.ToLower();
            string campOtro = tipo == "salida" ? "destino" : "origen";
            string otroId  = Sql.DocumentosTObj.ObtenerItem(campOtro, _idEditar)?.ToString() ?? "";
            Box_Sucursal_Identificador.Text = otroId;
            ActualizarDescripcionSucursal();

            // Emisión / Edición
            var emisionObj = Sql.DocumentosTObj.ObtenerItem("emision", _idEditar);
            var edicionObj = Sql.DocumentosTObj.ObtenerItem("edicion", _idEditar);
            TxtEmision.Text = emisionObj != null ? Convert.ToDateTime(emisionObj).ToString("dd/MM/yyyy HH:mm:ss") : "";
            TxtEdicion.Text = edicionObj != null ? Convert.ToDateTime(edicionObj).ToString("dd/MM/yyyy HH:mm:ss") : "";

            Box_Referencia.Text    = Sql.DocumentosTObj.ObtenerItem("referencia",  _idEditar)?.ToString() ?? "";
            Box_Observaciones.Text = Sql.DocumentosTObj.ObtenerItem("observacion", _idEditar)?.ToString() ?? "";

            // Permisos según emitido ─────────────────────────────────────────
            string emitido  = Sql.DocumentosTObj.ObtenerItem("emitido", _idEditar)?.ToString() ?? "";
            string estadoDB = Sql.DocumentosTObj.ObtenerItem("estado",  _idEditar)?.ToString() ?? "pendiente";
            bool esLocal    = (emitido == AppState.SucursalActiva.ToString());

            if (esLocal)
            {
                if (estadoDB.ToLower() == "entregado")
                {
                    // Solo lectura total: no se puede editar nada
                    DeshabilitarControlesCabecera();
                    _editarFormulario = false;
                    GridItems.IsEnabled = false;
                    ActualizarBotonesGrid();
                }
                else
                {
                    // Puede editar artículos pero no el estado
                    Box_Estado.IsEnabled = false;
                    _editarFormulario    = true;
                }
                SeleccionarEstado(estadoDB);
            }
            else
            {
                // De otra sucursal: solo se puede cambiar estado y ver artículos
                DeshabilitarControlesCabecera();

                if (estadoDB.ToLower() == "pendiente")
                    estadoDB = "pendiente revisar";

                Box_Estado.Items.Clear();
                Box_Estado.Items.Add(new ComboBoxItem { Content = "pendiente revisar" });
                Box_Estado.Items.Add(new ComboBoxItem { Content = "entregado" });
                Box_Estado.IsEnabled = true;
                Box_Referencia.IsEnabled    = true;
                Box_Observaciones.IsEnabled = true;
                _editarFormulario = true;
                SeleccionarEstado(estadoDB);
            }

            // Cargar artículos
            _items.Clear();
            int linea = 1;
            int uf = Sql.TraspasosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.TraspasosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() != _idEditar) continue;

                string artId  = Sql.TraspasosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                string codigo = Sql.ArticulosObj.ObtenerItem("codigo",   artId)?.ToString() ?? "";
                double cant   = Convert.ToDouble(Sql.TraspasosObj.ObtenerItem("cantidad", id) ?? 0);
                string desc   = ObtenerDescripcionArticulo(artId);

                _items.Add(new TraspasoItemFila
                {
                    TraspasoId  = id,
                    Linea       = linea++,
                    ArticuloId  = artId,
                    Codigo      = codigo,
                    Descripcion = desc,
                    Cantidad    = cant
                });
            }
            RefrescarGrid();
        }

        // ─── Modo nuevo ───────────────────────────────────────────────────────
        private void CargarParaNuevo()
        {
            _editarFormulario = true;
            Box_DocumentoT.IsEnabled = true;
            long siguiente = Convert.ToInt64(Sql.DocumentosTObj.Maximo("id") ?? 0) + 1;
            Box_DocumentoT.Text = siguiente.ToString();

            DateTime ahora = DateTime.Now;
            Box_Fecha.SelectedDate = ahora.Date;
            Box_Hora.Text = ahora.ToString("HH:mm:ss");
            SeleccionarEstado("pendiente");
            Box_Estado.IsEnabled = false;

            TxtEmision.Text = ahora.ToString("dd/MM/yyyy HH:mm:ss");
            TxtEdicion.Text = ahora.ToString("dd/MM/yyyy HH:mm:ss");

            _items.Clear();
            RefrescarGrid();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private void SeleccionarEstado(string valor)
        {
            foreach (ComboBoxItem item in Box_Estado.Items)
            {
                if (item.Content?.ToString() == valor)
                {
                    Box_Estado.SelectedItem = item;
                    return;
                }
            }
            if (Box_Estado.Items.Count > 0) Box_Estado.SelectedIndex = 0;
        }

        private void ActualizarDescripcionSucursal()
        {
            string id = Box_Sucursal_Identificador.Text.Trim();
            Box_Sucursal_Descripcion.Text = Sql.SucursalesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
        }

        private static string ObtenerDescripcionArticulo(string artId)
        {
            string desc    = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
            string famId   = Sql.ArticulosObj.ObtenerItem("familia",     artId)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
            string modelo  = Sql.ArticulosObj.ObtenerItem("modelo",      artId)?.ToString() ?? "";
            return FuncionesComunes.UnirVariables(desc, famDesc, modelo);
        }

        private void DeshabilitarControlesCabecera()
        {
            Box_DocumentoT.IsEnabled            = false;
            Box_Fecha.IsEnabled                  = false;
            Box_Hora.IsEnabled                   = false;
            Box_Sucursal_Identificador.IsEnabled = false;
            BtnBuscarSucursal.IsEnabled          = false;
            Box_Estado.IsEnabled                 = false;
            Box_Referencia.IsEnabled             = false;
            Box_Observaciones.IsEnabled          = false;
        }

        private void ActualizarBotonesGrid()
        {
            BtnImportarArticulos.IsEnabled = _editarFormulario;
            BtnBuscarArticulo.IsEnabled    = _editarFormulario;
            BtnInsertar.IsEnabled          = _editarFormulario;
            BtnNuevaLinea.IsEnabled        = _editarFormulario;
            BtnEliminarLinea.IsEnabled     = _editarFormulario;
        }

        // ─── Refrescar grid y totales ─────────────────────────────────────────
        private void RefrescarGrid()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].Linea = i + 1;

            var seleccionado = GridItems.SelectedItem as TraspasoItemFila;
            GridItems.ItemsSource = null;
            GridItems.ItemsSource = _items;
            if (seleccionado != null && _items.Contains(seleccionado))
                GridItems.SelectedItem = seleccionado;

            CargarTotales();
        }

        // ─── Totales por categoría ────────────────────────────────────────────
        private void CargarTotales()
        {
            double totalPeq = 0, totalMed = 0, totalGra = 0, totalOtros = 0, totalUnidades = 0;
            var distintos = new HashSet<string>();

            foreach (var item in _items)
            {
                totalUnidades += item.Cantidad;
                if (!string.IsNullOrEmpty(item.ArticuloId))
                    distintos.Add(item.ArticuloId);

                string catId   = Sql.ArticulosObj.ObtenerItem("categoria",   item.ArticuloId)?.ToString() ?? "";
                string catDesc = string.IsNullOrEmpty(catId) ? "" :
                                 Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "";

                switch (catDesc.ToLower())
                {
                    case "pequeña": totalPeq   += item.Cantidad; break;
                    case "mediana": totalMed   += item.Cantidad; break;
                    case "grande":  totalGra   += item.Cantidad; break;
                    default:        totalOtros += item.Cantidad; break;
                }
            }

            TxtTotalPeq.Text            = totalPeq.ToString("N0");
            TxtTotalMed.Text            = totalMed.ToString("N0");
            TxtTotalGra.Text            = totalGra.ToString("N0");
            TxtTotalOtros.Text          = totalOtros.ToString("N0");
            TxtTotalUnidades.Text       = totalUnidades.ToString("N0");
            TxtUnidadesDiferentes.Text  = distintos.Count.ToString();
        }

        // ─── Stock del artículo seleccionado (GridStock / Lista3) ─────────────
        private void CargarStock(TraspasoItemFila? fila)
        {
            if (fila == null || string.IsNullOrEmpty(fila.ArticuloId))
            {
                GridStock.ItemsSource = null;
                return;
            }

            double stock = StockCalculator.ContarStock(fila.ArticuloId, AppState.DataFechaFinal);

            GridStock.ItemsSource = new List<TraspasoStockFila>
            {
                new TraspasoStockFila
                {
                    Codigo     = fila.Codigo,
                    Disponible = stock.ToString("N0"),
                    Stock      = stock.ToString("N0")
                }
            };
        }

        // ─── Notificación de stock insuficiente (solo salidas, modo nuevo) ────
        private void NotificarStockInsuficiente(TraspasoItemFila? filaModificada = null)
        {
            if (AppState.TipoMovimiento.ToLower() != "salida") return;
            if (AppState.EventoFormularioM != "nuevo") return;

            // Agrupa artículos ya notificados para no repetir
            var notificados = new HashSet<string>();

            foreach (var item in _items)
            {
                if (string.IsNullOrEmpty(item.ArticuloId)) continue;
                if (notificados.Contains(item.ArticuloId)) continue;

                double totalCant = _items.Where(x => x.ArticuloId == item.ArticuloId)
                                          .Sum(x => x.Cantidad);
                double stock = StockCalculator.ContarStock(item.ArticuloId, AppState.DataFechaFinal);

                if (stock < totalCant)
                {
                    notificados.Add(item.ArticuloId);
                    MessageBox.Show($"{item.Descripcion}: stock insuficiente (disponible: {stock:F0}, solicitado: {totalCant:F0}).",
                        "Consola", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // ─── Eventos de campos ────────────────────────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void Campo_DateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void Campo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void Box_Sucursal_Identificador_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando)
            {
                ActualizarDescripcionSucursal();
                _hayCambios = true;
            }
        }

        private void Box_Numeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        // ─── Selección en GridItems → mostrar stock ───────────────────────────
        private void GridItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => CargarStock(GridItems.SelectedItem as TraspasoItemFila);

        // ─── Buscar sucursal ──────────────────────────────────────────────────
        private void BtnBuscarSucursal_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SucursalesGeneral(modoSelector: true);
            dlg.ShowDialog();
            if (SucursalesGeneral.SucursalSeleccionada != null)
            {
                Box_Sucursal_Identificador.Text = SucursalesGeneral.SucursalSeleccionada;
                ActualizarDescripcionSucursal();
                SucursalesGeneral.SucursalSeleccionada = null;
                _hayCambios = true;
            }
        }

        // ─── Buscar artículo (single-select) ─────────────────────────────────
        private void BtnBuscarArticulo_Click(object sender, RoutedEventArgs e)
        {
            if (!_editarFormulario) return;

            var filaActual = GridItems.SelectedItem as TraspasoItemFila;
            ArticulosGeneral.OpenAsDialog(Window.GetWindow(this)!, null, art =>
            {
                if (filaActual != null && _items.Contains(filaActual))
                {
                    filaActual.ArticuloId  = art.Id;
                    filaActual.Codigo      = art.Codigo;
                    filaActual.Descripcion = art.Descripcion;
                }
                else
                {
                    _items.Add(new TraspasoItemFila
                    {
                        TraspasoId  = "",
                        ArticuloId  = art.Id,
                        Codigo      = art.Codigo,
                        Descripcion = art.Descripcion,
                        Cantidad    = 1
                    });
                }
                _hayCambios = true;
                RefrescarGrid();
                NotificarStockInsuficiente();
            });
        }

        // ─── Importar artículos ───────────────────────────────────────────────
        private void BtnImportarArticulos_Click(object sender, RoutedEventArgs e)
        {
            if (!_editarFormulario) return;

            ArticulosGeneral.OpenAsDialog(Window.GetWindow(this)!, arts =>
            {
                foreach (var art in arts)
                {
                    if (_items.Any(x => x.ArticuloId == art.Id)) continue;
                    _items.Add(new TraspasoItemFila
                    {
                        TraspasoId  = "",
                        ArticuloId  = art.Id,
                        Codigo      = art.Codigo,
                        Descripcion = art.Descripcion,
                        Cantidad    = 1
                    });
                }
                _hayCambios = true;
                RefrescarGrid();
                NotificarStockInsuficiente();
            }, null);
        }

        // ─── Nueva línea vacía ────────────────────────────────────────────────
        private void BtnNuevaLinea_Click(object sender, RoutedEventArgs e)
        {
            if (!_editarFormulario) return;

            _items.Add(new TraspasoItemFila
            {
                TraspasoId = "", ArticuloId = "", Codigo = "",
                Descripcion = "", Cantidad = 1
            });
            _hayCambios = true;
            RefrescarGrid();
        }

        // ─── Eliminar línea seleccionada ──────────────────────────────────────
        private void BtnEliminarLinea_Click(object sender, RoutedEventArgs e)
        {
            if (!_editarFormulario) return;
            if (GridItems.SelectedItem is not TraspasoItemFila fila) return;
            _items.Remove(fila);
            _hayCambios = true;
            RefrescarGrid();
        }

        // ─── Edición de celda ─────────────────────────────────────────────────
        private void GridItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            _hayCambios = true;

            if (e.EditAction == DataGridEditAction.Commit &&
                e.Column.Header?.ToString() == "Código" &&
                e.Row.Item is TraspasoItemFila fila &&
                e.EditingElement is TextBox tb)
            {
                string codigo  = tb.Text.Trim();
                long artIdNum  = Sql.ArticulosObj.BuscarIdentificador("codigo", codigo);
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

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefrescarGrid();
                    NotificarStockInsuficiente(fila);
                }), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            if (e.EditAction == DataGridEditAction.Commit &&
                e.Column.Header?.ToString() == "Cantidad" &&
                e.Row.Item is TraspasoItemFila filaCant &&
                e.EditingElement is TextBox tbCant)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefrescarGrid();
                    NotificarStockInsuficiente(filaCant);
                }), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            Dispatcher.BeginInvoke(new Action(RefrescarGrid),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Seleccionar todo al entrar al campo Código ───────────────────────
        private void GridItems_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
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
            if (!_editarFormulario) return;

            int idx = GridItems.SelectedItem is TraspasoItemFila sel
                      ? _items.IndexOf(sel)
                      : _items.Count;
            if (idx < 0) idx = _items.Count;

            var nueva = new TraspasoItemFila
            {
                TraspasoId = "", ArticuloId = "", Codigo = "",
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
        }

        // ─── Guardar ──────────────────────────────────────────────────────────
        private bool Guardar()
            => AppState.EventoFormularioM == "editar" ? GuardarEditar() : GuardarNuevo();

        private bool GuardarNuevo()
        {
            string docT = Box_DocumentoT.Text.Trim();
            if (string.IsNullOrEmpty(docT))
            {
                MessageBox.Show("Ingrese el número de documento.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                if (!Sql.DocumentosTObj.VerificarId(docT, "id"))
                {
                    MessageBox.Show("El número de documento ya existe.", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                DateTime fechaBase  = Box_Fecha.SelectedDate ?? DateTime.Today;
                DateTime fechaFinal = CombinarFechaHora(fechaBase, Box_Hora.Text);

                string tipo    = AppState.TipoMovimiento.ToLower();
                string origen  = tipo == "salida"  ? AppState.SucursalActiva.ToString() : Box_Sucursal_Identificador.Text.Trim();
                string destino = tipo == "entrada" ? AppState.SucursalActiva.ToString() : Box_Sucursal_Identificador.Text.Trim();
                string estado  = (Box_Estado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "pendiente";

                Sql.DocumentosTObj.Nuevo(docT);
                Sql.DocumentosTObj.EstablecerItem("origen",      docT, origen);
                Sql.DocumentosTObj.EstablecerItem("destino",     docT, destino);
                Sql.DocumentosTObj.EstablecerItem("fecha",       docT, fechaFinal);
                Sql.DocumentosTObj.EstablecerItem("estado",      docT, estado);
                Sql.DocumentosTObj.EstablecerItem("referencia",  docT, Box_Referencia.Text.Trim());
                Sql.DocumentosTObj.EstablecerItem("observacion", docT, Box_Observaciones.Text.Trim());
                Sql.DocumentosTObj.EstablecerItem("emision",     docT, DateTime.Now);
                Sql.DocumentosTObj.EstablecerItem("edicion",     docT, DateTime.Now);
                Sql.DocumentosTObj.EstablecerItem("usuario",     docT, AppState.UsuarioActivo);
                Sql.DocumentosTObj.EstablecerItem("emitido",     docT, AppState.SucursalActiva);

                // ── Crear líneas con ID = documentoT + indice 3 dígitos (igual VBA) ──
                for (int i = 0; i < _items.Count; i++)
                {
                    var item  = _items[i];
                    string idStr = $"{docT}{(i + 1):D3}";   // e.g. "123001"
                    Sql.TraspasosObj.Nuevo(idStr);
                    Sql.TraspasosObj.EstablecerItem("documentoT", idStr, docT);
                    Sql.TraspasosObj.EstablecerItem("articulo",   idStr, item.ArticuloId);
                    Sql.TraspasosObj.EstablecerItem("cantidad",   idStr, item.Cantidad);
                    Sql.TraspasosObj.EstablecerItem("indice",     idStr, i + 1);
                }

                Sql.DocumentosTObj.OrdenarData(("fecha", false));
                Sql.TraspasosObj.OrdenarData(("documentoT", false), ("indice", false));

                MessageBox.Show("Guardado exitoso.", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                _hayCambios = false;
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
            string docT = _idEditar;
            try
            {
                DateTime fechaBase  = Box_Fecha.SelectedDate ?? DateTime.Today;
                DateTime fechaFinal = CombinarFechaHora(fechaBase, Box_Hora.Text);

                string tipo     = AppState.TipoMovimiento.ToLower();
                string campOtro = tipo == "salida" ? "destino" : "origen";
                string estado   = (Box_Estado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "pendiente";

                // Si es "pendiente revisar" guardar como "pendiente" en la DB (igual VBA)
                if (estado == "pendiente revisar") estado = "pendiente";

                Sql.DocumentosTObj.EstablecerItem("fecha",       docT, fechaFinal);
                Sql.DocumentosTObj.EstablecerItem(campOtro,      docT, Box_Sucursal_Identificador.Text.Trim());
                Sql.DocumentosTObj.EstablecerItem("estado",      docT, estado);
                Sql.DocumentosTObj.EstablecerItem("referencia",  docT, Box_Referencia.Text.Trim());
                Sql.DocumentosTObj.EstablecerItem("observacion", docT, Box_Observaciones.Text.Trim());
                Sql.DocumentosTObj.EstablecerItem("edicion",     docT, DateTime.Now);

                // ── Eliminar líneas existentes (igual VBA: .eliminar, no .ocultar) ──
                int uf = Sql.TraspasosObj.ContarFilas;
                var idsEliminar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.TraspasosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() == docT)
                        idsEliminar.Add(id);
                }
                foreach (var id in idsEliminar)
                    Sql.TraspasosObj.Eliminar(id);

                // ── Re-crear con ID = documentoT + indice 3 dígitos (igual VBA) ──
                for (int i = 0; i < _items.Count; i++)
                {
                    var item  = _items[i];
                    string idStr = $"{docT}{(i + 1):D3}";
                    Sql.TraspasosObj.Nuevo(idStr);
                    Sql.TraspasosObj.EstablecerItem("documentoT", idStr, docT);
                    Sql.TraspasosObj.EstablecerItem("articulo",   idStr, item.ArticuloId);
                    Sql.TraspasosObj.EstablecerItem("cantidad",   idStr, item.Cantidad);
                    Sql.TraspasosObj.EstablecerItem("indice",     idStr, i + 1);
                }

                Sql.DocumentosTObj.OrdenarData(("fecha", false));
                Sql.TraspasosObj.OrdenarData(("documentoT", false), ("indice", false));

                MessageBox.Show("Guardado exitoso.", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                _hayCambios = false;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ─── Combinar fecha + hora ────────────────────────────────────────────
        private static DateTime CombinarFechaHora(DateTime fecha, string horaTexto)
        {
            if (TimeSpan.TryParse(horaTexto, out var ts))
                return fecha.Date + ts;
            return fecha.Date;
        }

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (Guardar()) Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
            => Close();

        // ─── Al cerrar ────────────────────────────────────────────────────────
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_hayCambios) return;

            var res = MessageBox.Show("¿Guardar cambios?", "Consola",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                bool ok = Guardar();
                e.Cancel = !ok;
            }
            else if (res == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
        }
    }

    // ─── Modelos ──────────────────────────────────────────────────────────────
    public class TraspasoItemFila
    {
        public string TraspasoId  { get; set; } = "";
        public int    Linea       { get; set; }
        public string ArticuloId  { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Cantidad    { get; set; }
    }

    public class TraspasoStockFila
    {
        public string Codigo     { get; set; } = "";
        public string Disponible { get; set; } = "";
        public string Stock      { get; set; } = "";
    }
}
