using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class TraspasosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private string _mesActivo  = "";
        private string _modoFiltro = "filtros"; // "filtros" = Tree1 | "busquedas" = TxtBuscar

        /// <summary>
        /// Tipo de movimiento fijo para este control ("entrada" o "salida").
        /// Si está vacío, se lee de AppState.TipoMovimiento.
        /// </summary>
        public string TipoMovimiento { get; set; } = "";

        private bool _iniciado = false;

        public TraspasosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarMeses(); CargarTraspasos(); };
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

        // ─── Carga la lista de traspasos ──────────────────────────────────────
        public void CargarTraspasos()
        {
            if (TxtBuscar == null || Grid1 == null) return;

            var lista = new List<TraspasoFila>();
            int linea = 1;
            double totalCant = 0;
            string filtroEstado = ObtenerFiltroEstado();
            // Cada modo aplica solo su propio filtro de contenido (igual que VBA llaveActualisar)
            string busqueda  = _modoFiltro == "busquedas" ? TxtBuscar.Text.Trim().ToLower() : "";
            string mesFiltro = _modoFiltro == "filtros"   ? _mesActivo : "";
            string tipoMov = ObtenerFiltroTipo();

            // ── Actualizar header de columna "Destino"/"Origen"/"Sucursal" (índice 4) ────
            if (Grid1.Columns.Count > 4)
                Grid1.Columns[4].Header = tipoMov switch
                {
                    "salida"  => "Destino",
                    "entrada" => "Origen",
                    _         => "Sucursal"
                };

            int uf = Sql.DocumentosTObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosTObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                // Filtrar por sucursal activa según tipo de movimiento
                string origen  = Sql.DocumentosTObj.ObtenerItem("origen",  id)?.ToString() ?? "";
                string destino = Sql.DocumentosTObj.ObtenerItem("destino", id)?.ToString() ?? "";
                bool esSalida  = origen  == AppState.SucursalActiva.ToString();
                bool esEntrada = destino == AppState.SucursalActiva.ToString();

                string movActual;
                if (tipoMov == "salida")
                {
                    if (!esSalida) continue;
                    movActual = "salida";
                }
                else if (tipoMov == "entrada")
                {
                    if (!esEntrada) continue;
                    movActual = "entrada";
                }
                else
                {
                    if (!esSalida && !esEntrada) continue;
                    movActual = esSalida ? "salida" : "entrada";
                }
                string campOtro = movActual == "salida" ? "destino" : "origen";

                // Filtro por mes (solo en modo "filtros", independiente de TxtBuscar)
                if (!string.IsNullOrEmpty(mesFiltro))
                {
                    var fechaObj2 = Sql.DocumentosTObj.ObtenerItem("fecha", id);
                    if (fechaObj2 == null) continue;
                    DateTime fecha2 = Convert.ToDateTime(fechaObj2);
                    string mesDoc = ObtenerNombreMes(fecha2.Month);
                    if (!string.Equals(mesDoc, mesFiltro, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var fechaDocObj = Sql.DocumentosTObj.ObtenerItem("fecha", id);
                DateTime fechaDoc = fechaDocObj != null ? Convert.ToDateTime(fechaDocObj) : default;

                string otroSucId   = Sql.DocumentosTObj.ObtenerItem(campOtro, id)?.ToString() ?? "";
                string otroSucDesc = Sql.SucursalesObj.ObtenerItem("descripcion", otroSucId)?.ToString() ?? otroSucId;

                string estado  = Sql.DocumentosTObj.ObtenerItem("estado", id)?.ToString() ?? "";
                string emitido = Sql.DocumentosTObj.ObtenerItem("emitido", id)?.ToString() ?? "";

                // Si fue emitido por otra sucursal y está "pendiente" → "pendiente revisar"
                if (emitido != AppState.SucursalActiva.ToString() && estado == "pendiente")
                    estado = "pendiente revisar";

                // Filtro por estado
                if (!string.IsNullOrEmpty(filtroEstado) &&
                    !string.Equals(estado, filtroEstado, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Filtro por búsqueda
                if (!string.IsNullOrEmpty(busqueda))
                    if (!id.Contains(busqueda) && !otroSucDesc.ToLower().Contains(busqueda))
                        continue;

                double cant = CalcularCantidad(id);
                lista.Add(new TraspasoFila
                {
                    Linea        = linea++,
                    DocumentoT   = id,
                    FechaStr     = $"{fechaDoc:d} {fechaDoc:HH:mm:ss}",
                    Movimiento   = movActual,
                    SucursalDesc = otroSucDesc,
                    Estado       = estado,
                    Cantidad     = cant
                });
                totalCant += cant;
            }

            Grid1.ItemsSource = lista;
            TxtTotalCantidad.Text = totalCant.ToString("N0");
            LblTipoMovimiento.Text = tipoMov switch
            {
                "salida"  => "Salidas de Productos",
                "entrada" => "Entradas de Productos",
                _         => "Traspasos (Entradas y Salidas)"
            };

            // Ocultar el panel de detalle al recargar
            OcultarDetalle();
        }

        private string ObtenerFiltroTipo()
        {
            return (CboTipoMovimiento?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() switch
            {
                "entradas" => "entrada",
                "salidas"  => "salida",
                _          => ""
            };
        }

        // ─── Filtro de estado (4 opciones igual que VBA) ─────────────────────
        private string ObtenerFiltroEstado()
        {
            if (BtnFiltroPendiente?.IsChecked        == true) return "pendiente";
            if (BtnFiltroPendienteRevisar?.IsChecked == true) return "pendiente revisar";
            if (BtnFiltroEntregado?.IsChecked        == true) return "entregado";
            return ""; // todos
        }

        // ─── Nombre de mes ────────────────────────────────────────────────────
        private static string ObtenerNombreMes(int mes)
        {
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };
            return mes >= 1 && mes <= 12 ? meses[mes - 1] : "";
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<TraspasoFila> FilasGrid =>
            Grid1.ItemsSource as List<TraspasoFila> ?? new List<TraspasoFila>();

        private TraspasoFila ConstruirFilaTraspaso(string id, int linea)
        {
            string origen   = Sql.DocumentosTObj.ObtenerItem("origen",  id)?.ToString() ?? "";
            bool esSalida   = origen == AppState.SucursalActiva.ToString();
            string campOtro = esSalida ? "destino" : "origen";

            var fechaDocObj = Sql.DocumentosTObj.ObtenerItem("fecha", id);
            DateTime fechaDoc = fechaDocObj != null ? Convert.ToDateTime(fechaDocObj) : default;

            string otroSucId   = Sql.DocumentosTObj.ObtenerItem(campOtro, id)?.ToString() ?? "";
            string otroSucDesc = Sql.SucursalesObj.ObtenerItem("descripcion", otroSucId)?.ToString() ?? otroSucId;

            string estado  = Sql.DocumentosTObj.ObtenerItem("estado",  id)?.ToString() ?? "";
            string emitido = Sql.DocumentosTObj.ObtenerItem("emitido", id)?.ToString() ?? "";
            if (emitido != AppState.SucursalActiva.ToString() && estado == "pendiente")
                estado = "pendiente revisar";

            return new TraspasoFila
            {
                Linea        = linea,
                DocumentoT   = id,
                FechaStr     = $"{fechaDoc:d} {fechaDoc:HH:mm:ss}",
                Movimiento   = esSalida ? "salida" : "entrada",
                SucursalDesc = otroSucDesc,
                Estado       = estado,
                Cantidad     = CalcularCantidad(id)
            };
        }

        private void RenumerarYTotales()
        {
            var lista = FilasGrid;
            int n = 1;
            double totalCant = 0;
            foreach (var f in lista)
            {
                f.Linea    = n++;
                totalCant += f.Cantidad;
            }
            TxtTotalCantidad.Text = totalCant.ToString("N0");
            Grid1.Items.Refresh();
        }

        // ─── Sumar cantidad de artículos del documentoT ───────────────────────
        private static double CalcularCantidad(string documentoT)
        {
            double cant = 0;
            int uf = Sql.TraspasosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.TraspasosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() != documentoT) continue;
                cant += Convert.ToDouble(Sql.TraspasosObj.ObtenerItem("cantidad", id) ?? 0);
            }
            return cant;
        }

        // ─── Panel de detalle artículos (Lista2) ─────────────────────────────

        private void MostrarDetalle(string documentoT)
        {
            var detalles = new List<TraspasoDetalleFila>();
            int linea = 1;

            int uf = Sql.TraspasosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.TraspasosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() != documentoT) continue;

                string artId = Sql.TraspasosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                string codigo = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? artId;

                string desc      = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
                string famId     = Sql.ArticulosObj.ObtenerItem("familia", artId)?.ToString() ?? "";
                string famDesc   = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
                string modelo    = Sql.ArticulosObj.ObtenerItem("modelo", artId)?.ToString() ?? "";
                string descFull  = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

                double cantidad = Convert.ToDouble(Sql.TraspasosObj.ObtenerItem("cantidad", id) ?? 0);

                detalles.Add(new TraspasoDetalleFila
                {
                    Linea       = linea++,
                    Codigo      = codigo,
                    Descripcion = descFull,
                    Cantidad    = cantidad
                });
            }

            Lista2.ItemsSource = detalles;
            PanelDetalle.Visibility = detalles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OcultarDetalle()
        {
            PanelDetalle.Visibility = Visibility.Collapsed;
            Lista2.ItemsSource = null;
        }

        // ─── Selección en Grid1 → mostrar detalle ────────────────────────────
        private void Grid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Grid1.SelectedItem is TraspasoFila fila)
                MostrarDetalle(fila.DocumentoT);
            else
                OcultarDetalle();
        }

        // ─── Eventos de árbol y filtros ───────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Tree1.SelectedItem is TreeViewItem ti)
                _mesActivo = ti.Tag?.ToString() ?? "";
            _modoFiltro = "filtros";   // Tree1 activo → ignora TxtBuscar
            CargarTraspasos();
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
                CargarTraspasos();
            }
        }

        private void FiltroEstado_Checked(object sender, RoutedEventArgs e)
            => CargarTraspasos();

        private void CboTipoMovimiento_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => CargarTraspasos();

        // ─── Búsqueda (independiente del Tree1) ──────────────────────────────
        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _modoFiltro = "busquedas"; // TxtBuscar activo → ignora Tree1
            CargarTraspasos();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            _modoFiltro = "busquedas"; // TxtBuscar activo → ignora Tree1
            CargarTraspasos();
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
            string filtroTipo = ObtenerFiltroTipo();
            AppState.TipoMovimiento = string.IsNullOrEmpty(filtroTipo) ? "entrada" : filtroTipo;
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = "Nuevo Traspaso";
            var dlg = new TraspasosDetalle(this, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                if (dlg.DocumentoCreadoId == null) return;   // cancelado
                var nueva = ConstruirFilaTraspaso(dlg.DocumentoCreadoId, 0);
                FilasGrid.Add(nueva);
                RenumerarYTotales();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña(titulo, dlg, "nuevo-traspaso");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not TraspasoFila fila) return;

            var res = MessageBox.Show("¿Eliminar este traspaso y todos sus artículos?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                Sql.DocumentosTObj.EstablecerItem("edicion",  fila.DocumentoT, DateTime.Now);
                Sql.DocumentosTObj.EstablecerItem("usuarioE", fila.DocumentoT, AppState.UsuarioActivo);
                Sql.DocumentosTObj.Ocultar(fila.DocumentoT);

                int uf = Sql.TraspasosObj.ContarFilas;
                var idsOcultar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.TraspasosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() == fila.DocumentoT)
                        idsOcultar.Add(id);
                }
                foreach (var id in idsOcultar)
                    Sql.TraspasosObj.Ocultar(id);

                Sql.DocumentosTObj.OrdenarData(("fecha", false));
                Sql.TraspasosObj.OrdenarData(("documentoT", false), ("indice", false));

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
                MessageBox.Show($"Error al eliminar: {ex.Message}", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            Sql.DocumentosTObj.Actualizar();
            Sql.TraspasosObj.Actualizar();
            CargarTraspasos();
        }

        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not TraspasoFila fila) return;
            string docSel = fila.DocumentoT;
            int    linea  = fila.Linea;
            AppState.EventoFormularioM = "editar";
            string origenDoc = Sql.DocumentosTObj.ObtenerItem("origen", docSel)?.ToString() ?? "";
            AppState.TipoMovimiento = origenDoc == AppState.SucursalActiva.ToString() ? "salida" : "entrada";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var dlg = new TraspasosDetalle(this, docSel, tituloTab: $"Traspaso {docSel}");
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFilaTraspaso(docSel, linea);
                    lista[idx] = actualizada;
                    RenumerarYTotales();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña($"Traspaso {docSel}", dlg, $"traspaso-{docSel}");
        }
    }

    // ─── Modelos ──────────────────────────────────────────────────────────────
    public class TraspasoFila
    {
        public int    Linea        { get; set; }
        public string DocumentoT   { get; set; } = "";
        public string FechaStr     { get; set; } = "";
        public string Movimiento   { get; set; } = "";
        public string SucursalDesc { get; set; } = "";
        public string Estado       { get; set; } = "";
        public double Cantidad     { get; set; }
    }

    public class TraspasoDetalleFila
    {
        public int    Linea       { get; set; }
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Cantidad    { get; set; }
    }
}
