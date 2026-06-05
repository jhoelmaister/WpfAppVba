using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class PedidosDetalle : UserControl
    {
        public event Action? Cerrando;
        private static SqlData Sql => SqlData.Instance;
        private readonly PedidosGeneral? _padre;
        private readonly string _idEditar;

        private bool _cargando         = true;
        private bool _cambioDocumento  = false;
        private bool _cambioPedido     = false;
        private bool _cambioTrasaccion = false;
        private bool _cambioEntrega    = false;

        private List<PedidoItemFila>     _pedidos       = new();
        private List<TrasaccionItemFila> _trasacciones  = new();
        private List<EntregaItemFila>    _entregas      = new();

        private readonly HashSet<string> _articulosAlertados = new();
        private string                   _observaciones = "";

        public string? DocumentoCreadoId { get; private set; }

        // Listas estáticas para ComboBox dentro de DataGrid
        public static List<string> FormasPedido     = new() { "sin factura", "con factura" };
        public static List<string> FormasTrasaccion = new() { "cheque", "efectivo", "transferencia", "pago Qr" };

        private bool HayCambios => _cambioDocumento || _cambioPedido || _cambioTrasaccion || _cambioEntrega;

        private bool _iniciado = false;
        private readonly string _tituloTab;

        public PedidosDetalle(PedidosGeneral? padre = null, string idEditar = "", string tituloTab = "")
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

            string tipo = AppState.TipoMovimiento.ToLower();
            LblTitulo.Text = tipo == "venta" ? "Venta de Productos" : "Compra de Productos";
            CboMovimiento.SelectedIndex = tipo == "compra" ? 1 : 0;

            // Etiquetas dinámicas según tipo
            if (TabCobros != null)
                TabCobros.Header = tipo == "venta" ? "Cobros" : "Pagos";
            BtnCobrarDocumento.Content = tipo == "venta" ? "Cobrar Documento" : "Pagar Documento";

            // tipoPedido = "rapido" → ocultar tabs de cobros y entregas
            string tipoPedido = AppState.TipoPedido.ToLower();
            if (tipoPedido == "rapido")
            {
                if (TabCobros   != null) TabCobros.Visibility   = Visibility.Collapsed;
                if (TabEntregas != null) TabEntregas.Visibility = Visibility.Collapsed;
            }

            if (AppState.EventoFormularioM == "editar")
                CargarParaEditar();
            else
                CargarParaNuevo();

            _cargando        = false;
            _cambioDocumento = false;
            _cambioPedido    = false;
            _cambioTrasaccion= false;
            _cambioEntrega   = false;
        }

        // ─── Modo nuevo ───────────────────────────────────────────────────────
        private void CargarParaNuevo()
        {
            Box_DocumentoP.IsEnabled = true;
            long siguiente = Convert.ToInt64(Sql.DocumentosPObj.Maximo("id") ?? 0) + 1;
            Box_DocumentoP.Text = siguiente.ToString();

            string tipoPedido = AppState.TipoPedido.ToLower();
            DateTime ahora = DateTime.Now;
            bool mismoAnio = int.TryParse(AppState.PeriodoActivo, out int periodo) && periodo == ahora.Year;

            Box_Fecha.SelectedDate = mismoAnio ? ahora.Date : AppState.DataFechaFinal.Date;
            Box_Hora.Text          = mismoAnio ? ahora.ToString("HH:mm:ss") : "23:59:59";

            // Pedido nuevo sin líneas ni entregas -> nada pendiente -> estado "entregado".
            string cuentaInicial = tipoPedido == "rapido" ? "cancelado" : "pendiente";
            SeleccionarEstado("entregado");
            Box_Cuenta.Text  = cuentaInicial;
            Box_Emision.Text = $"{ahora:d} {ahora:HH:mm:ss}";
            Box_Edicion.Text = $"{ahora:d} {ahora:HH:mm:ss}";

            _observaciones = "";
            Box_Observaciones.Text = "";

            _pedidos.Clear();
            _trasacciones.Clear();
            _entregas.Clear();
            RefrescarGridPedidos();
            RefrescarGridTrasacciones();
            RefrescarGridEntregas();
            ActualizarTotales();
        }

        // ─── Modo editar ──────────────────────────────────────────────────────
        private void CargarParaEditar()
        {
            Box_DocumentoP.IsEnabled = false;
            Box_DocumentoP.Text = _idEditar;

            var fechaObj = Sql.DocumentosPObj.ObtenerItem("fecha", _idEditar);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : DateTime.Now;
            Box_Fecha.SelectedDate = fecha.Date;
            Box_Hora.Text = fecha.ToString("HH:mm:ss");

            string terceroId = Sql.DocumentosPObj.ObtenerItem("tercero", _idEditar)?.ToString() ?? "";
            Box_Tercero_Identificador.Text = terceroId;
            ActualizarDescripcionTercero();

            string estadoVal = Sql.DocumentosPObj.ObtenerItem("estado", _idEditar)?.ToString() ?? "pendiente";
            SeleccionarEstado(estadoVal);

            Box_Referencia.Text  = Sql.DocumentosPObj.ObtenerItem("referencia",  _idEditar)?.ToString() ?? "";
            _observaciones = Sql.DocumentosPObj.ObtenerItem("observacion", _idEditar)?.ToString() ?? "";
            Box_Observaciones.Text = _observaciones;
            Box_Cuenta.Text      = Sql.DocumentosPObj.ObtenerItem("estadoC",     _idEditar)?.ToString() ?? "";

            var emisionObj = Sql.DocumentosPObj.ObtenerItem("emision", _idEditar);
            var edicionObj = Sql.DocumentosPObj.ObtenerItem("edicion", _idEditar);
            Box_Emision.Text = emisionObj != null ? $"{Convert.ToDateTime(emisionObj):d} {Convert.ToDateTime(emisionObj):HH:mm:ss}" : "";
            Box_Edicion.Text = edicionObj != null ? $"{Convert.ToDateTime(edicionObj):d} {Convert.ToDateTime(edicionObj):HH:mm:ss}" : "";

            AppState.TipoPedido = Sql.DocumentosPObj.ObtenerItem("tipo", _idEditar)?.ToString() ?? "rapido";

            // Cargar pedidos
            _pedidos.Clear();
            int linea = 1;
            int uf = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() != _idEditar) continue;

                string artId   = Sql.PedidosObj.ObtenerItem("articulo",  id)?.ToString() ?? "";
                string codigo  = Sql.ArticulosObj.ObtenerItem("codigo",   artId)?.ToString() ?? "";
                double cant    = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad",  id) ?? 0);
                double importe = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("importe",   id) ?? 0);
                string forma   = Sql.PedidosObj.ObtenerItem("forma",     id)?.ToString() ?? "sin factura";
                double contable= Convert.ToDouble(Sql.PedidosObj.ObtenerItem("contable",  id) ?? 0);
                string tipo    = Sql.PedidosObj.ObtenerItem("tipo",      id)?.ToString() ?? "automatico";
                double precio  = cant > 0 ? importe / cant : 0;

                _pedidos.Add(new PedidoItemFila
                {
                    PedidoId    = id,
                    Linea       = linea++,
                    ArticuloId  = artId,
                    Codigo      = codigo,
                    Descripcion = ObtenerDescripcionArticulo(artId),
                    Cantidad    = cant,
                    Forma       = forma,
                    Contable    = contable,
                    Precio      = precio,
                    Importe     = importe,
                    Tipo        = tipo
                });
            }

            // Cargar trasacciones
            _trasacciones.Clear();
            int ufT = Sql.TrasaccionesObj.ContarFilas;
            int lineaT = 1;
            for (int i = 1; i <= ufT; i++)
            {
                var idObj = Sql.TrasaccionesObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.TrasaccionesObj.ObtenerItem("documentoP", id)?.ToString() != _idEditar) continue;

                var fObj = Sql.TrasaccionesObj.ObtenerItem("fecha", id);
                DateTime ft = fObj != null ? Convert.ToDateTime(fObj) : DateTime.Now;
                _trasacciones.Add(new TrasaccionItemFila
                {
                    TrasaccionId = id,
                    Linea        = lineaT++,
                    FechaStr     = ft.ToString("d"),
                    FechaDate    = ft.Date,
                    HoraStr      = ft.ToString("HH:mm:ss"),
                    Descripcion  = Sql.TrasaccionesObj.ObtenerItem("descripcion", id)?.ToString() ?? "",
                    Forma        = Sql.TrasaccionesObj.ObtenerItem("forma",       id)?.ToString() ?? "efectivo",
                    Importe      = Convert.ToDouble(Sql.TrasaccionesObj.ObtenerItem("importe", id) ?? 0)
                });
            }

            // Cargar entregas
            _entregas.Clear();
            int ufE = Sql.EntregasObj.ContarFilas;
            int lineaE = 1;
            for (int i = 1; i <= ufE; i++)
            {
                var idObj = Sql.EntregasObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.EntregasObj.ObtenerItem("documentoP", id)?.ToString() != _idEditar) continue;

                string artId  = Sql.EntregasObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                string codigo = Sql.ArticulosObj.ObtenerItem("codigo",  artId)?.ToString() ?? "";
                var fObj = Sql.EntregasObj.ObtenerItem("fecha", id);
                DateTime fe = fObj != null ? Convert.ToDateTime(fObj) : DateTime.Now;
                _entregas.Add(new EntregaItemFila
                {
                    EntregaId   = id,
                    Linea       = lineaE++,
                    ArticuloId  = artId,
                    Codigo      = codigo,
                    Descripcion = ObtenerDescripcionArticulo(artId),
                    Cantidad    = Convert.ToDouble(Sql.EntregasObj.ObtenerItem("cantidad", id) ?? 0),
                    FechaStr    = fe.ToString("d"),
                    FechaDate   = fe.Date,
                    HoraStr     = fe.ToString("HH:mm:ss")
                });
            }

            RefrescarGridPedidos();
            RefrescarGridTrasacciones();
            RefrescarGridEntregas();
            ActualizarTotales();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private void SeleccionarEstado(string valor) => Box_Estado.Text = valor;

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

        private double ObtenerPrecioArticulo(string artId)
        {
            double precio = 0;
            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;
            int uf = Sql.PreciosObj.ContarFilas;
            for (int p = 1; p <= uf; p++)
            {
                var pid = Sql.PreciosObj.Mover(p)?.ToString();
                if (pid == null) continue;
                if (Sql.PreciosObj.ObtenerItem("articulo", pid)?.ToString() != artId) continue;
                var fp = Sql.PreciosObj.ObtenerItem("fecha", pid);
                if (fp == null) continue;
                if (Convert.ToDateTime(fp) <= fechaDoc)
                    precio = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", pid) ?? 0);
            }
            return precio;
        }

        private static DateTime CombinarFechaHora(DateTime fecha, string horaTexto)
        {
            if (TimeSpan.TryParse(horaTexto, out var ts))
                return fecha.Date + ts;
            return fecha.Date;
        }

        // ─── Refrescar grids (preserva selección) ────────────────────────────
        private void RefrescarGridPedidos()
        {
            for (int i = 0; i < _pedidos.Count; i++) _pedidos[i].Linea = i + 1;
            var sel = GridItems.SelectedItem as PedidoItemFila;
            GridItems.ItemsSource = null;
            GridItems.ItemsSource = _pedidos;
            if (sel != null && _pedidos.Contains(sel)) GridItems.SelectedItem = sel;
            CargarStockYPrecios(GridItems.SelectedItem as PedidoItemFila);
        }

        private void RefrescarGridTrasacciones()
        {
            for (int i = 0; i < _trasacciones.Count; i++) _trasacciones[i].Linea = i + 1;
            var sel = GridTrasacciones.SelectedItem as TrasaccionItemFila;
            GridTrasacciones.ItemsSource = null;
            GridTrasacciones.ItemsSource = _trasacciones;
            if (sel != null && _trasacciones.Contains(sel)) GridTrasacciones.SelectedItem = sel;
        }

        private void RefrescarGridEntregas()
        {
            for (int i = 0; i < _entregas.Count; i++) _entregas[i].Linea = i + 1;
            var sel = GridEntregas.SelectedItem as EntregaItemFila;
            GridEntregas.ItemsSource = null;
            GridEntregas.ItemsSource = _entregas;
            if (sel != null && _entregas.Contains(sel)) GridEntregas.SelectedItem = sel;
        }

        // ─── Totales ──────────────────────────────────────────────────────────
        private void ActualizarTotales()
        {
            CargarTotales();
            CargarTotalesDivisas();
            CargarEstadosCuenta();
            if (AppState.TipoPedido.ToLower() == "normal")
                CargarEstados();
        }

        private void CargarTotales()
        {
            double totalUnid = 0, totalPeq = 0, totalMed = 0, totalGra = 0, totalOtros = 0;
            var articulosUnicos = new HashSet<string>();

            foreach (var p in _pedidos)
            {
                if (!string.IsNullOrEmpty(p.ArticuloId)) articulosUnicos.Add(p.ArticuloId);
                totalUnid += p.Cantidad;

                string catId   = Sql.ArticulosObj.ObtenerItem("categoria", p.ArticuloId)?.ToString() ?? "";
                string catDesc = Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString()?.ToLower() ?? "";
                if (catDesc == "pequeña" || catDesc == "pequena")  totalPeq   += p.Cantidad;
                else if (catDesc == "mediana")                     totalMed   += p.Cantidad;
                else if (catDesc == "grande")                      totalGra   += p.Cantidad;
                else                                               totalOtros += p.Cantidad;
            }

            TxtTotalUnidades.Text        = totalUnid.ToString("N0");
            TxtUnidadesDiferentes.Text   = articulosUnicos.Count.ToString();
            TxtTotalPeq.Text             = totalPeq.ToString("N0");
            TxtTotalMed.Text             = totalMed.ToString("N0");
            TxtTotalGra.Text             = totalGra.ToString("N0");
            TxtTotalOtros.Text           = totalOtros.ToString("N0");
        }

        private void CargarTotalesDivisas()
        {
            double totalImporte = _pedidos.Sum(p => p.Importe);
            double totalCuenta;

            if (AppState.TipoPedido.ToLower() == "rapido")
                totalCuenta = totalImporte;
            else
                totalCuenta = _trasacciones.Sum(t => t.Importe);

            double saldo = totalImporte - totalCuenta;

            TxtTotalImporte.Text = totalImporte.ToString("N2");
            TxtTotalCuenta.Text  = totalCuenta.ToString("N2");
            TxtTotalSaldo.Text   = saldo.ToString("N2");
        }

        private void CargarEstadosCuenta()
        {
            if (!double.TryParse(TxtTotalImporte.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double importe)) return;
            if (!double.TryParse(TxtTotalCuenta.Text,  NumberStyles.Any, CultureInfo.CurrentCulture, out double cuenta))  return;

            string nuevaCuenta;
            if (importe > 0 && cuenta == 0)         nuevaCuenta = "pendiente";
            else if (cuenta > 0 && cuenta < importe) nuevaCuenta = "pendiente parcial";
            else if (cuenta >= importe)              nuevaCuenta = "cancelado";
            else                                     nuevaCuenta = "pendiente";

            bool prev = _cargando;
            _cargando = true;
            Box_Cuenta.Text = nuevaCuenta;
            _cargando = prev;
        }

        private void CargarEstados()
        {
            // Solo aplica en modo "normal"
            if (AppState.TipoPedido.ToLower() != "normal") return;
            if (_pedidos.Count == 0) return;

            // Agrupar cantidades de pedidos por artículo
            var cantPedido = _pedidos
                .Where(p => !string.IsNullOrEmpty(p.ArticuloId))
                .GroupBy(p => p.ArticuloId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

            // Agrupar cantidades de entregas por artículo
            var cantEntrega = _entregas
                .Where(e => !string.IsNullOrEmpty(e.ArticuloId))
                .GroupBy(e => e.ArticuloId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

            string estado = "pendiente";
            bool hayEntregas = _entregas.Count > 0;

            foreach (var kv in cantPedido)
            {
                double entregado = cantEntrega.TryGetValue(kv.Key, out double val) ? val : 0;
                if (entregado < kv.Value && hayEntregas)
                { estado = "entrega parcial"; break; }
                if (entregado >= kv.Value)
                    estado = "entregado";
            }

            bool prev = _cargando;
            _cargando = true;
            SeleccionarEstado(estado);
            _cargando = prev;
        }

        // ─── Stock y precios del artículo seleccionado ────────────────────────
        private void CargarStockYPrecios(PedidoItemFila? fila)
        {
            GridPrecios.ItemsSource = null;
            GridStock.ItemsSource   = null;

            if (fila == null || string.IsNullOrEmpty(fila.ArticuloId)) return;

            // Precios
            var precios = new List<PrecioFila>();
            int uf = Sql.PreciosObj.ContarFilas;
            for (int i = uf; i >= 1; i--)
            {
                var pid = Sql.PreciosObj.Mover(i)?.ToString();
                if (pid == null) continue;
                if (Sql.PreciosObj.ObtenerItem("articulo", pid)?.ToString() != fila.ArticuloId) continue;
                var fp = Sql.PreciosObj.ObtenerItem("fecha", pid);
                precios.Add(new PrecioFila
                {
                    Codigo = fila.Codigo,
                    Fecha  = fp != null ? Convert.ToDateTime(fp).ToString("d") : "",
                    Precio = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", pid) ?? 0).ToString("N2")
                });
            }
            GridPrecios.ItemsSource = precios;

            // Stock
            double stockTotal = StockCalculator.ContarStock(fila.ArticuloId, AppState.DataFechaFinal);
            double stockDisp  = StockCalculator.ContarStock(fila.ArticuloId, DateTime.Now);
            GridStock.ItemsSource = new List<StockFila>
            {
                new() { Codigo = fila.Codigo, Disponible = stockDisp.ToString("N0"), Stock = stockTotal.ToString("N0") }
            };
        }

        // ─── Notificación de stock insuficiente (solo ventas, modo nuevo) ─────
        private void VerificarStockVenta()
        {
            if (AppState.TipoMovimiento.ToLower() != "venta") return;
            if (AppState.EventoFormularioM != "nuevo") return;

            foreach (var item in _pedidos)
            {
                if (string.IsNullOrEmpty(item.ArticuloId)) continue;
                if (_articulosAlertados.Contains(item.ArticuloId)) continue;

                double totalCant = _pedidos.Where(x => x.ArticuloId == item.ArticuloId)
                                            .Sum(x => x.Cantidad);
                double stock = StockCalculator.ContarStock(item.ArticuloId, AppState.DataFechaFinal);

                if (stock < totalCant)
                {
                    _articulosAlertados.Add(item.ArticuloId);
                    MessageBox.Show($"{item.Descripcion}: stock insuficiente (disponible: {stock:F0}, solicitado: {totalCant:F0}).",
                        "Consola", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // ─── Cambio de tipo de movimiento (ComboBox superior) ────────────────
        private void CboMovimiento_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargando) return;
            string tipo = (CboMovimiento.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "venta";
            AppState.TipoMovimiento = tipo;
            LblTitulo.Text = tipo == "venta" ? "Venta de Productos" : "Compra de Productos";
            if (TabCobros != null)
                TabCobros.Header = tipo == "venta" ? "Cobros" : "Pagos";
            BtnCobrarDocumento.Content = tipo == "venta" ? "Cobrar Documento" : "Pagar Documento";
            _cambioDocumento = true;
        }

        // ─── Eventos de campos del encabezado ─────────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _cambioDocumento = true;
        }

        private void Campo_DateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Sincronizar el TextBox visible cuando el código asigna Box_Fecha.SelectedDate
            if (Box_FechaTexto != null && Box_Fecha.SelectedDate.HasValue)
                Box_FechaTexto.Text = Box_Fecha.SelectedDate.Value.ToString("dd/MM/yyyy");
            if (!_cargando) _cambioDocumento = true;
        }

        // ─── Calendario popup para la fecha del pedido ────────────────────────
        private void BtnCalendario_Click(object sender, RoutedEventArgs e)
        {
            if (Box_Fecha.SelectedDate.HasValue)
                CalendarioFecha.SelectedDate = Box_Fecha.SelectedDate.Value;
            PopupCalendario.IsOpen = !PopupCalendario.IsOpen;
        }

        private void CalendarioFecha_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CalendarioFecha.SelectedDate.HasValue)
            {
                DateTime fecha = CalendarioFecha.SelectedDate.Value;
                Box_FechaTexto.Text = fecha.ToString("dd/MM/yyyy");
                bool prev = _cargando;
                _cargando = true;
                Box_Fecha.SelectedDate = fecha;
                _cargando = prev;
                _cambioDocumento = true;
            }
            PopupCalendario.IsOpen = false;
        }

        private void Box_FechaTexto_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DateTime.TryParseExact(Box_FechaTexto.Text.Trim(), "dd/MM/yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime fecha))
            {
                bool prev = _cargando;
                _cargando = true;
                Box_Fecha.SelectedDate = fecha;
                _cargando = prev;
                _cambioDocumento = true;
            }
            else if (!string.IsNullOrEmpty(Box_FechaTexto.Text) && Box_Fecha.SelectedDate.HasValue)
            {
                Box_FechaTexto.Text = Box_Fecha.SelectedDate.Value.ToString("dd/MM/yyyy");
            }
        }

        private void Box_Hora_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _cambioDocumento = true;
        }

        private void Campo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _cambioDocumento = true;
        }

        private void Box_Tercero_Identificador_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando)
            {
                ActualizarDescripcionTercero();
                _cambioDocumento = true;
            }
        }

        private void Box_Observaciones_TextChanged(object sender, TextChangedEventArgs e)
        {
            _observaciones = Box_Observaciones.Text;
            if (!_cargando) _cambioDocumento = true;
        }

        private void Box_Numeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        // ─── Buscar tercero ───────────────────────────────────────────────────
        private void BtnBuscarTercero_Click(object sender, RoutedEventArgs e)
        {
            TercerosGeneral.TerceroSeleccionado = null;
            TercerosGeneral.OpenAsDialog(Window.GetWindow(this)!, modoSelector: true, contexto: _tituloTab, llamador: this, onCerrado: () =>
            {
                if (!string.IsNullOrEmpty(TercerosGeneral.TerceroSeleccionado))
                    Box_Tercero_Identificador.Text = TercerosGeneral.TerceroSeleccionado;
            });
        }

        // ─── Selección en grid de pedidos ─────────────────────────────────────
        private void GridItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CargarStockYPrecios(GridItems.SelectedItem as PedidoItemFila);
        }

        // ─── Doble clic en GridPrecios → cargar precio en fila seleccionada ──
        private void GridPrecios_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridPrecios.SelectedItem is not PrecioFila precioFila) return;
            if (GridItems.SelectedItem  is not PedidoItemFila itemFila) return;
            if (!double.TryParse(precioFila.Precio, NumberStyles.Any, CultureInfo.CurrentCulture, out double precio)) return;
            itemFila.Precio  = precio;
            itemFila.Importe = Math.Round(itemFila.Cantidad * precio, 2);
            _cambioPedido = true;
            Dispatcher.BeginInvoke(() => { RefrescarGridPedidos(); ActualizarTotales(); });
        }

        // ─── CellEditEnding – Pedidos ─────────────────────────────────────────
        private void GridItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.Item is not PedidoItemFila fila) return;

            string col = e.Column.Header?.ToString() ?? "";

            if (col == "Código" && e.EditingElement is TextBox tbCod)
            {
                string codigo = tbCod.Text.Trim();
                long artIdNum = Sql.ArticulosObj.BuscarIdentificador("codigo", codigo);
                if (artIdNum > 0)
                {
                    string artId = artIdNum.ToString();
                    fila.ArticuloId  = artId;
                    fila.Codigo      = codigo;
                    fila.Descripcion = ObtenerDescripcionArticulo(artId);
                    double precio    = ObtenerPrecioArticulo(artId);
                    fila.Precio      = precio;
                    fila.Importe     = precio * fila.Cantidad;
                    fila.Tipo        = "automatico";
                }
                else
                {
                    fila.ArticuloId  = "";
                    fila.Codigo      = codigo;
                    fila.Descripcion = string.IsNullOrEmpty(codigo) ? "" : "⚠ Artículo no encontrado";
                    fila.Precio      = 0;
                    fila.Importe     = 0;
                }
            }
            else if (col == "Cantidad" && e.EditingElement is TextBox tbCant)
            {
                if (double.TryParse(tbCant.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double cant))
                {
                    fila.Cantidad = cant;
                    fila.Importe  = cant * fila.Precio;
                }
            }
            else if (col == "Precio" && e.EditingElement is TextBox tbPrecio)
            {
                if (double.TryParse(tbPrecio.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double precio))
                {
                    fila.Precio  = precio;
                    fila.Importe = fila.Cantidad * precio;
                    fila.Tipo    = "manual";
                }
            }
            else if (col == "Importe" && e.EditingElement is TextBox tbImp)
            {
                if (double.TryParse(tbImp.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double importe))
                {
                    fila.Importe = importe;
                    fila.Precio  = fila.Cantidad > 0 ? importe / fila.Cantidad : 0;
                    fila.Tipo    = "manual";
                }
            }
            else if (col == "Forma")
            {
                if (fila.Forma == "sin factura")
                    fila.Contable = 0;
                else if (fila.Forma == "con factura")
                    fila.Contable = 100;
            }
            else if (col == "Contable" && e.EditingElement is TextBox tbCont)
            {
                if (double.TryParse(tbCont.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double cont))
                    fila.Contable = cont;
            }

            _cambioPedido = true;
            string colNombre = col;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefrescarGridPedidos();
                ActualizarTotales();
                if (colNombre == "Código" || colNombre == "Cantidad")
                    VerificarStockVenta();
                GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void GridItems_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is TextBox tb) { tb.SelectAll(); tb.Focus(); }
        }

        private void GridItems_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Column.Header?.ToString() == "Contable" &&
                e.Row.Item is PedidoItemFila fila &&
                fila.Forma == "sin factura")
            {
                e.Cancel = true;
            }
        }

        // ─── Botones Pedidos ──────────────────────────────────────────────────
        private void BtnImportarArticulos_Click(object sender, RoutedEventArgs e)
        {
            ArticulosGeneral.OpenAsTab(Window.GetWindow(this)!, arts =>
            {
                foreach (var art in arts)
                {
                    double precio = ObtenerPrecioArticulo(art.Id);
                    _pedidos.Add(new PedidoItemFila
                    {
                        PedidoId    = "", ArticuloId  = art.Id,
                        Codigo      = art.Codigo, Descripcion = art.Descripcion,
                        Cantidad    = 1, Forma = "sin factura", Contable = 0,
                        Precio      = precio, Importe = precio, Tipo = "automatico"
                    });
                }
                _cambioPedido = true;
                RefrescarGridPedidos();
                ActualizarTotales();
                VerificarStockVenta();
                if (_pedidos.Count > 0)
                {
                    var ultimo = _pedidos[_pedidos.Count - 1];
                    GridItems.SelectedItem = ultimo;
                    GridItems.ScrollIntoView(ultimo);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
            }, null, contexto: _tituloTab, llamador: this);
        }

        // ─── Buscar artículo individual (doble clic en ArticulosGeneral) ────────
        private void BtnBuscarArticulo_Click(object sender, RoutedEventArgs e)
        {
            // Guardar referencia a la fila actualmente seleccionada
            var filaActual = GridItems.SelectedItem as PedidoItemFila;

            ArticulosGeneral.OpenAsTab(Window.GetWindow(this)!, null, art =>
            {
                double precio = ObtenerPrecioArticulo(art.Id);
                PedidoItemFila filaEnfocar;

                if (filaActual != null && _pedidos.Contains(filaActual))
                {
                    // Llenar la fila seleccionada con el artículo buscado
                    filaActual.ArticuloId  = art.Id;
                    filaActual.Codigo      = art.Codigo;
                    filaActual.Descripcion = art.Descripcion;
                    filaActual.Precio      = precio;
                    filaActual.Importe     = precio * filaActual.Cantidad;
                    filaActual.Tipo        = "automatico";
                    filaEnfocar = filaActual;
                }
                else
                {
                    // Sin fila seleccionada → agregar nueva línea
                    var nueva = new PedidoItemFila
                    {
                        PedidoId    = "", ArticuloId  = art.Id,
                        Codigo      = art.Codigo, Descripcion = art.Descripcion,
                        Cantidad    = 1, Forma = "sin factura", Contable = 0,
                        Precio      = precio, Importe = precio, Tipo = "automatico"
                    };
                    _pedidos.Add(nueva);
                    filaEnfocar = nueva;
                }
                _cambioPedido = true;
                RefrescarGridPedidos();
                ActualizarTotales();
                VerificarStockVenta();

                // Enfocar columna Cantidad de la fila e iniciar edición
                EnfocarColumnaCantidad(filaEnfocar);
            }, contexto: _tituloTab, llamador: this);
        }

        // Posiciona el cursor en la celda Cantidad de la fila indicada e inicia edición
        private void EnfocarColumnaCantidad(PedidoItemFila fila)
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

        private void BtnNuevaLinea_Click(object sender, RoutedEventArgs e)
        {
            _pedidos.Add(new PedidoItemFila
            {
                PedidoId = "", ArticuloId = "", Codigo = "", Descripcion = "",
                Cantidad = 1, Forma = "sin factura", Contable = 0, Precio = 0, Importe = 0
            });
            _cambioPedido = true;
            RefrescarGridPedidos();
            ActualizarTotales();
            int lastIdx = GridItems.Items.Count - 1;
            if (lastIdx >= 0)
            {
                GridItems.SelectedIndex = lastIdx;
                GridItems.ScrollIntoView(GridItems.SelectedItem);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
        }

        private void BtnInsertar_Click(object sender, RoutedEventArgs e)
        {
            int idx = GridItems.SelectedItem is PedidoItemFila sel
                      ? _pedidos.IndexOf(sel) : _pedidos.Count;
            if (idx < 0) idx = _pedidos.Count;
            _pedidos.Insert(idx, new PedidoItemFila
            {
                PedidoId = "", ArticuloId = "", Codigo = "", Descripcion = "",
                Cantidad = 1, Forma = "sin factura", Contable = 0, Precio = 0, Importe = 0
            });
            _cambioPedido = true;
            RefrescarGridPedidos();
            if (idx < GridItems.Items.Count)
            { GridItems.SelectedIndex = idx; GridItems.ScrollIntoView(GridItems.SelectedItem); }
            ActualizarTotales();
            GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
        }

        private void BtnEliminarLinea_Click(object sender, RoutedEventArgs e)
        {
            if (GridItems.SelectedItem is not PedidoItemFila fila) return;
            int idx = _pedidos.IndexOf(fila);
            _pedidos.Remove(fila);
            _cambioPedido = true;
            RefrescarGridPedidos();
            ActualizarTotales();
            if (_pedidos.Count > 0)
            {
                var siguiente = _pedidos[Math.Min(idx, _pedidos.Count - 1)];
                GridItems.SelectedItem = siguiente;
                GridItems.ScrollIntoView(siguiente);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridItems);
        }

        private void BtnActualizarPrecios_Click(object sender, RoutedEventArgs e)
        {
            foreach (var p in _pedidos)
            {
                if (string.IsNullOrEmpty(p.ArticuloId)) continue;
                double precio = ObtenerPrecioArticulo(p.ArticuloId);
                p.Precio  = precio;
                p.Importe = precio * p.Cantidad;
                p.Tipo    = "automatico";
            }
            _cambioPedido = true;
            RefrescarGridPedidos();
            ActualizarTotales();
        }

        // ─── CellEditEnding – Trasacciones ────────────────────────────────────
        private void GridTrasacciones_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.Item is TrasaccionItemFila fila && e.Column.Header?.ToString() == "Fecha")
                fila.FechaStr = fila.FechaDate?.ToString("d") ?? fila.FechaStr;
            _cambioTrasaccion = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefrescarGridTrasacciones();
                CargarTotalesDivisas();
                CargarEstadosCuenta();
                GridFocusHelper.EnfocarCeldaSeleccionada(GridTrasacciones);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // ─── Botones Cobros/Pagos ─────────────────────────────────────────────
        private void BtnNuevaLineaTrasaccion_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtTotalSaldo.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double saldo) || saldo <= 0)
            { MessageBox.Show("La cuenta ya se canceló.", "Consola", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;
            string tipo = AppState.TipoMovimiento.ToLower();
            _trasacciones.Add(new TrasaccionItemFila
            {
                TrasaccionId = "", Descripcion = $"{(tipo == "venta" ? "Cobro" : "Pago")} de documento {Box_DocumentoP.Text}",
                FechaStr = fechaDoc.ToString("d"), HoraStr = DateTime.Now.ToString("HH:mm:ss"),
                Forma = "efectivo", Importe = 0
            });
            _cambioTrasaccion = true;
            RefrescarGridTrasacciones();
        }

        private void BtnInsertarTrasaccion_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtTotalSaldo.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double saldo) || saldo <= 0)
            { MessageBox.Show("La cuenta ya se canceló.", "Consola", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            int idx = GridTrasacciones.SelectedItem is TrasaccionItemFila sel
                      ? _trasacciones.IndexOf(sel) : _trasacciones.Count;
            if (idx < 0) idx = _trasacciones.Count;
            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;
            string tipo = AppState.TipoMovimiento.ToLower();
            _trasacciones.Insert(idx, new TrasaccionItemFila
            {
                TrasaccionId = "", Descripcion = $"{(tipo == "venta" ? "Cobro" : "Pago")} de documento {Box_DocumentoP.Text}",
                FechaStr = fechaDoc.ToString("d"), HoraStr = DateTime.Now.ToString("HH:mm:ss"),
                Forma = "efectivo", Importe = 0
            });
            _cambioTrasaccion = true;
            RefrescarGridTrasacciones();
        }

        private void BtnEliminarLineaTrasaccion_Click(object sender, RoutedEventArgs e)
        {
            if (GridTrasacciones.SelectedItem is not TrasaccionItemFila fila) return;
            int idx = _trasacciones.IndexOf(fila);
            _trasacciones.Remove(fila);
            _cambioTrasaccion = true;
            RefrescarGridTrasacciones();
            CargarTotalesDivisas();
            CargarEstadosCuenta();
            if (_trasacciones.Count > 0)
            {
                var siguiente = _trasacciones[Math.Min(idx, _trasacciones.Count - 1)];
                GridTrasacciones.SelectedItem = siguiente;
                GridTrasacciones.ScrollIntoView(siguiente);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridTrasacciones);
        }

        private void BtnCobrarDocumento_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtTotalSaldo.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out double saldo) || saldo <= 0)
            { MessageBox.Show("La cuenta ya se canceló.", "Consola", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;
            string tipo = AppState.TipoMovimiento.ToLower();
            _trasacciones.Add(new TrasaccionItemFila
            {
                TrasaccionId = "",
                Descripcion  = $"{(tipo == "venta" ? "Cobro" : "Pago")} de documento {Box_DocumentoP.Text}",
                FechaStr     = fechaDoc.ToString("d"),
                HoraStr      = DateTime.Now.ToString("HH:mm:ss"),
                Forma        = "efectivo",
                Importe      = saldo
            });
            _cambioTrasaccion = true;
            RefrescarGridTrasacciones();
            CargarTotalesDivisas();
            CargarEstadosCuenta();
        }

        // ─── CellEditEnding – Entregas ────────────────────────────────────────
        private void GridEntregas_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.Item is not EntregaItemFila fila) return;

            if (e.Column.Header?.ToString() == "Fecha")
                fila.FechaStr = fila.FechaDate?.ToString("d") ?? fila.FechaStr;

            if (e.Column.Header?.ToString() == "Código" && e.EditingElement is TextBox tbCod)
            {
                string codigo = tbCod.Text.Trim();
                long artIdNum = Sql.ArticulosObj.BuscarIdentificador("codigo", codigo);
                if (artIdNum > 0)
                {
                    fila.ArticuloId  = artIdNum.ToString();
                    fila.Codigo      = codigo;
                    fila.Descripcion = ObtenerDescripcionArticulo(fila.ArticuloId);
                }
                else
                {
                    fila.ArticuloId  = "";
                    fila.Codigo      = codigo;
                    fila.Descripcion = string.IsNullOrEmpty(codigo) ? "" : "⚠ Artículo no encontrado";
                }
            }

            _cambioEntrega = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefrescarGridEntregas();
                if (AppState.TipoPedido.ToLower() == "normal") CargarEstados();
                GridFocusHelper.EnfocarCeldaSeleccionada(GridEntregas);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void GridEntregas_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (e.EditingElement is TextBox tb) { tb.SelectAll(); tb.Focus(); }
        }

        // ─── Botones Entregas ─────────────────────────────────────────────────
        private void BtnImportarArticulosEntregas_Click(object sender, RoutedEventArgs e)
        {
            ArticulosGeneral.OpenAsTab(Window.GetWindow(this)!, arts =>
            {
                DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;
                foreach (var art in arts)
                {
                    _entregas.Add(new EntregaItemFila
                    {
                        EntregaId = "", ArticuloId = art.Id,
                        Codigo = art.Codigo, Descripcion = art.Descripcion,
                        Cantidad = 1,
                        FechaStr = fechaDoc.ToString("d"),
                        HoraStr  = DateTime.Now.ToString("HH:mm:ss")
                    });
                }
                _cambioEntrega = true;
                RefrescarGridEntregas();
                if (AppState.TipoPedido.ToLower() == "normal") CargarEstados();
                if (_entregas.Count > 0)
                {
                    var ultimo = _entregas[_entregas.Count - 1];
                    GridEntregas.SelectedItem = ultimo;
                    GridEntregas.ScrollIntoView(ultimo);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(GridEntregas);
            }, null, contexto: _tituloTab, llamador: this);
        }

        private void BtnBuscarArticuloEntregas_Click(object sender, RoutedEventArgs e)
        {
            var filaActual = GridEntregas.SelectedItem as EntregaItemFila;
            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;

            ArticulosGeneral.OpenAsTab(Window.GetWindow(this)!, null, art =>
            {
                EntregaItemFila filaEnfocar;

                if (filaActual != null && _entregas.Contains(filaActual))
                {
                    filaActual.ArticuloId  = art.Id;
                    filaActual.Codigo      = art.Codigo;
                    filaActual.Descripcion = art.Descripcion;
                    filaEnfocar = filaActual;
                }
                else
                {
                    var nueva = new EntregaItemFila
                    {
                        EntregaId = "", ArticuloId = art.Id,
                        Codigo = art.Codigo, Descripcion = art.Descripcion,
                        Cantidad = 1,
                        FechaStr = fechaDoc.ToString("d"),
                        FechaDate = fechaDoc.Date,
                        HoraStr  = DateTime.Now.ToString("HH:mm:ss")
                    };
                    _entregas.Add(nueva);
                    filaEnfocar = nueva;
                }
                _cambioEntrega = true;
                RefrescarGridEntregas();
                if (AppState.TipoPedido.ToLower() == "normal") CargarEstados();

                var colCantidad = GridEntregas.Columns
                    .FirstOrDefault(c => c.Header?.ToString() == "Cantidad");
                if (colCantidad != null)
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        GridEntregas.SelectedItem = filaEnfocar;
                        GridEntregas.CurrentCell  = new DataGridCellInfo(filaEnfocar, colCantidad);
                        GridEntregas.ScrollIntoView(filaEnfocar, colCantidad);
                        GridEntregas.Focus();
                        GridEntregas.BeginEdit();
                    }), System.Windows.Threading.DispatcherPriority.Background);
            }, contexto: _tituloTab, llamador: this);
        }

        private void BtnNuevaLineaEntrega_Click(object sender, RoutedEventArgs e)
        {
            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;
            _entregas.Add(new EntregaItemFila
            {
                EntregaId = "", ArticuloId = "", Codigo = "", Descripcion = "",
                Cantidad = 1,
                FechaStr = fechaDoc.ToString("d"),
                HoraStr  = DateTime.Now.ToString("HH:mm:ss")
            });
            _cambioEntrega = true;
            RefrescarGridEntregas();
            if (_entregas.Count > 0)
            {
                var ultimo = _entregas[_entregas.Count - 1];
                GridEntregas.SelectedItem = ultimo;
                GridEntregas.ScrollIntoView(ultimo);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridEntregas);
        }

        private void BtnInsertarEntrega_Click(object sender, RoutedEventArgs e)
        {
            int idx = GridEntregas.SelectedItem is EntregaItemFila sel
                      ? _entregas.IndexOf(sel) : _entregas.Count;
            if (idx < 0) idx = _entregas.Count;
            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;
            _entregas.Insert(idx, new EntregaItemFila
            {
                EntregaId = "", ArticuloId = "", Codigo = "", Descripcion = "",
                Cantidad = 1,
                FechaStr = fechaDoc.ToString("d"),
                HoraStr  = DateTime.Now.ToString("HH:mm:ss")
            });
            _cambioEntrega = true;
            RefrescarGridEntregas();
            if (idx < GridEntregas.Items.Count)
            {
                GridEntregas.SelectedIndex = idx;
                GridEntregas.ScrollIntoView(GridEntregas.SelectedItem);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridEntregas);
        }

        private void BtnEliminarLineaEntrega_Click(object sender, RoutedEventArgs e)
        {
            if (GridEntregas.SelectedItem is not EntregaItemFila fila) return;
            int idx = _entregas.IndexOf(fila);
            _entregas.Remove(fila);
            _cambioEntrega = true;
            RefrescarGridEntregas();
            if (AppState.TipoPedido.ToLower() == "normal") CargarEstados();
            if (_entregas.Count > 0)
            {
                var siguiente = _entregas[Math.Min(idx, _entregas.Count - 1)];
                GridEntregas.SelectedItem = siguiente;
                GridEntregas.ScrollIntoView(siguiente);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridEntregas);
        }

        private void BtnRegistrarEntregas_Click(object sender, RoutedEventArgs e)
        {
            // Solo modo normal: auto-llena entregas pendientes
            var cantPedido = _pedidos
                .Where(p => !string.IsNullOrEmpty(p.ArticuloId))
                .GroupBy(p => p.ArticuloId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

            var cantEntrega = _entregas
                .Where(e2 => !string.IsNullOrEmpty(e2.ArticuloId))
                .GroupBy(e2 => e2.ArticuloId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));

            DateTime fechaDoc = Box_Fecha.SelectedDate ?? DateTime.Today;

            foreach (var kv in cantPedido)
            {
                double entregado = cantEntrega.TryGetValue(kv.Key, out double val) ? val : 0;
                double pendiente = kv.Value - entregado;
                if (pendiente <= 0) continue;

                string codigo = Sql.ArticulosObj.ObtenerItem("codigo", kv.Key)?.ToString() ?? "";
                _entregas.Add(new EntregaItemFila
                {
                    EntregaId   = "", ArticuloId = kv.Key, Codigo = codigo,
                    Descripcion = ObtenerDescripcionArticulo(kv.Key),
                    Cantidad    = pendiente,
                    FechaStr    = fechaDoc.ToString("d"),
                    HoraStr     = DateTime.Now.ToString("HH:mm:ss")
                });
            }

            _cambioEntrega = true;
            RefrescarGridEntregas();
            CargarEstados();
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
            { MessageBox.Show("Ingrese el número de documento.", "Consola", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }

            try
            {
                if (!Sql.DocumentosPObj.VerificarId(docP, "id"))
                { MessageBox.Show("El número de documento ya existe.", "Consola", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }

                DateTime fechaFinal = CombinarFechaHora(Box_Fecha.SelectedDate ?? DateTime.Today, Box_Hora.Text);
                string estado  = Box_Estado.Text;
                string cuenta  = Box_Cuenta.Text;
                string tipoPed = AppState.TipoPedido.ToLower();

                Sql.DocumentosPObj.Nuevo(docP);
                Sql.DocumentosPObj.EstablecerItem("sucursal",    docP, AppState.SucursalActiva);
                Sql.DocumentosPObj.EstablecerItem("tercero",     docP, Box_Tercero_Identificador.Text.Trim());
                Sql.DocumentosPObj.EstablecerItem("movimiento",  docP, AppState.TipoMovimiento);
                Sql.DocumentosPObj.EstablecerItem("estado",      docP, estado);
                Sql.DocumentosPObj.EstablecerItem("fecha",       docP, fechaFinal);
                Sql.DocumentosPObj.EstablecerItem("referencia",  docP, Box_Referencia.Text.Trim());
                Sql.DocumentosPObj.EstablecerItem("observacion", docP, _observaciones);
                Sql.DocumentosPObj.EstablecerItem("estadoC",     docP, cuenta);
                Sql.DocumentosPObj.EstablecerItem("tipo",        docP, tipoPed);
                Sql.DocumentosPObj.EstablecerItem("emision",     docP, DateTime.Now);
                Sql.DocumentosPObj.EstablecerItem("edicion",     docP, DateTime.Now);
                Sql.DocumentosPObj.EstablecerItem("emitido",     docP, AppState.SucursalActiva);
                Sql.DocumentosPObj.EstablecerItem("usuario",     docP, AppState.UsuarioActivo);
                Sql.DocumentosPObj.EstablecerItem("usuarioE",    docP, AppState.UsuarioActivo);

                GuardarLineasPedido(docP);
                GuardarLineasTrasaccion(docP);
                GuardarLineasEntrega(docP);
                OrdenarTablas();

                MessageBox.Show("Guardado exitoso.", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                _cambioDocumento = _cambioPedido = _cambioTrasaccion = _cambioEntrega = false;
                DocumentoCreadoId = docP;   // Bug 3: comunica el id al padre
                return true;
            }
            catch (Exception ex)
            { MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
        }

        private bool GuardarEditar()
        {
            string docP = _idEditar;
            try
            {
                DateTime fechaFinal = CombinarFechaHora(Box_Fecha.SelectedDate ?? DateTime.Today, Box_Hora.Text);
                string estado = Box_Estado.Text;
                string cuenta = Box_Cuenta.Text;

                Sql.DocumentosPObj.EstablecerItem("tercero",     docP, Box_Tercero_Identificador.Text.Trim());
                Sql.DocumentosPObj.EstablecerItem("fecha",       docP, fechaFinal);
                Sql.DocumentosPObj.EstablecerItem("estado",      docP, estado);
                Sql.DocumentosPObj.EstablecerItem("referencia",  docP, Box_Referencia.Text.Trim());
                Sql.DocumentosPObj.EstablecerItem("observacion", docP, _observaciones);
                Sql.DocumentosPObj.EstablecerItem("estadoC",     docP, cuenta);
                Sql.DocumentosPObj.EstablecerItem("edicion",     docP, DateTime.Now);
                Sql.DocumentosPObj.EstablecerItem("usuario",     docP, AppState.UsuarioActivo);
                Sql.DocumentosPObj.EstablecerItem("usuarioE",    docP, AppState.UsuarioActivo);

                // Solo eliminar y recrear las líneas que realmente cambiaron.
                // EliminarLineas sin su correspondiente Guardar ocultaría
                // las filas existentes sin volver a crearlas (Bug 2).
                if (_cambioPedido)
                {
                    EliminarLineas(Sql.PedidosObj, "documentoP", docP);
                    GuardarLineasPedido(docP);
                }
                if (_cambioTrasaccion)
                {
                    EliminarLineas(Sql.TrasaccionesObj, "documentoP", docP);
                    GuardarLineasTrasaccion(docP);
                }
                if (_cambioEntrega)
                {
                    EliminarLineas(Sql.EntregasObj, "documentoP", docP);
                    GuardarLineasEntrega(docP);
                }
                OrdenarTablas();

                MessageBox.Show("Guardado exitoso.", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                _cambioDocumento = _cambioPedido = _cambioTrasaccion = _cambioEntrega = false;
                return true;
            }
            catch (Exception ex)
            { MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
        }

        private void EliminarLineas(DataConsulta tabla, string campo, string docP)
        {
            int uf = tabla.ContarFilas;
            var ids = new List<string>();
            for (int i = 1; i <= uf; i++)
            {
                var idObj = tabla.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (tabla.ObtenerItem(campo, id)?.ToString() == docP)
                    ids.Add(id);
            }
            foreach (var id in ids) tabla.Eliminar(id);
        }

        private void GuardarLineasPedido(string docP)
        {
            for (int i = 0; i < _pedidos.Count; i++)
            {
                var item  = _pedidos[i];
                string id = $"{docP}{(i + 1):D3}";
                Sql.PedidosObj.Nuevo(id);
                Sql.PedidosObj.EstablecerItem("documentoP", id, docP);
                Sql.PedidosObj.EstablecerItem("articulo",   id, item.ArticuloId);
                Sql.PedidosObj.EstablecerItem("indice",     id, i + 1);
                Sql.PedidosObj.EstablecerItem("cantidad",   id, item.Cantidad);
                Sql.PedidosObj.EstablecerItem("importe",    id, item.Importe);
                Sql.PedidosObj.EstablecerItem("forma",      id, item.Forma);
                Sql.PedidosObj.EstablecerItem("contable",   id, item.Contable);
                Sql.PedidosObj.EstablecerItem("tipo",       id, item.Tipo);
            }
        }

        private void GuardarLineasTrasaccion(string docP)
        {
            for (int i = 0; i < _trasacciones.Count; i++)
            {
                var item  = _trasacciones[i];
                string id = $"{docP}{(i + 1):D3}";
                DateTime fechaT = CombinarFechaHora(
                    DateTime.TryParse(item.FechaStr, out var fd) ? fd : DateTime.Today,
                    item.HoraStr);
                Sql.TrasaccionesObj.Nuevo(id);
                Sql.TrasaccionesObj.EstablecerItem("documentoP",  id, docP);
                Sql.TrasaccionesObj.EstablecerItem("indice",      id, i + 1);
                Sql.TrasaccionesObj.EstablecerItem("fecha",       id, fechaT);
                Sql.TrasaccionesObj.EstablecerItem("descripcion", id, item.Descripcion);
                Sql.TrasaccionesObj.EstablecerItem("importe",     id, item.Importe);
                Sql.TrasaccionesObj.EstablecerItem("forma",       id, item.Forma);
            }
        }

        private void GuardarLineasEntrega(string docP)
        {
            for (int i = 0; i < _entregas.Count; i++)
            {
                var item  = _entregas[i];
                string id = $"{docP}{(i + 1):D3}";
                DateTime fechaE = CombinarFechaHora(
                    DateTime.TryParse(item.FechaStr, out var fd) ? fd : DateTime.Today,
                    item.HoraStr);
                Sql.EntregasObj.Nuevo(id);
                Sql.EntregasObj.EstablecerItem("documentoP", id, docP);
                Sql.EntregasObj.EstablecerItem("articulo",   id, item.ArticuloId);
                Sql.EntregasObj.EstablecerItem("indice",     id, i + 1);
                Sql.EntregasObj.EstablecerItem("cantidad",   id, item.Cantidad);
                Sql.EntregasObj.EstablecerItem("fecha",      id, fechaE);
            }
        }

        private static void OrdenarTablas()
        {
            Sql.DocumentosPObj.OrdenarData(("fecha", false));
            Sql.PedidosObj.OrdenarData(("documentoP", false), ("indice", false));
            Sql.TrasaccionesObj.OrdenarData(("documentoP", false), ("indice", false));
            Sql.EntregasObj.OrdenarData(("documentoP", false), ("indice", false));
        }

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            CommitEdicionesPendientes();
            if (Guardar()) Cerrando?.Invoke();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            _cambioDocumento = _cambioPedido = _cambioTrasaccion = _cambioEntrega = false;
            Cerrando?.Invoke();
        }

        // Confirma cualquier celda en edición en los 3 grids editables
        private void CommitEdicionesPendientes()
        {
            GridItems.CommitEdit(DataGridEditingUnit.Row, true);
            GridTrasacciones.CommitEdit(DataGridEditingUnit.Row, true);
            GridEntregas.CommitEdit(DataGridEditingUnit.Row, true);
        }

        // Llamado por el botón X del overlay para verificar cambios antes de cerrar
        public void IntentarCerrar()
        {
            CommitEdicionesPendientes();
            if (!HayCambios) { Cerrando?.Invoke(); return; }

            var res = MessageBox.Show("¿Guardar cambios?", "Consola",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes && Guardar()) Cerrando?.Invoke();
            else if (res == MessageBoxResult.No) Cerrando?.Invoke();
        }
    }

    // ─── Modelos de fila ──────────────────────────────────────────────────────
    public class PedidoItemFila
    {
        public string PedidoId    { get; set; } = "";
        public int    Linea       { get; set; }
        public string ArticuloId  { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Cantidad    { get; set; }
        public string Forma       { get; set; } = "sin factura";
        public double Contable    { get; set; } = 0;
        public double Precio      { get; set; }
        public double Importe     { get; set; }
        public string Tipo        { get; set; } = "automatico";
    }

    public class TrasaccionItemFila
    {
        public string    TrasaccionId { get; set; } = "";
        public int       Linea        { get; set; }
        public string    FechaStr     { get; set; } = "";
        public DateTime? FechaDate    { get; set; }
        public string    HoraStr      { get; set; } = "";
        public string    Descripcion  { get; set; } = "";
        public string    Forma        { get; set; } = "efectivo";
        public double    Importe      { get; set; }
    }

    public class EntregaItemFila
    {
        public string    EntregaId   { get; set; } = "";
        public int       Linea       { get; set; }
        public string    ArticuloId  { get; set; } = "";
        public string    Codigo      { get; set; } = "";
        public string    Descripcion { get; set; } = "";
        public double    Cantidad    { get; set; }
        public string    FechaStr    { get; set; } = "";
        public DateTime? FechaDate   { get; set; }
        public string    HoraStr     { get; set; } = "";
    }

    public class PrecioFila
    {
        public string Codigo { get; set; } = "";
        public string Fecha  { get; set; } = "";
        public string Precio { get; set; } = "";
    }

    public class StockFila
    {
        public string Codigo     { get; set; } = "";
        public string Disponible { get; set; } = "";
        public string Stock      { get; set; } = "";
    }
}
