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
        private bool _hayCambios = false;
        private bool _cargando   = true;
        private List<TraspasoItemFila> _items = new();

        public TraspasosDetalle(TraspasosGeneral? padre = null, string idEditar = "")
        {
            InitializeComponent();
            _padre    = padre;
            _idEditar = idEditar;
            Loaded   += (_, _) => CargarUserform();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            string tipo = AppState.TipoMovimiento.ToLower();
            LblTitulo.Text = tipo == "salida" ? "Salida de Productos" : "Entrada de Productos";

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

            string tipo    = AppState.TipoMovimiento.ToLower();
            string campOtro = tipo == "salida" ? "destino" : "origen";
            string otroId  = Sql.DocumentosTObj.ObtenerItem(campOtro, _idEditar)?.ToString() ?? "";
            Box_Sucursal_Identificador.Text = otroId;
            ActualizarDescripcionSucursal();

            // Estado: seleccionar en ComboBox
            string estadoVal = Sql.DocumentosTObj.ObtenerItem("estado", _idEditar)?.ToString() ?? "pendiente";
            SeleccionarEstado(estadoVal);

            Box_Referencia.Text    = Sql.DocumentosTObj.ObtenerItem("referencia",  _idEditar)?.ToString() ?? "";
            Box_Observaciones.Text = Sql.DocumentosTObj.ObtenerItem("observacion", _idEditar)?.ToString() ?? "";

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
            Box_DocumentoT.IsEnabled = true;
            long siguiente = Convert.ToInt64(Sql.DocumentosTObj.Maximo("id") ?? 0) + 1;
            Box_DocumentoT.Text = siguiente.ToString();
            Box_Fecha.SelectedDate = DateTime.Today;
            Box_Hora.Text = DateTime.Now.ToString("HH:mm:ss");
            SeleccionarEstado("pendiente");
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
            if (Box_Estado.Items.Count > 0)
                Box_Estado.SelectedIndex = 0;
        }

        private void ActualizarDescripcionSucursal()
        {
            string id = Box_Sucursal_Identificador.Text.Trim();
            Box_Sucursal_Descripcion.Text = Sql.SucursalesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
        }

        private static string ObtenerDescripcionArticulo(string artId)
        {
            string desc   = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
            string famId  = Sql.ArticulosObj.ObtenerItem("familia",     artId)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
            string modelo = Sql.ArticulosObj.ObtenerItem("modelo",      artId)?.ToString() ?? "";
            return FuncionesComunes.UnirVariables(desc, famDesc, modelo);
        }

        private void RefrescarGrid()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].Linea = i + 1;
            GridItems.ItemsSource = null;
            GridItems.ItemsSource = _items;
            TxtTotalUnidades.Text = _items.Sum(x => x.Cantidad).ToString("F2");
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

        // ─── Importar artículos ───────────────────────────────────────────────
        private void BtnImportarArticulos_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ArticulosGeneral(arts =>
            {
                foreach (var art in arts)
                {
                    // Evitar duplicados
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
            });
            dlg.ShowDialog();
        }

        // ─── Nueva línea vacía ────────────────────────────────────────────────
        private void BtnNuevaLinea_Click(object sender, RoutedEventArgs e)
        {
            _items.Add(new TraspasoItemFila
            {
                TraspasoId  = "",
                ArticuloId  = "",
                Codigo      = "",
                Descripcion = "",
                Cantidad    = 0
            });
            _hayCambios = true;
            RefrescarGrid();
        }

        // ─── Eliminar línea seleccionada ──────────────────────────────────────
        private void BtnEliminarLinea_Click(object sender, RoutedEventArgs e)
        {
            if (GridItems.SelectedItem is not TraspasoItemFila fila) return;
            _items.Remove(fila);
            _hayCambios = true;
            RefrescarGrid();
        }

        // ─── Edición de celda ─────────────────────────────────────────────────
        private void GridItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            _hayCambios = true;

            // Cuando se confirma la edición de la columna Código → buscar artículo
            if (e.EditAction == DataGridEditAction.Commit &&
                e.Column.Header?.ToString() == "Código" &&
                e.Row.Item is TraspasoItemFila fila &&
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

            Dispatcher.BeginInvoke(new Action(RefrescarGrid),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Seleccionar todo al entrar al campo Código ───────────────────────
        private void GridItems_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.Column.Header?.ToString() == "Código" && e.EditingElement is TextBox tb)
            {
                tb.SelectAll();
                tb.Focus();
            }
        }

        // ─── Insertar línea en la posición seleccionada ───────────────────────
        private void BtnInsertar_Click(object sender, RoutedEventArgs e)
        {
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
        {
            return AppState.EventoFormularioM == "editar"
                ? GuardarEditar()
                : GuardarNuevo();
        }

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

                DateTime fechaBase = Box_Fecha.SelectedDate ?? DateTime.Today;
                DateTime fechaFinal = CombinarFechaHora(fechaBase, Box_Hora.Text);

                string tipo  = AppState.TipoMovimiento.ToLower();
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

                // Crear líneas de traspaso
                long baseId = Convert.ToInt64(Sql.TraspasosObj.Maximo("id") ?? 0);
                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    long nuevaId = baseId + i + 1;
                    string idStr = nuevaId.ToString();
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
                DateTime fechaBase = Box_Fecha.SelectedDate ?? DateTime.Today;
                DateTime fechaFinal = CombinarFechaHora(fechaBase, Box_Hora.Text);

                string tipo    = AppState.TipoMovimiento.ToLower();
                string campOtro = tipo == "salida" ? "destino" : "origen";
                string estado  = (Box_Estado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "pendiente";

                Sql.DocumentosTObj.EstablecerItem("fecha",       docT, fechaFinal);
                Sql.DocumentosTObj.EstablecerItem(campOtro,      docT, Box_Sucursal_Identificador.Text.Trim());
                Sql.DocumentosTObj.EstablecerItem("estado",      docT, estado);
                Sql.DocumentosTObj.EstablecerItem("referencia",  docT, Box_Referencia.Text.Trim());
                Sql.DocumentosTObj.EstablecerItem("observacion", docT, Box_Observaciones.Text.Trim());
                Sql.DocumentosTObj.EstablecerItem("edicion",     docT, DateTime.Now);

                // Ocultar líneas existentes
                int uf = Sql.TraspasosObj.ContarFilas;
                var idsOcultar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.TraspasosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() == docT)
                        idsOcultar.Add(id);
                }
                foreach (var id in idsOcultar)
                    Sql.TraspasosObj.Ocultar(id);

                // Re-crear líneas desde _items
                long baseId = Convert.ToInt64(Sql.TraspasosObj.Maximo("id") ?? 0);
                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    long nuevaId = baseId + i + 1;
                    string idStr = nuevaId.ToString();
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

        // ─── Combinar fecha + hora de texto ───────────────────────────────────
        private static DateTime CombinarFechaHora(DateTime fecha, string horaTexto)
        {
            if (TimeSpan.TryParse(horaTexto, out var ts))
                return fecha.Date + ts;
            return fecha.Date;
        }

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            bool ok = Guardar();
            if (ok) Close();
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

    // ─── Modelo de fila para el grid de artículos ─────────────────────────────
    public class TraspasoItemFila
    {
        public string TraspasoId  { get; set; } = "";
        public int    Linea       { get; set; }
        public string ArticuloId  { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Cantidad    { get; set; }
    }
}
