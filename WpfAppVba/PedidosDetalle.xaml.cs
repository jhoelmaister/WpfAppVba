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
    public partial class PedidosDetalle : Window
    {
        private static SqlData Sql => SqlData.Instance;
        private readonly PedidosGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;
        private List<PedidoItemFila> _items = new();

        public PedidosDetalle(PedidosGeneral? padre = null, string idEditar = "")
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
            LblTitulo.Text = tipo == "venta" ? "Registro de Venta" : "Registro de Compra";

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
            Box_DocumentoP.IsEnabled = false;
            Box_DocumentoP.Text = _idEditar;

            var fechaObj = Sql.DocumentosPObj.ObtenerItem("fecha", _idEditar);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : DateTime.Now;
            Box_Fecha.SelectedDate = fecha.Date;
            Box_Hora.Text = fecha.ToString("HH:mm");

            string terceroId = Sql.DocumentosPObj.ObtenerItem("tercero", _idEditar)?.ToString() ?? "";
            Box_Tercero_Identificador.Text = terceroId;
            ActualizarDescripcionTercero();

            string estadoVal = Sql.DocumentosPObj.ObtenerItem("estado", _idEditar)?.ToString() ?? "pendiente";
            SeleccionarEstado(estadoVal);

            Box_Observaciones.Text = Sql.DocumentosPObj.ObtenerItem("observacion", _idEditar)?.ToString() ?? "";

            // Cargar artículos
            _items.Clear();
            int linea = 1;
            int uf = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() != _idEditar) continue;

                string artId  = Sql.PedidosObj.ObtenerItem("articulo",  id)?.ToString() ?? "";
                string codigo = Sql.ArticulosObj.ObtenerItem("codigo",  artId)?.ToString() ?? "";
                double cant   = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad", id) ?? 0);
                double precio = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("precio",   id) ?? 0);
                string desc   = ObtenerDescripcionArticulo(artId);

                _items.Add(new PedidoItemFila
                {
                    PedidoId    = id,
                    Linea       = linea++,
                    ArticuloId  = artId,
                    Codigo      = codigo,
                    Descripcion = desc,
                    Cantidad    = cant,
                    Precio      = precio
                });
            }
            RefrescarGrid();
        }

        // ─── Modo nuevo ───────────────────────────────────────────────────────
        private void CargarParaNuevo()
        {
            Box_DocumentoP.IsEnabled = true;
            long siguiente = Convert.ToInt64(Sql.DocumentosPObj.Maximo("id") ?? 0) + 1;
            Box_DocumentoP.Text = siguiente.ToString();
            Box_Fecha.SelectedDate = DateTime.Today;
            Box_Hora.Text = DateTime.Now.ToString("HH:mm");
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

        private void ActualizarDescripcionTercero()
        {
            string id = Box_Tercero_Identificador.Text.Trim();
            Box_Tercero_Descripcion.Text = Sql.TercerosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
        }

        private static string ObtenerDescripcionArticulo(string artId)
        {
            string desc    = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
            string famId   = Sql.ArticulosObj.ObtenerItem("familia",     artId)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
            string modelo  = Sql.ArticulosObj.ObtenerItem("modelo",      artId)?.ToString() ?? "";
            return FuncionesComunes.UnirVariables(desc, famDesc, modelo);
        }

        private void RefrescarGrid()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].Linea = i + 1;
            GridItems.ItemsSource = null;
            GridItems.ItemsSource = _items;
            TxtTotalCantidad.Text = _items.Sum(x => x.Cantidad).ToString("F2");
            TxtTotalImporte.Text  = _items.Sum(x => x.Total).ToString("F2");
        }

        // ─── Búsqueda de precio para un artículo ─────────────────────────────
        private double ObtenerPrecioArticulo(string artId)
        {
            double precio = 0;
            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;
            int ufp = Sql.PreciosObj.ContarFilas;
            for (int p = 1; p <= ufp; p++)
            {
                var pid = Sql.PreciosObj.Mover(p)?.ToString();
                if (pid == null) continue;
                if (Sql.PreciosObj.ObtenerItem("articulo", pid)?.ToString() != artId) continue;
                var fechaPrecioObj = Sql.PreciosObj.ObtenerItem("fecha", pid);
                if (fechaPrecioObj == null) continue;
                DateTime fechaPrecio = Convert.ToDateTime(fechaPrecioObj);
                if (fechaPrecio <= fechaDoc)
                    precio = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", pid) ?? 0);
            }
            return precio;
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

        private void Box_Tercero_Identificador_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando)
            {
                ActualizarDescripcionTercero();
                _hayCambios = true;
            }
        }

        private void Box_Numeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        // ─── Buscar tercero ───────────────────────────────────────────────────
        private void BtnBuscarTercero_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new TercerosGeneral();
            dlg.ShowDialog();
            // TercerosGeneral no tiene modo selector aún; el usuario puede cerrar
            // y tipear el código manualmente. Extensible si se añade modo selector.
        }

        // ─── Importar artículos ───────────────────────────────────────────────
        private void BtnImportarArticulos_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ArticulosGeneral(arts =>
            {
                foreach (var art in arts)
                {
                    if (_items.Any(x => x.ArticuloId == art.Id)) continue;
                    double precio = ObtenerPrecioArticulo(art.Id);
                    _items.Add(new PedidoItemFila
                    {
                        PedidoId    = "",
                        ArticuloId  = art.Id,
                        Codigo      = art.Codigo,
                        Descripcion = art.Descripcion,
                        Cantidad    = 1,
                        Precio      = precio
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
            _items.Add(new PedidoItemFila
            {
                PedidoId    = "",
                ArticuloId  = "",
                Codigo      = "",
                Descripcion = "",
                Cantidad    = 0,
                Precio      = 0
            });
            _hayCambios = true;
            RefrescarGrid();
        }

        // ─── Eliminar línea seleccionada ──────────────────────────────────────
        private void BtnEliminarLinea_Click(object sender, RoutedEventArgs e)
        {
            if (GridItems.SelectedItem is not PedidoItemFila fila) return;
            _items.Remove(fila);
            _hayCambios = true;
            RefrescarGrid();
        }

        // ─── Edición de celda ─────────────────────────────────────────────────
        private void GridItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            _hayCambios = true;
            Dispatcher.BeginInvoke(new Action(RefrescarGrid),
                System.Windows.Threading.DispatcherPriority.Background);
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
            string docP = Box_DocumentoP.Text.Trim();
            if (string.IsNullOrEmpty(docP))
            {
                MessageBox.Show("Ingrese el número de documento.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                if (!Sql.DocumentosPObj.VerificarId(docP, "id"))
                {
                    MessageBox.Show("El número de documento ya existe.", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                DateTime fechaBase  = Box_Fecha.SelectedDate ?? DateTime.Today;
                DateTime fechaFinal = CombinarFechaHora(fechaBase, Box_Hora.Text);
                string estado       = (Box_Estado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "pendiente";

                Sql.DocumentosPObj.Nuevo(docP);
                Sql.DocumentosPObj.EstablecerItem("sucursal",    docP, AppState.SucursalActiva);
                Sql.DocumentosPObj.EstablecerItem("tercero",     docP, Box_Tercero_Identificador.Text.Trim());
                Sql.DocumentosPObj.EstablecerItem("fecha",       docP, fechaFinal);
                Sql.DocumentosPObj.EstablecerItem("movimiento",  docP, AppState.TipoMovimiento);
                Sql.DocumentosPObj.EstablecerItem("tipo",        docP, "rapido");
                Sql.DocumentosPObj.EstablecerItem("estado",      docP, estado);
                Sql.DocumentosPObj.EstablecerItem("observacion", docP, Box_Observaciones.Text.Trim());
                Sql.DocumentosPObj.EstablecerItem("emision",     docP, DateTime.Now);
                Sql.DocumentosPObj.EstablecerItem("edicion",     docP, DateTime.Now);
                Sql.DocumentosPObj.EstablecerItem("usuario",     docP, AppState.UsuarioActivo);

                // Crear líneas de pedido
                long baseId = Convert.ToInt64(Sql.PedidosObj.Maximo("id") ?? 0);
                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    long nuevaId = baseId + i + 1;
                    string idStr = nuevaId.ToString();
                    Sql.PedidosObj.Nuevo(idStr);
                    Sql.PedidosObj.EstablecerItem("documentoP", idStr, docP);
                    Sql.PedidosObj.EstablecerItem("articulo",   idStr, item.ArticuloId);
                    Sql.PedidosObj.EstablecerItem("cantidad",   idStr, item.Cantidad);
                    Sql.PedidosObj.EstablecerItem("precio",     idStr, item.Precio);
                    Sql.PedidosObj.EstablecerItem("indice",     idStr, i + 1);
                }

                Sql.DocumentosPObj.OrdenarData(("fecha", false));
                Sql.PedidosObj.OrdenarData(("documentoP", false), ("indice", false));

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
            string docP = _idEditar;
            try
            {
                DateTime fechaBase  = Box_Fecha.SelectedDate ?? DateTime.Today;
                DateTime fechaFinal = CombinarFechaHora(fechaBase, Box_Hora.Text);
                string estado       = (Box_Estado.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "pendiente";

                Sql.DocumentosPObj.EstablecerItem("tercero",     docP, Box_Tercero_Identificador.Text.Trim());
                Sql.DocumentosPObj.EstablecerItem("fecha",       docP, fechaFinal);
                Sql.DocumentosPObj.EstablecerItem("estado",      docP, estado);
                Sql.DocumentosPObj.EstablecerItem("observacion", docP, Box_Observaciones.Text.Trim());
                Sql.DocumentosPObj.EstablecerItem("edicion",     docP, DateTime.Now);

                // Ocultar líneas existentes
                int uf = Sql.PedidosObj.ContarFilas;
                var idsOcultar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.PedidosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() == docP)
                        idsOcultar.Add(id);
                }
                foreach (var id in idsOcultar)
                    Sql.PedidosObj.Ocultar(id);

                // Re-crear líneas desde _items
                long baseId = Convert.ToInt64(Sql.PedidosObj.Maximo("id") ?? 0);
                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    long nuevaId = baseId + i + 1;
                    string idStr = nuevaId.ToString();
                    Sql.PedidosObj.Nuevo(idStr);
                    Sql.PedidosObj.EstablecerItem("documentoP", idStr, docP);
                    Sql.PedidosObj.EstablecerItem("articulo",   idStr, item.ArticuloId);
                    Sql.PedidosObj.EstablecerItem("cantidad",   idStr, item.Cantidad);
                    Sql.PedidosObj.EstablecerItem("precio",     idStr, item.Precio);
                    Sql.PedidosObj.EstablecerItem("indice",     idStr, i + 1);
                }

                Sql.DocumentosPObj.OrdenarData(("fecha", false));
                Sql.PedidosObj.OrdenarData(("documentoP", false), ("indice", false));

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
    public class PedidoItemFila
    {
        public string PedidoId    { get; set; } = "";
        public int    Linea       { get; set; }
        public string ArticuloId  { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Cantidad    { get; set; }
        public double Precio      { get; set; }
        public double Total       => Cantidad * Precio;
    }
}
