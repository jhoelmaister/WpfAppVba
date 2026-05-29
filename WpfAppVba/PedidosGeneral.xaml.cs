using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class PedidosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private string _mesActivo  = "";
        private string _modoFiltro = "filtros"; // "filtros" = Tree1 | "busquedas" = TxtBuscar

        /// <summary>
        /// Tipo de movimiento fijo para este control ("venta" o "compra").
        /// Si está vacío, se lee de AppState.TipoMovimiento.
        /// </summary>
        public string TipoMovimiento { get; set; } = "";

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

            // Nodo padre "General" → muestra todos los meses (Tag vacío = sin filtro)
            var nodoGeneral = new TreeViewItem
            {
                Header     = "General",
                Tag        = "",
                IsExpanded = true
            };
            foreach (var mes in meses)
                nodoGeneral.Items.Add(new TreeViewItem { Header = mes, Tag = mes });

            Tree1.Items.Add(nodoGeneral);

            // Selección por defecto: mes actual
            int mesActual = DateTime.Now.Month - 1;
            if (nodoGeneral.Items[mesActual] is TreeViewItem ti)
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
            // Cada modo aplica solo su propio filtro de contenido (igual que VBA llaveActualisar)
            string busqueda = _modoFiltro == "busquedas" ? TxtBuscar.Text.Trim().ToLower() : "";
            string mesFiltro = _modoFiltro == "filtros"  ? _mesActivo : "";
            string tipoMov  = !string.IsNullOrEmpty(TipoMovimiento)
                              ? TipoMovimiento.ToLower()
                              : AppState.TipoMovimiento.ToLower();

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

                // Filtro por mes (solo en modo "filtros", independiente de TxtBuscar)
                if (!string.IsNullOrEmpty(mesFiltro))
                {
                    var fechaObj2 = Sql.DocumentosPObj.ObtenerItem("fecha", id);
                    if (fechaObj2 == null) continue;
                    DateTime fecha2 = Convert.ToDateTime(fechaObj2);
                    string mesDoc = ObtenerNombreMes(fecha2.Month);
                    if (!string.Equals(mesDoc, mesFiltro, StringComparison.OrdinalIgnoreCase)) continue;
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
                    FechaStr    = $"{fechaDoc:d} {fechaDoc:HH:mm:ss}",
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
            TxtTotalCantidad.Text = totalCant.ToString("N0");
            TxtTotalImporte.Text  = totalImporte.ToString("N2");

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

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<PedidoFila> FilasGrid =>
            Grid1.ItemsSource as List<PedidoFila> ?? new List<PedidoFila>();

        private PedidoFila ConstruirFilaPedido(string id, int linea)
        {
            var fechaDocObj = Sql.DocumentosPObj.ObtenerItem("fecha", id);
            DateTime fechaDoc = fechaDocObj != null ? Convert.ToDateTime(fechaDocObj) : default;
            string terceroId   = Sql.DocumentosPObj.ObtenerItem("tercero", id)?.ToString() ?? "";
            string terceroDesc = Sql.TercerosObj.ObtenerItem("descripcion", terceroId)?.ToString() ?? terceroId;
            string estado  = Sql.DocumentosPObj.ObtenerItem("estado",  id)?.ToString() ?? "";
            string estadoC = Sql.DocumentosPObj.ObtenerItem("estadoC", id)?.ToString() ?? "";

            return new PedidoFila
            {
                Linea       = linea,
                DocumentoP  = id,
                FechaStr    = $"{fechaDoc:d} {fechaDoc:HH:mm:ss}",
                TerceroDesc = terceroDesc,
                Estado      = estado,
                Cuenta      = estadoC,
                Cantidad    = CalcularCantidad(id),
                Importe     = CalcularImporte(id)
            };
        }

        private void RenumerarYTotales()
        {
            var lista = FilasGrid;
            int n = 1;
            double totalCant = 0, totalImporte = 0;
            foreach (var f in lista)
            {
                f.Linea       = n++;
                totalCant    += f.Cantidad;
                totalImporte += f.Importe;
            }
            TxtTotalCantidad.Text = totalCant.ToString("N0");
            TxtTotalImporte.Text  = totalImporte.ToString("N2");
            Grid1.Items.Refresh();
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
                string desc        = Sql.ArticulosObj.ObtenerItem("descripcion", articuloId)?.ToString() ?? "";
                string famId       = Sql.ArticulosObj.ObtenerItem("familia",     articuloId)?.ToString() ?? "";
                string famDesc     = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
                string modelo      = Sql.ArticulosObj.ObtenerItem("modelo",      articuloId)?.ToString() ?? "";
                string descripcion = FuncionesComunes.UnirVariables(desc, famDesc, modelo);
                double cantidad    = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad", id) ?? 0);
                double importe     = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("importe",  id) ?? 0);

                detalles.Add(new PedidoDetalleFila
                {
                    Linea      = linea++,
                    ArticuloId = articuloId,
                    Descripcion = descripcion,
                    Cantidad   = cantidad,
                    Importe    = importe.ToString("N2")
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
            _modoFiltro = "filtros";   // Tree1 activo → ignora TxtBuscar
            CargarPedidos();
        }

        private void Tree1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Si el usuario hace clic en el item ya seleccionado, SelectedItemChanged no se dispara.
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null && source is not TreeViewItem)
                source = VisualTreeHelper.GetParent(source);

            if (source is TreeViewItem tvi && tvi.IsSelected)
            {
                _modoFiltro = "filtros";
                CargarPedidos();
            }
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

        // ─── Búsqueda (independiente del Tree1) ──────────────────────────────
        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _modoFiltro = "busquedas"; // TxtBuscar activo → ignora Tree1
            CargarPedidos();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            _modoFiltro = "busquedas"; // TxtBuscar activo → ignora Tree1
            CargarPedidos();
        }

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Tecla Enter ──────────────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            AppState.TipoPedido        = "normal";
            if (!string.IsNullOrEmpty(TipoMovimiento))
                AppState.TipoMovimiento = TipoMovimiento;
            var dlg = new PedidosDetalle(this) { Owner = Window.GetWindow(this) };
            dlg.ShowDialog();
            if (dlg.DocumentoCreadoId == null) return;   // cancelado

            var nueva = ConstruirFilaPedido(dlg.DocumentoCreadoId, 0);
            FilasGrid.Add(nueva);
            RenumerarYTotales();
            Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
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

                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0) lista.RemoveAt(idx);
                RenumerarYTotales();

                if (lista.Count > 0)
                {
                    var sel = lista[Math.Min(idx, lista.Count - 1)];
                    Grid1.SelectedItem = sel; Grid1.ScrollIntoView(sel);
                }
                else OcultarDetalle();
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
            if (!string.IsNullOrEmpty(TipoMovimiento))
                AppState.TipoMovimiento = TipoMovimiento;
            new PedidosDetalle(this, fila.DocumentoP) { Owner = Window.GetWindow(this) }.ShowDialog();

            var lista = FilasGrid;
            int idx   = lista.IndexOf(fila);
            if (idx >= 0)
            {
                var actualizada = ConstruirFilaPedido(docSel, fila.Linea);
                lista[idx] = actualizada;
                RenumerarYTotales();
                Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
            }

            // Tras cerrar el diálogo, WPF restaura el foco de forma asíncrona hacia
            // la ventana/TabControl, dejando la fila seleccionada pero sin foco de
            // teclado dentro del grid (las flechas dejan de navegar). Diferimos el
            // enfoque a la CELDA seleccionada (prioridad Background, para correr tras
            // la restauración de WPF), que es lo que reactiva la navegación.
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
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
