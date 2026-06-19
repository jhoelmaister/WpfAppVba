using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class PreciosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        // Modo de filtro activo: "todos" | "busqueda" | "familia"
        private string _modoFiltro = "todos";
        private bool _iniciado = false;

        public event Action? Cerrando;

        public PreciosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; ConfigurarModo(); CargarRegiones(); CargarArbol(); CargarArticulos(); };
        }

        private void ConfigurarModo()
        {
            if (!AppState.EsAdmin)
            {
                BtnNuevoPrecio.Visibility    = Visibility.Collapsed;
                BtnEditarPrecio.Visibility   = Visibility.Collapsed;
                BtnEliminarPrecio.Visibility = Visibility.Collapsed;
            }
        }

        public void IntentarCerrar() => Cerrando?.Invoke();

        // ─── Carga el selector de regiones ───────────────────────────────────
        private void CargarRegiones()
        {
            var lista = new List<RegionItem>();
            int uf = Sql.RegionesObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.RegionesObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                string desc = Sql.RegionesObj.ObtenerItem("descripcion", id)?.ToString() ?? id;
                lista.Add(new RegionItem { Id = id, Descripcion = desc });
            }
            CmbRegion.ItemsSource = lista;
            if (lista.Count > 0) CmbRegion.SelectedIndex = 0;
        }

        // ─── Región actualmente seleccionada en el filtro ─────────────────────
        private RegionItem? RegionSeleccionada => CmbRegion.SelectedItem as RegionItem;
        private string RegionSeleccionadaId   => RegionSeleccionada?.Id ?? "";
        private string RegionSeleccionadaDesc => RegionSeleccionada?.Descripcion ?? "";

        private void CmbRegion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArticuloSeleccionado is PrecioArticuloFila fila)
                CargarPrecios(fila.Id);
        }

        // ─── Árbol de productos/familias (igual a ArticulosGeneral) ───────────
        private void CargarArbol()
        {
            Tree1.Items.Clear();

            var nodoTodos = new TreeViewItem { Header = "Todos", Tag = "todos" };

            int ufProd = Sql.ProductosObj.ContarFilas;
            for (int i = 1; i <= ufProd; i++)
            {
                var idObj = Sql.ProductosObj.Mover(i);
                if (idObj == null) continue;
                string prodId = idObj.ToString()!;
                string prodDesc = Sql.ProductosObj.ObtenerItem("descripcion", prodId)?.ToString() ?? prodId;

                var nodoProd = new TreeViewItem
                {
                    Header = prodDesc,
                    Tag    = $"producto:{prodId}"
                };

                int ufFam = Sql.FamiliasObj.ContarFilas;
                for (int j = 1; j <= ufFam; j++)
                {
                    var famIdObj = Sql.FamiliasObj.Mover(j);
                    if (famIdObj == null) continue;
                    string famId = famIdObj.ToString()!;
                    string famProd = Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "";
                    if (famProd != prodId) continue;

                    string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? famId;
                    nodoProd.Items.Add(new TreeViewItem
                    {
                        Header = famDesc,
                        Tag    = $"familia:{famId}"
                    });
                }

                nodoProd.IsExpanded = true;
                nodoTodos.Items.Add(nodoProd);
            }

            nodoTodos.IsExpanded = true;
            Tree1.Items.Add(nodoTodos);

            nodoTodos.IsSelected = true;
        }

        // ─── Carga la lista de artículos (filtrada por árbol/búsqueda) ────────
        public void CargarArticulos()
        {
            var lista = new List<PrecioArticuloFila>();
            int linea = 1;

            string busqueda  = _modoFiltro == "busqueda" ? TxtBuscar.Text.Trim().ToLower() : "";
            string tagFiltro = _modoFiltro == "familia"  ? ObtenerTagFiltro()              : "";

            int uf = Sql.ArticulosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string famId = Sql.ArticulosObj.ObtenerItem("familia", id)?.ToString() ?? "";

                // Filtro de árbol
                if (!string.IsNullOrEmpty(tagFiltro))
                {
                    if (tagFiltro.StartsWith("familia:"))
                    {
                        string famFiltro = tagFiltro.Substring("familia:".Length);
                        if (famId != famFiltro) continue;
                    }
                    else if (tagFiltro.StartsWith("producto:"))
                    {
                        string prodFiltro = tagFiltro.Substring("producto:".Length);
                        string famProd    = Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "";
                        if (famProd != prodFiltro) continue;
                    }
                }

                string codigo  = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc    = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo  = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
                string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
                string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

                // Filtro de búsqueda
                if (!string.IsNullOrEmpty(busqueda))
                {
                    if (!codigo.ToLower().Contains(busqueda) &&
                        !descCompleta.ToLower().Contains(busqueda))
                        continue;
                }

                lista.Add(new PrecioArticuloFila
                {
                    Linea       = linea++,
                    Id          = id,
                    Codigo      = codigo,
                    Descripcion = descCompleta,
                    Estado      = Sql.ArticulosObj.ObtenerItem("estado", id)?.ToString() ?? ""
                });
            }

            Grid1.ItemsSource = lista;
            GridPrecios.ItemsSource = null;
        }

        // ─── Obtener filtro del árbol ─────────────────────────────────────────
        private string ObtenerTagFiltro()
        {
            if (Tree1.SelectedItem is TreeViewItem item)
                return item.Tag?.ToString() ?? "todos";
            return "todos";
        }

        // ─── Carga el historial de precios del artículo seleccionado ──────────
        private void CargarPrecios(string articuloId)
        {
            var lista = new List<PrecioHistFila>();
            int linea = 1;
            string regionFiltro = RegionSeleccionadaId;

            int uf = Sql.PreciosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PreciosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() != articuloId) continue;
                if (!string.IsNullOrEmpty(regionFiltro) &&
                    (Sql.PreciosObj.ObtenerItem("region", id)?.ToString() ?? "") != regionFiltro) continue;

                lista.Add(ConstruirFilaPrecio(id, linea++));
            }

            GridPrecios.ItemsSource = lista;
        }

        private PrecioHistFila ConstruirFilaPrecio(string id, int linea)
        {
            var fechaObj = Sql.PreciosObj.ObtenerItem("fecha", id);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
            string regionId   = Sql.PreciosObj.ObtenerItem("region", id)?.ToString() ?? "";
            string regionDesc = Sql.RegionesObj.ObtenerItem("descripcion", regionId)?.ToString() ?? regionId;
            string articuloId = Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() ?? "";

            return new PrecioHistFila
            {
                Linea    = linea,
                Id       = id,
                Codigo   = Sql.PreciosObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                Fecha    = fecha,
                FechaStr = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                Region   = regionDesc,
                Precio   = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", id) ?? 0)
            };
        }

        // ─── Lista del historial de precios en memoria ────────────────────────
        private List<PrecioHistFila> PreciosGrid =>
            GridPrecios.ItemsSource as List<PrecioHistFila> ?? new List<PrecioHistFila>();

        private void RenumerarPrecios()
        {
            int n = 1;
            foreach (var f in PreciosGrid) f.Linea = n++;
            GridPrecios.Items.Refresh();
        }

        // ─── Artículo actualmente seleccionado ────────────────────────────────
        private PrecioArticuloFila? ArticuloSeleccionado => Grid1.SelectedItem as PrecioArticuloFila;

        // ─── Eventos árbol y búsqueda ─────────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _modoFiltro = "familia";
            CargarArticulos();
        }

        private void Tree1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            while (source != null && source is not TreeViewItem)
                source = VisualTreeHelper.GetParent(source);

            if (source is TreeViewItem tvi && tvi.IsSelected)
            {
                _modoFiltro = "familia";
                CargarArticulos();
            }
        }

        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _modoFiltro = "busqueda";
            CargarArticulos();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            _modoFiltro = "busqueda";
            CargarArticulos();
        }

        // ─── Al seleccionar un artículo → cargar su historial ─────────────────
        private void Grid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArticuloSeleccionado is not PrecioArticuloFila fila)
            {
                GridPrecios.ItemsSource = null;
                return;
            }
            CargarPrecios(fila.Id);
        }

        // ─── Doble clic / Enter en grid de artículos → nuevo precio ───────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            BtnNuevoPrecio_Click(sender, e);
        }

        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnNuevoPrecio_Click(sender, e);
        }

        // ─── Doble clic / Enter en grid de precios → editar precio ────────────
        private void GridPrecios_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            BtnEditarPrecio_Click(sender, e);
        }

        private void GridPrecios_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnEditarPrecio_Click(sender, e);
        }

        // ─── Botones de precio ────────────────────────────────────────────────
        private void BtnNuevoPrecio_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.EsAdmin) return;
            if (ArticuloSeleccionado is not PrecioArticuloFila fila)
            {
                MessageBox.Show("Seleccione un artículo primero.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(RegionSeleccionadaId))
            {
                MessageBox.Show("Seleccione una región primero.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;

            var detalle = new PreciosDetalle(fila.Id, fila.Codigo, fila.Descripcion,
                                             "", RegionSeleccionadaId, RegionSeleccionadaDesc);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                if (detalle.ItemCreadoId == null) return;
                var nueva = ConstruirFilaPrecio(detalle.ItemCreadoId, 0);
                PreciosGrid.Add(nueva);
                RenumerarPrecios();
                GridPrecios.SelectedItem = nueva; GridPrecios.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(GridPrecios);
            };
            consola.AbrirPestaña("Nuevo Precio", detalle, $"nuevo-precio-{fila.Id}");
        }

        private void BtnEditarPrecio_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.EsAdmin) return;
            if (ArticuloSeleccionado is not PrecioArticuloFila art) return;
            if (GridPrecios.SelectedItem is not PrecioHistFila fila) return;

            string idSel = fila.Id;
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new PreciosDetalle(art.Id, art.Codigo, art.Descripcion, fila.Id);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                var lista = PreciosGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFilaPrecio(idSel, fila.Linea);
                    lista[idx] = actualizada;
                    RenumerarPrecios();
                    GridPrecios.SelectedItem = actualizada; GridPrecios.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(GridPrecios);
            };
            consola.AbrirPestaña($"Precio {art.Codigo}", detalle, $"precio-{idSel}");
        }

        private void BtnEliminarPrecio_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.EsAdmin) return;
            if (GridPrecios.SelectedItem is not PrecioHistFila fila) return;

            // Verificación de conexión en 2 capas antes de persistir el borrado.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            var res = MessageBox.Show("¿Eliminar este registro de precio?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            Sql.PreciosObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
            Sql.PreciosObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
            Sql.PreciosObj.Ocultar(fila.Id);
            Sql.PreciosObj.OrdenarData(("fecha", false));

            var lista = PreciosGrid;
            int idx   = lista.IndexOf(fila);
            if (idx >= 0) lista.RemoveAt(idx);
            RenumerarPrecios();

            if (lista.Count > 0)
            {
                var sel = lista[Math.Min(idx, lista.Count - 1)];
                GridPrecios.SelectedItem = sel; GridPrecios.ScrollIntoView(sel);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(GridPrecios);
        }

        // ─── Cambiar estado del artículo (mostrar/ocultar) ────────────────────
        private void BtnCambiarEstado_Click(object sender, RoutedEventArgs e)
        {
            // Verificación de conexión en 2 capas antes de persistir el cambio de estado.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            if (ArticuloSeleccionado is not PrecioArticuloFila fila)
            {
                MessageBox.Show("Seleccione un artículo primero.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string actual = Sql.ArticulosObj.ObtenerItem("estado", fila.Id)?.ToString() ?? "mostrar";
            string nuevo  = actual == "mostrar" ? "ocultar" : "mostrar";

            Sql.ArticulosObj.EstablecerItem("estado",   fila.Id, nuevo);
            Sql.ArticulosObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
            Sql.ArticulosObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
            Sql.ArticulosObj.OrdenarData(("familia", false), ("indice", false));

            fila.Estado = nuevo;
            Grid1.Items.Refresh();
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            Sql.ArticulosObj.Actualizar();
            Sql.PreciosObj.Actualizar();
            CargarArbol();
            CargarArticulos();
        }

        // ─── Lista de precios en PDF (agrupada por familia) ───────────────────
        private void BtnListaPreciosPdf_Click(object sender, RoutedEventArgs e)
        {
            string regionId   = RegionSeleccionadaId;
            string regionDesc = RegionSeleccionadaDesc;
            if (string.IsNullOrEmpty(regionId))
            {
                MessageBox.Show("Seleccione una región primero.", "Lista de precios",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title            = "Guardar lista de precios",
                FileName         = $"{DateTime.Now:yyyyMMdd HHmmss} lista de precios {regionDesc}.pdf",
                DefaultExt       = ".pdf",
                Filter           = "PDF (*.pdf)|*.pdf",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

            var btn = sender as Button;
            try
            {
                if (btn != null) btn.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;

                GenerarPdfListaPrecios(dlg.FileName, regionId, regionDesc);

                MessageBox.Show("Lista de precios generada correctamente.", "Lista de precios",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar la lista de precios:\n{ex.Message}", "Lista de precios",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private void GenerarPdfListaPrecios(string filePath, string regionId, string regionDesc)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;

            var fontTitulo = new XFont("Arial", 14, XFontStyleEx.Bold);
            var fontGrupo  = new XFont("Arial", 11, XFontStyleEx.Bold);
            var fontCuerpo = new XFont("Arial", 10, XFontStyleEx.Regular);

            const double margen = 40;
            var document = new PdfDocument();
            PdfPage page = document.AddPage();
            page.Size = PageSize.A4;
            XGraphics gfx = XGraphics.FromPdfPage(page);
            double y = margen;

            void NuevaPagina()
            {
                page = document.AddPage();
                page.Size = PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = margen;
            }

            void AsegurarEspacio(double alto)
            {
                if (y + alto > page.Height - margen) NuevaPagina();
            }

            gfx.DrawString($"Lista de Precios — {regionDesc}", fontTitulo, XBrushes.Black, new XPoint(margen, y));
            y += 22;

            int ufProd = Sql.ProductosObj.ContarFilas;
            for (int i = 1; i <= ufProd; i++)
            {
                var prodIdObj = Sql.ProductosObj.Mover(i);
                if (prodIdObj == null) continue;
                string prodId   = prodIdObj.ToString()!;
                string prodDesc = Sql.ProductosObj.ObtenerItem("descripcion", prodId)?.ToString() ?? prodId;

                int ufFam = Sql.FamiliasObj.ContarFilas;
                for (int j = 1; j <= ufFam; j++)
                {
                    var famIdObj = Sql.FamiliasObj.Mover(j);
                    if (famIdObj == null) continue;
                    string famId = famIdObj.ToString()!;
                    if ((Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "") != prodId) continue;
                    string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? famId;

                    // Artículos visibles ("mostrar") de esta familia
                    var articulos = new List<(string codigo, string desc, double precio)>();
                    int ufArt = Sql.ArticulosObj.ContarFilas;
                    for (int k = 1; k <= ufArt; k++)
                    {
                        var artIdObj = Sql.ArticulosObj.Mover(k);
                        if (artIdObj == null) continue;
                        string artId = artIdObj.ToString()!;
                        if ((Sql.ArticulosObj.ObtenerItem("familia", artId)?.ToString() ?? "") != famId) continue;
                        if ((Sql.ArticulosObj.ObtenerItem("estado",  artId)?.ToString() ?? "") != "mostrar") continue;

                        string codigo = Sql.ArticulosObj.ObtenerItem("codigo",      artId)?.ToString() ?? "";
                        string desc   = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
                        double precio = ObtenerPrecioVigente(artId, regionId);
                        articulos.Add((codigo, desc, precio));
                    }

                    if (articulos.Count == 0) continue;

                    AsegurarEspacio(18 + 14);
                    gfx.DrawString($"{prodDesc} & {famDesc}", fontGrupo, XBrushes.Black, new XPoint(margen, y));
                    y += 18;

                    foreach (var art in articulos)
                    {
                        AsegurarEspacio(14);
                        gfx.DrawString($"{art.codigo}  {art.desc}", fontCuerpo, XBrushes.Black, new XPoint(margen + 12, y));
                        string precioStr = art.precio.ToString("#,##0.##");
                        XSize sz = gfx.MeasureString(precioStr, fontCuerpo);
                        gfx.DrawString(precioStr, fontCuerpo, XBrushes.Black,
                            new XPoint(page.Width - margen - sz.Width, y));
                        y += 14;
                    }

                    y += 10;
                }
            }

            document.Save(filePath);
        }

        // ─── Precio vigente (más reciente) de un artículo en una región ───────
        private double ObtenerPrecioVigente(string articuloId, string regionId)
        {
            double precio = 0;
            DateTime mejorFecha = default;

            int uf = Sql.PreciosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PreciosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() != articuloId) continue;
                if ((Sql.PreciosObj.ObtenerItem("region", id)?.ToString() ?? "") != regionId) continue;

                var fechaObj = Sql.PreciosObj.ObtenerItem("fecha", id);
                DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
                if (fecha >= mejorFecha)
                {
                    mejorFecha = fecha;
                    precio = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", id) ?? 0);
                }
            }

            return precio;
        }
    }

    // ─── Modelos de fila ──────────────────────────────────────────────────────
    public class PrecioArticuloFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Estado      { get; set; } = "";
    }

    public class PrecioHistFila
    {
        public int      Linea    { get; set; }
        public string   Id       { get; set; } = "";
        public string   Codigo   { get; set; } = "";
        public DateTime Fecha    { get; set; }
        public string   FechaStr { get; set; } = "";
        public string   Region   { get; set; } = "";
        public double   Precio   { get; set; }
    }

    public class RegionItem
    {
        public string Id          { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
