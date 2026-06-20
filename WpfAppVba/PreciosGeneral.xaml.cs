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
    public partial class PreciosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private string _mesActivo  = "";
        private string _modoFiltro = "filtros"; // "filtros" = Tree1 | "busquedas" = TxtBuscar

        private bool _iniciado = false;

        public event Action? Cerrando;
        public void IntentarCerrar() => Cerrando?.Invoke();

        public PreciosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; ConfigurarModo(); CargarRegiones(); CargarMeses(); CargarListas(); };
        }

        private void ConfigurarModo()
        {
            if (!AppState.EsAdmin)
            {
                BtnNuevo.Visibility    = Visibility.Collapsed;
                BtnEditar.Visibility   = Visibility.Collapsed;
                BtnEliminar.Visibility = Visibility.Collapsed;
            }
        }

        // ─── Combo de regiones (filtro) ───────────────────────────────────────
        private void CargarRegiones()
        {
            var lista = new List<RegionItem> { new RegionItem { Id = "", Descripcion = "Todas las regiones" } };

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
            CboRegion.SelectedIndex     = 0;
        }

        // ─── Carga el árbol de meses ──────────────────────────────────────────
        private void CargarMeses()
        {
            Tree1.Items.Clear();
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };

            var nodoGeneral = new TreeViewItem
            {
                Header     = "General",
                Tag        = "",
                IsExpanded = true
            };
            foreach (var mes in meses)
                nodoGeneral.Items.Add(new TreeViewItem { Header = mes, Tag = mes });

            Tree1.Items.Add(nodoGeneral);

            int mesActual = DateTime.Now.Month - 1;
            if (nodoGeneral.Items[mesActual] is TreeViewItem ti)
            {
                ti.IsSelected = true;
                _mesActivo = meses[mesActual];
            }
        }

        // ─── Carga la lista de documentos de precios (documentosL) ───────────
        public void CargarListas()
        {
            if (TxtBuscar == null || Grid1 == null) return;

            var lista = new List<PrecioListaFila>();
            int linea = 1;
            string busqueda  = _modoFiltro == "busquedas" ? TxtBuscar.Text.Trim().ToLower() : "";
            string mesFiltro = _modoFiltro == "filtros"   ? _mesActivo : "";
            string regionId  = CboRegion?.SelectedValue?.ToString() ?? "";

            int uf = Sql.DocumentosLObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosLObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string region = Sql.DocumentosLObj.ObtenerItem("region", id)?.ToString() ?? "";
                if (!string.IsNullOrEmpty(regionId) && region != regionId) continue;

                var fechaDocObj = Sql.DocumentosLObj.ObtenerItem("fecha", id);
                DateTime fechaDoc = fechaDocObj != null ? Convert.ToDateTime(fechaDocObj) : default;
                if (!string.IsNullOrEmpty(mesFiltro))
                {
                    if (fechaDocObj == null) continue;
                    string mesDoc = ObtenerNombreMes(fechaDoc.Month);
                    if (!string.Equals(mesDoc, mesFiltro, StringComparison.OrdinalIgnoreCase)) continue;
                }

                string codigo      = Sql.DocumentosLObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string observacion = Sql.DocumentosLObj.ObtenerItem("observacion", id)?.ToString() ?? "";

                if (!string.IsNullOrEmpty(busqueda))
                    if (!codigo.ToLower().Contains(busqueda) && !observacion.ToLower().Contains(busqueda))
                        continue;

                lista.Add(ConstruirFila(id, linea++));
            }

            Grid1.ItemsSource       = lista;
            TxtTotalDocumentos.Text = lista.Count.ToString("N0");
            TxtTotalLineas.Text     = lista.Sum(f => f.Lineas).ToString("N0");

            int año = AppState.DataFechaFinal.Year > 2000
                ? AppState.DataFechaFinal.Year
                : DateTime.Now.Year;
            LblSubtitulo.Text = string.IsNullOrEmpty(_mesActivo)
                ? año.ToString()
                : $"{_mesActivo} {año}";

            OcultarDetalle();
        }

        // ─── Nombre de mes ────────────────────────────────────────────────────
        private static string ObtenerNombreMes(int mes)
        {
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };
            return mes >= 1 && mes <= 12 ? meses[mes - 1] : "";
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<PrecioListaFila> FilasGrid =>
            Grid1.ItemsSource as List<PrecioListaFila> ?? new List<PrecioListaFila>();

        private PrecioListaFila ConstruirFila(string id, int linea)
        {
            var fechaObj = Sql.DocumentosLObj.ObtenerItem("fecha", id);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
            string region  = Sql.DocumentosLObj.ObtenerItem("region", id)?.ToString() ?? "";
            var (cantLineas, valor) = CalcularTotales(id);
            return new PrecioListaFila
            {
                Linea      = linea,
                Id         = id,
                Codigo     = Sql.DocumentosLObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                Fecha      = fecha,
                FechaStr   = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                RegionDesc = Sql.RegionesObj.ObtenerItem("descripcion", region)?.ToString() ?? "",
                Lineas     = cantLineas,
                ValorTotal = valor
            };
        }

        private void RenumerarYTotales()
        {
            var lista = FilasGrid;
            int n = 1;
            foreach (var f in lista) f.Linea = n++;
            TxtTotalDocumentos.Text = lista.Count.ToString("N0");
            TxtTotalLineas.Text     = lista.Sum(f => f.Lineas).ToString("N0");
            Grid1.Items.Refresh();
        }

        // ─── Cuenta líneas y suma el valor de un documentoL ───────────────────
        private static (int lineas, double valor) CalcularTotales(string documentoL)
        {
            int lineas = 0;
            double valor = 0;
            int uf = Sql.PreciosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PreciosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PreciosObj.ObtenerItem("documentoL", id)?.ToString() != documentoL) continue;
                lineas++;
                valor += Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", id) ?? 0);
            }
            return (lineas, valor);
        }

        // ─── Panel de detalle artículos (Lista2) ─────────────────────────────
        private void MostrarDetalle(string documentoL)
        {
            string codigoDoc = Sql.DocumentosLObj.ObtenerItem("codigo", documentoL)?.ToString() ?? documentoL;
            LblDetalleHeader.Text = $"Artículos de la lista {codigoDoc}";
            var detalles = new List<PrecioDetalleFila>();
            int linea = 1;

            int uf = Sql.PreciosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PreciosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PreciosObj.ObtenerItem("documentoL", id)?.ToString() != documentoL) continue;

                string artId  = Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                string codigo = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? artId;

                string desc     = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
                string famId    = Sql.ArticulosObj.ObtenerItem("familia", artId)?.ToString() ?? "";
                string famDesc  = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
                string modelo   = Sql.ArticulosObj.ObtenerItem("modelo", artId)?.ToString() ?? "";
                string descFull = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

                double precio = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", id) ?? 0);

                detalles.Add(new PrecioDetalleFila
                {
                    Linea       = linea++,
                    Codigo      = codigo,
                    Descripcion = descFull,
                    Precio      = precio
                });
            }

            Lista2.ItemsSource = detalles;
        }

        private void OcultarDetalle()
        {
            LblDetalleHeader.Text = "Artículos de la lista";
            Lista2.ItemsSource    = null;
        }

        // ─── Selección en Grid1 → mostrar detalle ────────────────────────────
        private void Grid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Grid1.SelectedItem is PrecioListaFila fila)
                MostrarDetalle(fila.Id);
            else
                OcultarDetalle();
        }

        // ─── Eventos de árbol ─────────────────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Tree1.SelectedItem is TreeViewItem ti)
                _mesActivo = ti.Tag?.ToString() ?? "";
            _modoFiltro = "filtros";
            CargarListas();
        }

        private void Tree1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null && source is not TreeViewItem)
                source = VisualTreeHelper.GetParent(source);

            if (source is TreeViewItem tvi && tvi.IsSelected)
            {
                _modoFiltro = "filtros";
                CargarListas();
            }
        }

        private void CboRegion_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => CargarListas();

        // ─── Búsqueda (independiente del Tree1) ──────────────────────────────
        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _modoFiltro = "busquedas";
            CargarListas();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            _modoFiltro = "busquedas";
            CargarListas();
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
            if (!AppState.EsAdmin) return;
            AppState.EventoFormularioL = "nuevo";

            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = "Nueva Lista de Precios";
            string clave  = "nueva-lista-precios";
            var dlg = new PreciosDetalle(this, tituloTab: titulo);
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
        {
            if (!AppState.EsAdmin) return;
            AbrirEditar();
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.EsAdmin) return;
            if (Grid1.SelectedItem is not PrecioListaFila fila) return;

            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            var res = MessageBox.Show("¿Eliminar esta lista de precios y todas sus líneas?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                int uf = Sql.PreciosObj.ContarFilas;
                var idsOcultar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.PreciosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.PreciosObj.ObtenerItem("documentoL", id)?.ToString() == fila.Id)
                        idsOcultar.Add(id);
                }
                foreach (string id in idsOcultar)
                    Sql.PreciosObj.Ocultar(id);

                Sql.DocumentosLObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.DocumentosLObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.DocumentosLObj.Ocultar(fila.Id);

                Sql.PreciosObj.OrdenarData(("documentoL", false), ("indice", false));
                Sql.DocumentosLObj.OrdenarData(("fecha", false));

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
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            Sql.DocumentosLObj.Actualizar();
            Sql.PreciosObj.Actualizar();
            CargarRegiones();
            CargarListas();
        }

        // ─── Helper ───────────────────────────────────────────────────────────
        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not PrecioListaFila fila) return;

            string idSel = fila.Id;
            int    linea = fila.Linea;
            AppState.EventoFormularioL = "editar";

            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = $"Lista de Precios {fila.Codigo}";
            var dlg = new PreciosDetalle(this, idSel, tituloTab: titulo);
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
            consola.AbrirPestaña(titulo, dlg, $"lista-precios-{idSel}");
        }
    }

    // ─── Modelos ──────────────────────────────────────────────────────────────
    public class PrecioListaFila
    {
        public int      Linea      { get; set; }
        public string   Id         { get; set; } = "";
        public string   Codigo     { get; set; } = "";
        public DateTime Fecha      { get; set; }
        public string   FechaStr   { get; set; } = "";
        public string   RegionDesc { get; set; } = "";
        public int      Lineas     { get; set; }
        public double   ValorTotal { get; set; }
    }

    public class PrecioDetalleFila
    {
        public int    Linea       { get; set; }
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public double Precio      { get; set; }
    }

    public class RegionItem
    {
        public string Id          { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
