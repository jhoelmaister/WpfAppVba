using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class PedidosGeneral : Window
    {
        private static SqlData Sql => SqlData.Instance;
        private string _mesActivo = "";

        public PedidosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { CargarMeses(); CargarPedidos(); };
        }

        // ─── Carga el árbol de meses ──────────────────────────────────────────
        private void CargarMeses()
        {
            Tree1.Items.Clear();
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };
            foreach (var mes in meses)
            {
                var item = new TreeViewItem { Header = mes, Tag = mes };
                Tree1.Items.Add(item);
            }

            int mesActual = DateTime.Now.Month - 1;
            if (Tree1.Items[mesActual] is TreeViewItem ti)
            {
                ti.IsSelected = true;
                _mesActivo = meses[mesActual];
            }
        }

        // ─── Carga la lista de pedidos ────────────────────────────────────────
        public void CargarPedidos()
        {
            // Guard: evita correr antes de que todos los controles estén inicializados
            if (TxtBuscar == null || Grid1 == null) return;

            var lista = new List<PedidoFila>();
            int linea = 1;
            double totalCant = 0, totalImporte = 0;

            string filtroEstado = ObtenerFiltroEstado();
            string filtroCuenta = ObtenerFiltroCuenta();
            string busqueda     = TxtBuscar.Text.ToLower();
            string tipoMov      = AppState.TipoMovimiento.ToLower();

            int uf = Sql.DocumentosPObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosPObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                // Filtrar por tipo de movimiento y sucursal activa
                string movDoc = Sql.DocumentosPObj.ObtenerItem("movimiento", id)?.ToString() ?? "";
                if (!string.Equals(movDoc, tipoMov, StringComparison.OrdinalIgnoreCase)) continue;

                string sucursal = Sql.DocumentosPObj.ObtenerItem("sucursal", id)?.ToString() ?? "";
                if (sucursal != AppState.SucursalActiva.ToString()) continue;

                // Filtro por mes
                if (!string.IsNullOrEmpty(_mesActivo))
                {
                    var fechaObj2 = Sql.DocumentosPObj.ObtenerItem("fecha", id);
                    if (fechaObj2 == null) continue;
                    DateTime fecha2 = Convert.ToDateTime(fechaObj2);
                    string mesDoc = ObtenerNombreMes(fecha2.Month);
                    if (!string.Equals(mesDoc, _mesActivo, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var fechaDocObj = Sql.DocumentosPObj.ObtenerItem("fecha", id);
                DateTime fechaDoc = fechaDocObj != null ? Convert.ToDateTime(fechaDocObj) : default;

                string terceroId   = Sql.DocumentosPObj.ObtenerItem("tercero", id)?.ToString() ?? "";
                string terceroDesc = Sql.TercerosObj.ObtenerItem("descripcion", terceroId)?.ToString() ?? terceroId;

                string estado = Sql.DocumentosPObj.ObtenerItem("estado", id)?.ToString() ?? "";

                // ── Usar estadoC (campo correcto en VBA) para cuenta ──────────
                string estadoC = Sql.DocumentosPObj.ObtenerItem("estadoC", id)?.ToString() ?? "";

                // Filtro por estado
                if (!string.IsNullOrEmpty(filtroEstado) &&
                    !string.Equals(estado, filtroEstado, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Filtro por cuenta (estadoC)
                if (!string.IsNullOrEmpty(filtroCuenta) &&
                    !string.Equals(estadoC, filtroCuenta, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Filtro por búsqueda
                if (!string.IsNullOrEmpty(busqueda))
                    if (!id.Contains(busqueda) && !terceroDesc.ToLower().Contains(busqueda))
                        continue;

                double cant    = CalcularCantidad(id);
                double importe = CalcularImporte(id);

                lista.Add(new PedidoFila
                {
                    Linea       = linea++,
                    DocumentoP  = id,
                    FechaStr    = fechaDoc.ToString("dd/MM/yyyy HH:mm:ss"),
                    TerceroDesc = terceroDesc,
                    Estado      = estado,
                    Cuenta      = estadoC,
                    Cantidad    = cant,
                    Importe     = importe
                });

                totalCant    += cant;
                totalImporte += importe;
            }

            Grid1.ItemsSource = lista;
            TxtTotalCantidad.Text = totalCant.ToString("F0");
            TxtTotalImporte.Text  = totalImporte.ToString("F2");

            // ── Título correcto según VBA ─────────────────────────────────────
            LblTipoMovimiento.Text = tipoMov == "venta"
                ? "Ventas de Productos"
                : "Compras de Productos";

            // Ocultar el panel de detalle al recargar
            OcultarDetalle();
        }

        // ─── Filtros ──────────────────────────────────────────────────────────
        private string ObtenerFiltroEstado()
        {
            if (BtnFiltroPendiente?.IsChecked == true)      return "pendiente";
            if (BtnFiltroEntregado?.IsChecked == true)      return "entregado";
            if (BtnFiltroEntregaParcial?.IsChecked == true) return "entrega parcial";
            return "";
        }

        private string ObtenerFiltroCuenta()
        {
            if (BtnCuentaPendiente?.IsChecked == true) return "pendiente";
            if (BtnCuentaCancelado?.IsChecked == true) return "cancelado";
            if (BtnCuentaParcial?.IsChecked   == true) return "pendiente parcial";  // ← correcto
            return "";
        }

        // ─── Nombre de mes ────────────────────────────────────────────────────
        private static string ObtenerNombreMes(int mes)
        {
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };
            return mes >= 1 && mes <= 12 ? meses[mes - 1] : "";
        }

        // ─── Calcular totales de un documentoP ───────────────────────────────
        private static double CalcularCantidad(string documentoP)
        {
            double cant = 0;
            int uf = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() != documentoP) continue;
                cant += Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad", id) ?? 0);
            }
            return cant;
        }

        private static double CalcularImporte(string documentoP)
        {
            double importe = 0;
            int uf = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() != documentoP) continue;
                // ── Usar la columna importe directamente (igual que VBA) ──────
                importe += Convert.ToDouble(Sql.PedidosObj.ObtenerItem("importe", id) ?? 0);
            }
            return importe;
        }

        // ─── Panel de detalle (Lista2) ────────────────────────────────────────

        private void MostrarDetalle(string documentoP)
        {
            var detalles = new List<PedidoDetalleFila>();
            int linea = 1;

            int uf = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() != documentoP) continue;

                string articuloId  = Sql.PedidosObj.ObtenerItem("articulo",  id)?.ToString() ?? "";
                string descripcion = Sql.ArticulosObj.ObtenerItem("descripcion", articuloId)?.ToString() ?? articuloId;
                double cantidad    = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad", id) ?? 0);
                double importe     = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("importe",  id) ?? 0);

                detalles.Add(new PedidoDetalleFila
                {
                    Linea      = linea++,
                    ArticuloId = articuloId,
                    Descripcion = descripcion,
                    Cantidad   = cantidad,
                    Importe    = importe.ToString("F2")
                });
            }

            Lista2.ItemsSource = detalles;
            PanelDetalle.Visibility = detalles.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OcultarDetalle()
        {
            PanelDetalle.Visibility = Visibility.Collapsed;
            Lista2.ItemsSource = null;
        }

        // ─── Eventos árbol y filtros ──────────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Tree1.SelectedItem is TreeViewItem ti)
                _mesActivo = ti.Tag?.ToString() ?? "";
            CargarPedidos();
        }

        private void FiltroEstado_Checked(object sender, RoutedEventArgs e)
            => CargarPedidos();

        private void FiltroCuenta_Checked(object sender, RoutedEventArgs e)
            => CargarPedidos();

        // ─── Selección en Grid1 → mostrar detalle ────────────────────────────
        private void Grid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Grid1.SelectedItem is PedidoFila fila)
                MostrarDetalle(fila.DocumentoP);
            else
                OcultarDetalle();
        }

        // ─── Búsqueda ─────────────────────────────────────────────────────────
        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CargarPedidos();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarPedidos();

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoPedido        = "normal";   // ← según VBA: siempre "normal" desde aquí
            new PedidosDetalle(this).ShowDialog();
            CargarPedidos();
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not PedidoFila fila) return;

            var res = MessageBox.Show("¿Eliminar este pedido y todos sus artículos?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                Sql.DocumentosPObj.Ocultar(fila.DocumentoP);

                int uf = Sql.PedidosObj.ContarFilas;
                var idsOcultar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.PedidosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() == fila.DocumentoP)
                        idsOcultar.Add(id);
                }
                foreach (var id in idsOcultar)
                    Sql.PedidosObj.Ocultar(id);

                Sql.DocumentosPObj.OrdenarData(("fecha", false));
                Sql.PedidosObj.OrdenarData(("documentoP", false), ("indice", false));
                CargarPedidos();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar: {ex.Message}", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            Sql.DocumentosPObj.Actualizar();
            Sql.PedidosObj.Actualizar();
            CargarPedidos();
        }

        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not PedidoFila fila) return;
            string docSel = fila.DocumentoP;
            AppState.EventoFormularioM = "editar";
            new PedidosDetalle(this, fila.DocumentoP).ShowDialog();
            CargarPedidos();
            var item = (Grid1.ItemsSource as System.Collections.Generic.List<PedidoFila>)
                       ?.Find(x => x.DocumentoP == docSel);
            if (item != null) { Grid1.SelectedItem = item; Grid1.ScrollIntoView(item); }
            Grid1.Focus();
        }
    }

    // ─── Modelo de fila principal ─────────────────────────────────────────────
    public class PedidoFila
    {
        public int    Linea       { get; set; }
        public string DocumentoP  { get; set; } = "";
        public string FechaStr    { get; set; } = "";
        public string TerceroDesc { get; set; } = "";
        public string Estado      { get; set; } = "";
        public string Cuenta      { get; set; } = "";
        public double Cantidad    { get; set; }
        public double Importe     { get; set; }
    }

    // ─── Modelo de fila de detalle (Lista2) ──────────────────────────────────
    public class PedidoDetalleFila
    {
        public int    Linea       { get; set; }
        public string ArticuloId  { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Cantidad    { get; set; }
        public string Importe     { get; set; } = "";
    }
}
