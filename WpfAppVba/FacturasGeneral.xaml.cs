using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class FacturasGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private string _mesActivo  = "";
        private string _modoFiltro = "filtros"; // "filtros" = Tree1 | "busquedas" = TxtBuscar

        private bool _iniciado = false;

        public FacturasGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; ConfigurarModo(); CargarMeses(); CargarFacturas(); };
        }

        private void ConfigurarModo()
        {
            if (!AppState.EsAdmin) BtnEliminar.Visibility = Visibility.Collapsed;
        }

        // ─── Carga el árbol de meses ──────────────────────────────────────────
        private void CargarMeses()
        {
            Tree1.Items.Clear();
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };

            int año = AppState.DataFechaFinal.Year > 2000
                ? AppState.DataFechaFinal.Year
                : DateTime.Now.Year;

            // Solo los meses que tienen documentos cargados (igual que CorreccionesGeneral).
            var mesesConDatos = new SortedSet<int>();
            int uf = Sql.DocumentosFObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosFObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                string suc = Sql.DocumentosFObj.ObtenerItem("sucursal", id)?.ToString() ?? "";
                if (suc != AppState.SucursalActiva) continue;
                var fechaObj = Sql.DocumentosFObj.ObtenerItem("fecha", id);
                if (fechaObj == null) continue;
                mesesConDatos.Add(Convert.ToDateTime(fechaObj).Month);
            }

            // Si no hay documentos en el período, el árbol queda vacío (no se muestra el año).
            if (mesesConDatos.Count == 0) return;

            // Nodo padre con el año/período activo → muestra todos los meses (Tag vacío = sin filtro)
            var nodoGeneral = new TreeViewItem
            {
                Header     = año.ToString(),
                Tag        = "",
                IsExpanded = true
            };
            foreach (int mes in mesesConDatos)
                nodoGeneral.Items.Add(new TreeViewItem { Header = meses[mes - 1], Tag = meses[mes - 1] });

            Tree1.Items.Add(nodoGeneral);

            // Selección por defecto: mes actual (si tiene documentos)
            int mesActual = DateTime.Now.Month;
            if (mesesConDatos.Contains(mesActual))
            {
                foreach (var item in nodoGeneral.Items)
                {
                    if (item is not TreeViewItem ti || (string)ti.Tag != meses[mesActual - 1]) continue;
                    ti.IsSelected = true;
                    _mesActivo = meses[mesActual - 1];
                    break;
                }
            }
        }

        // ─── Carga la lista de facturas ────────────────────────────────────────
        public void CargarFacturas()
        {
            if (TxtBuscar == null || Grid1 == null) return;

            var lista = new List<FacturaFila>();
            int linea = 1;
            double totalImporte = 0;
            string busqueda  = _modoFiltro == "busquedas" ? TxtBuscar.Text.Trim().ToLower() : "";
            string mesFiltro = _modoFiltro == "filtros"   ? _mesActivo : "";
            string filtroEstado = ObtenerFiltroEstado();
            string filtroCuenta = ObtenerFiltroCuenta();
            string filtroTipo   = ObtenerFiltroTipo();

            int uf = Sql.DocumentosFObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosFObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                // Filtrar por tipo de movimiento y sucursal activa
                string movDoc = (Sql.DocumentosFObj.ObtenerItem("movimiento", id)?.ToString() ?? "venta").ToLower();
                if (!string.IsNullOrEmpty(filtroTipo) &&
                    !string.Equals(movDoc, filtroTipo, StringComparison.OrdinalIgnoreCase)) continue;

                string suc = Sql.DocumentosFObj.ObtenerItem("sucursal", id)?.ToString() ?? "";
                if (suc != AppState.SucursalActiva) continue;

                // Filtro por mes (solo en modo "filtros", independiente de TxtBuscar)
                var fechaDocObj = Sql.DocumentosFObj.ObtenerItem("fecha", id);
                DateTime fechaDoc = fechaDocObj != null ? Convert.ToDateTime(fechaDocObj) : default;
                if (!string.IsNullOrEmpty(mesFiltro))
                {
                    if (fechaDocObj == null) continue;
                    string mesDoc = ObtenerNombreMes(fechaDoc.Month);
                    if (!string.Equals(mesDoc, mesFiltro, StringComparison.OrdinalIgnoreCase)) continue;
                }

                string estado  = (Sql.DocumentosFObj.ObtenerItem("estado",  id)?.ToString() ?? "pendiente").ToLower();
                string estadoC = (Sql.DocumentosFObj.ObtenerItem("estadoC", id)?.ToString() ?? "pendiente").ToLower();

                // Filtro por estado
                if (!string.IsNullOrEmpty(filtroEstado) &&
                    !string.Equals(estado, filtroEstado, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Filtro por cuenta (estadoC)
                if (!string.IsNullOrEmpty(filtroCuenta) &&
                    !string.Equals(estadoC, filtroCuenta, StringComparison.OrdinalIgnoreCase))
                    continue;

                string codigo      = Sql.DocumentosFObj.ObtenerItem("codigo", id)?.ToString() ?? "";
                string referencia  = Sql.DocumentosFObj.ObtenerItem("referencia", id)?.ToString() ?? "";
                string terceroId   = Sql.DocumentosFObj.ObtenerItem("tercero", id)?.ToString() ?? "";
                string terceroDesc = Sql.TercerosObj.ObtenerItem("descripcion", terceroId)?.ToString() ?? "";

                // Filtro por búsqueda
                if (!string.IsNullOrEmpty(busqueda))
                    if (!codigo.ToLower().Contains(busqueda) && !referencia.ToLower().Contains(busqueda)
                        && !terceroDesc.ToLower().Contains(busqueda))
                        continue;

                double importe = CalcularImporte(id);
                lista.Add(new FacturaFila
                {
                    Linea        = linea++,
                    Id           = id,
                    Codigo       = codigo,
                    Fecha        = fechaDoc,
                    FechaStr     = fechaDoc != default ? $"{fechaDoc:d} {fechaDoc:HH:mm:ss}" : "",
                    Referencia   = referencia,
                    TerceroDesc  = terceroDesc,
                    Movimiento   = movDoc,
                    ImporteTotal = importe,
                    Estado       = estado,
                    EstadoC      = estadoC
                });
                totalImporte += importe;
            }

            Grid1.ItemsSource        = lista;
            TxtTotalImporte.Text     = totalImporte.ToString("N2");
            TxtTotalDocumentos.Text  = lista.Count.ToString("N0");
            TxtEstadosPendientes.Text = lista.Count(f => f.Estado == "pendiente").ToString();
            TxtCuentasPendientes.Text = lista.Count(f => f.EstadoC == "pendiente" || f.EstadoC == "pendiente parcial").ToString();
            int año = AppState.DataFechaFinal.Year > 2000
                ? AppState.DataFechaFinal.Year
                : DateTime.Now.Year;
            LblSubtitulo.Text = string.IsNullOrEmpty(_mesActivo)
                ? año.ToString()
                : $"{_mesActivo} {año}";

            OcultarDetalle();
        }

        // ─── Filtros ──────────────────────────────────────────────────────────
        private string ObtenerFiltroEstado()
        {
            if (BtnFiltroPendiente?.IsChecked == true) return "pendiente";
            if (BtnFiltroEntregado?.IsChecked == true) return "entregado";
            return "";
        }

        private string ObtenerFiltroCuenta()
        {
            if (BtnCuentaPendiente?.IsChecked == true) return "pendiente";
            if (BtnCuentaCancelado?.IsChecked == true) return "cancelado";
            if (BtnCuentaParcial?.IsChecked   == true) return "pendiente parcial";
            return "";
        }

        private void FiltroEstado_Checked(object sender, RoutedEventArgs e)
            => CargarFacturas();

        private void FiltroCuenta_Checked(object sender, RoutedEventArgs e)
            => CargarFacturas();

        private string ObtenerFiltroTipo()
        {
            return (CboTipoMovimiento?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() switch
            {
                "ventas"  => "venta",
                "compras" => "compra",
                _         => ""
            };
        }

        private void CboTipoMovimiento_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => CargarFacturas();

        // ─── Nombre de mes ────────────────────────────────────────────────────
        private static string ObtenerNombreMes(int mes)
        {
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };
            return mes >= 1 && mes <= 12 ? meses[mes - 1] : "";
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<FacturaFila> FilasGrid =>
            Grid1.ItemsSource as List<FacturaFila> ?? new List<FacturaFila>();

        private FacturaFila ConstruirFila(string id, int linea)
        {
            var fechaObj = Sql.DocumentosFObj.ObtenerItem("fecha", id);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
            string terceroId = Sql.DocumentosFObj.ObtenerItem("tercero", id)?.ToString() ?? "";
            return new FacturaFila
            {
                Linea        = linea,
                Id           = id,
                Codigo       = Sql.DocumentosFObj.ObtenerItem("codigo",     id)?.ToString() ?? "",
                Fecha        = fecha,
                FechaStr     = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                Referencia   = Sql.DocumentosFObj.ObtenerItem("referencia", id)?.ToString() ?? "",
                TerceroDesc  = Sql.TercerosObj.ObtenerItem("descripcion", terceroId)?.ToString() ?? "",
                Movimiento   = (Sql.DocumentosFObj.ObtenerItem("movimiento", id)?.ToString() ?? "venta").ToLower(),
                ImporteTotal = CalcularImporte(id),
                Estado       = Sql.DocumentosFObj.ObtenerItem("estado",  id)?.ToString() ?? "pendiente",
                EstadoC      = Sql.DocumentosFObj.ObtenerItem("estadoC", id)?.ToString() ?? "pendiente"
            };
        }

        private void RenumerarYTotales()
        {
            var lista = FilasGrid;
            int n = 1;
            double totalImporte = 0;
            foreach (var f in lista)
            {
                f.Linea       = n++;
                totalImporte += f.ImporteTotal;
            }
            TxtTotalImporte.Text      = totalImporte.ToString("N2");
            TxtTotalDocumentos.Text   = lista.Count.ToString("N0");
            TxtEstadosPendientes.Text = lista.Count(f => f.Estado == "pendiente").ToString();
            TxtCuentasPendientes.Text = lista.Count(f => f.EstadoC == "pendiente" || f.EstadoC == "pendiente parcial").ToString();
            Grid1.Items.Refresh();
        }

        // ─── Suma el importe total de las líneas de un documento ──────────────
        private static double CalcularImporte(string documentoF)
        {
            double importe = 0;
            int uf = Sql.FacturasObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.FacturasObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.FacturasObj.ObtenerItem("documentoF", id)?.ToString() != documentoF) continue;
                importe += Convert.ToDouble(Sql.FacturasObj.ObtenerItem("importe", id) ?? 0);
            }
            return importe;
        }

        // ─── Panel de detalle de líneas (Lista2) ──────────────────────────────
        private void MostrarDetalle(string documentoF)
        {
            string codigoDoc = Sql.DocumentosFObj.ObtenerItem("codigo", documentoF)?.ToString() ?? documentoF;
            LblDetalleHeader.Text = $"Líneas del documento {codigoDoc}";
            var detalles = new List<FacturaDetalleFila>();
            int linea = 1;

            int uf = Sql.FacturasObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.FacturasObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.FacturasObj.ObtenerItem("documentoF", id)?.ToString() != documentoF) continue;

                string categoriaId = Sql.FacturasObj.ObtenerItem("categoria", id)?.ToString() ?? "";
                string categoriaDesc = string.IsNullOrEmpty(categoriaId)
                    ? ""
                    : Sql.CategoriasObj.ObtenerItem("descripcion", categoriaId)?.ToString() ?? "";

                detalles.Add(new FacturaDetalleFila
                {
                    Linea     = linea++,
                    Concepto  = Sql.FacturasObj.ObtenerItem("concepto", id)?.ToString() ?? "",
                    Categoria = categoriaDesc,
                    Importe   = Convert.ToDouble(Sql.FacturasObj.ObtenerItem("importe", id) ?? 0)
                });
            }

            Lista2.ItemsSource = detalles;
        }

        private void OcultarDetalle()
        {
            LblDetalleHeader.Text = "Líneas del documento";
            Lista2.ItemsSource    = null;
        }

        // ─── Selección en Grid1 → mostrar detalle ────────────────────────────
        private void Grid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Grid1.SelectedItem is FacturaFila fila)
                MostrarDetalle(fila.Id);
            else
                OcultarDetalle();
        }

        // ─── Eventos de árbol ─────────────────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Tree1.SelectedItem is TreeViewItem ti)
                _mesActivo = ti.Tag?.ToString() ?? "";
            _modoFiltro = "filtros";   // Tree1 activo → ignora TxtBuscar
            CargarFacturas();
        }

        private void Tree1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null && source is not TreeViewItem)
                source = VisualTreeHelper.GetParent(source);

            if (source is TreeViewItem tvi && tvi.IsSelected)
            {
                _modoFiltro = "filtros";
                CargarFacturas();
            }
        }

        // ─── Búsqueda (independiente del Tree1) ──────────────────────────────
        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _modoFiltro = "busquedas";
            CargarFacturas();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            _modoFiltro = "busquedas";
            CargarFacturas();
        }

        // ─── Doble clic / Enter = editar ──────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AbrirEditar();
        }

        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = "Nueva Factura";
            string clave  = "nueva-factura";
            var dlg = new FacturasDetalle(this, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                if (dlg.ItemCreadoId == null) return;
                var nueva = ConstruirFila(dlg.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                RenumerarYTotales();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña(titulo, dlg, clave);
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not FacturaFila fila) return;

            // Verificación de conexión en 2 capas antes de persistir el borrado.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            var res = MessageBox.Show("¿Eliminar esta factura y todas sus líneas?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                // Ocultar todas las líneas de este documentoF
                int uf = Sql.FacturasObj.ContarFilas;
                var idsOcultar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.FacturasObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.FacturasObj.ObtenerItem("documentoF", id)?.ToString() == fila.Id)
                        idsOcultar.Add(id);
                }
                foreach (string id in idsOcultar)
                    Sql.FacturasObj.Ocultar(id);

                // Ocultar el documento de factura
                Sql.DocumentosFObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.DocumentosFObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.DocumentosFObj.Ocultar(fila.Id);

                Sql.FacturasObj.OrdenarData(("documentoF", false), ("indice", false));
                Sql.DocumentosFObj.OrdenarData(("fecha", false));

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
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            Sql.DocumentosFObj.Actualizar();
            Sql.FacturasObj.Actualizar();
            CargarFacturas();
        }

        // ─── Helper ───────────────────────────────────────────────────────────
        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not FacturaFila fila) return;

            string idSel = fila.Id;
            int    linea = fila.Linea;

            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = $"Factura {fila.Codigo}";
            var dlg = new FacturasDetalle(this, idSel, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFila(idSel, linea);
                    lista[idx] = actualizada;
                    RenumerarYTotales();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña(titulo, dlg, $"factura-{idSel}");
        }
    }

    // ─── Modelos ──────────────────────────────────────────────────────────────
    public class FacturaFila
    {
        public int      Linea        { get; set; }
        public string   Id           { get; set; } = "";
        public string   Codigo       { get; set; } = "";
        public DateTime Fecha        { get; set; }
        public string   FechaStr     { get; set; } = "";
        public string   Referencia   { get; set; } = "";
        public string   TerceroDesc  { get; set; } = "";
        public string   Movimiento   { get; set; } = "venta";
        public double   ImporteTotal { get; set; }
        public string   Estado       { get; set; } = "pendiente";
        public string   EstadoC      { get; set; } = "pendiente";
    }

    public class FacturaDetalleFila
    {
        public int    Linea     { get; set; }
        public string Concepto  { get; set; } = "";
        public string Categoria { get; set; } = "";
        public double Importe   { get; set; }
    }
}
