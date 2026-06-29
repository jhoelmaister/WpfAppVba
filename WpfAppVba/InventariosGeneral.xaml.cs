using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class InventariosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        private bool _iniciado = false;

        public InventariosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; ConfigurarModo(); CargarInventarios(); };
        }

        private void ConfigurarModo()
        {
            if (!AppState.EsAdmin) BtnEliminar.Visibility = Visibility.Collapsed;
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarInventarios()
        {
            var lista = new List<InventarioFila>();
            int linea = 1;

            int uf = Sql.DocumentosIObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosIObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                var fechaObj = Sql.DocumentosIObj.ObtenerItem("fecha", id);
                DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
                double cantidad = CalcularCantidad(id);

                lista.Add(new InventarioFila
                {
                    Linea         = linea++,
                    Id            = id,
                    Codigo        = Sql.DocumentosIObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                    Fecha         = fecha,
                    FechaStr      = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                    CantidadTotal = cantidad
                });
            }

            Grid1.ItemsSource = lista;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<InventarioFila> FilasGrid =>
            Grid1.ItemsSource as List<InventarioFila> ?? new List<InventarioFila>();

        private InventarioFila ConstruirFilaInventario(string id, int linea)
        {
            var fechaObj = Sql.DocumentosIObj.ObtenerItem("fecha", id);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
            return new InventarioFila
            {
                Linea         = linea,
                Id            = id,
                Codigo        = Sql.DocumentosIObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                Fecha         = fecha,
                FechaStr      = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                CantidadTotal = CalcularCantidad(id)
            };
        }

        private void Renumerar()
        {
            int n = 1;
            foreach (var f in FilasGrid) f.Linea = n++;
            Grid1.Items.Refresh();
        }

        // ─── Calcula la cantidad total de un documento de inventario ──────────
        private double CalcularCantidad(string documentoI)
        {
            double cant = 0;
            int uf = Sql.InventariosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.InventariosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.InventariosObj.ObtenerItem("documentoI", id)?.ToString() != documentoI)
                    continue;

                cant += Convert.ToDouble(Sql.InventariosObj.ObtenerItem("cantidad", id) ?? 0);
            }
            return cant;
        }

        // ─── Doble clic = editar ───────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Tecla Enter = editar ─────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioI = "nuevo";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new InventariosDetalle(this);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                if (detalle.ItemCreadoId == null) return;   // cancelado
                var nueva = ConstruirFilaInventario(detalle.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                Renumerar();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña("Nuevo Inventario", detalle, "nuevo-inventario");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            CargarInventarios();
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not InventarioFila fila) return;

            // Verificación de conexión en 2 capas antes de persistir el borrado.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            // Solo se puede eliminar la apertura más reciente
            if (fila.Id != AppState.AperturaIdActiva)
            {
                MessageBox.Show("Solo se puede eliminar la última apertura.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show("¿Eliminar este inventario de apertura?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                // Ocultar todos los inventarios de este documentoI
                int uf = Sql.InventariosObj.ContarFilas;
                var idsEliminar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.InventariosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.InventariosObj.ObtenerItem("documentoI", id)?.ToString() == fila.Id)
                        idsEliminar.Add(id);
                }
                foreach (string id in idsEliminar)
                    Sql.InventariosObj.Ocultar(id);

                // Ocultar el documento de inventario
                Sql.DocumentosIObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.DocumentosIObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.DocumentosIObj.Ocultar(fila.Id);

                Sql.InventariosObj.OrdenarData(("documentoI", false));
                Sql.DocumentosIObj.OrdenarData(("id", false));

                int periodo = string.IsNullOrEmpty(AppState.PeriodoActivo)
                    ? DateTime.Now.Year
                    : int.Parse(AppState.PeriodoActivo);
                AppState.ActualizarBase(periodo);
                AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);

                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0) lista.RemoveAt(idx);
                Renumerar();

                if (lista.Count > 0)
                {
                    var sel = lista[Math.Min(idx, lista.Count - 1)];
                    Grid1.SelectedItem = sel; Grid1.ScrollIntoView(sel);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Helper ───────────────────────────────────────────────────────────
        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not InventarioFila fila) return;

            // Solo se puede editar la apertura más reciente
            if (fila.Id != AppState.AperturaIdActiva)
            {
                MessageBox.Show("Solo se puede editar la última apertura.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string idSel = fila.Id;
            int    linea = fila.Linea;
            AppState.EventoFormularioI = "editar";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new InventariosDetalle(this, fila.Id);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFilaInventario(idSel, linea);
                    lista[idx] = actualizada;
                    Renumerar();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña($"Inventario {fila.Codigo}", detalle, $"inventario-{idSel}");
        }

        // ─── PDF del inventario (botón "Acciones" por fila, mismo formato que PreciosGeneral) ─
        private void BtnPdfFila_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not InventarioFila fila) return;

            var dlg = new SaveFileDialog
            {
                Title            = "Guardar inventario",
                FileName         = $"{DateTime.Now:yyyyMMdd HHmmss} inventario {fila.Codigo}.pdf",
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

                GenerarPdfInventario(dlg.FileName, fila);

                Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el PDF:\n{ex.Message}", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                if (btn != null) btn.IsEnabled = true;
            }
        }

        // Agrupa las líneas del inventario por producto y familia (encabezado "Producto & Familia"),
        // igual estructura de tabla que GenerarPdfListaPrecios en PreciosGeneral.
        private void GenerarPdfInventario(string filePath, InventarioFila fila)
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;

            var fontTitulo = new XFont("Arial", 14, XFontStyleEx.Bold);
            var fontHeader = new XFont("Arial", 9,  XFontStyleEx.Bold);
            var fontGrupo  = new XFont("Arial", 10, XFontStyleEx.Bold);
            var fontCuerpo = new XFont("Arial", 9,  XFontStyleEx.Regular);

            var brushCeleste = new XSolidBrush(XColor.FromArgb(191, 219, 254));
            var brushHeader  = new XSolidBrush(XColor.FromArgb(241, 245, 249));
            var penLinea     = new XPen(XColors.Black, 0.6);

            const double margen        = 40;
            const double altoHeader    = 20;
            const double altoGrupo     = 18;
            const double altoFila      = 16;
            const double anchoN        = 28;
            const double anchoCodigo   = 65;
            const double anchoCantidad = 75;

            var document = new PdfDocument();
            PdfPage page = document.AddPage();
            page.Size = PageSize.A4;
            XGraphics gfx = XGraphics.FromPdfPage(page);

            double anchoTabla       = page.Width - margen * 2;
            double anchoDescripcion = anchoTabla - anchoN - anchoCodigo - anchoCantidad;
            double xN        = margen;
            double xCodigo   = xN + anchoN;
            double xDesc     = xCodigo + anchoCodigo;
            double xCantidad = xDesc + anchoDescripcion;

            double y = margen;

            // Fila de datos: 4 columnas independientes, cada una con su línea separadora.
            void DibujarFilaDatos(string n, string codigo, string desc, string cantidad)
            {
                gfx.DrawRectangle(penLinea, xN, y, anchoTabla, altoFila);
                gfx.DrawLine(penLinea, xCodigo,   y, xCodigo,   y + altoFila);
                gfx.DrawLine(penLinea, xDesc,     y, xDesc,     y + altoFila);
                gfx.DrawLine(penLinea, xCantidad, y, xCantidad, y + altoFila);

                gfx.DrawString(n,        fontCuerpo, XBrushes.Black, new XRect(xN,           y, anchoN,               altoFila), XStringFormats.Center);
                gfx.DrawString(codigo,   fontCuerpo, XBrushes.Black, new XRect(xCodigo + 4,   y, anchoCodigo - 8,      altoFila), XStringFormats.CenterLeft);
                gfx.DrawString(desc,     fontCuerpo, XBrushes.Black, new XRect(xDesc + 4,     y, anchoDescripcion - 8, altoFila), XStringFormats.CenterLeft);
                gfx.DrawString(cantidad, fontCuerpo, XBrushes.Black, new XRect(xCantidad + 4, y, anchoCantidad - 8,    altoFila), XStringFormats.CenterRight);

                y += altoFila;
            }

            // Encabezado de columnas: igual estructura que una fila de datos, con fondo gris claro.
            void DibujarEncabezadoColumnas()
            {
                gfx.DrawRectangle(penLinea, brushHeader, xN, y, anchoTabla, altoHeader);
                gfx.DrawLine(penLinea, xCodigo,   y, xCodigo,   y + altoHeader);
                gfx.DrawLine(penLinea, xDesc,     y, xDesc,     y + altoHeader);
                gfx.DrawLine(penLinea, xCantidad, y, xCantidad, y + altoHeader);

                gfx.DrawString("N°",          fontHeader, XBrushes.Black, new XRect(xN,           y, anchoN,               altoHeader), XStringFormats.Center);
                gfx.DrawString("Código",      fontHeader, XBrushes.Black, new XRect(xCodigo + 4,   y, anchoCodigo - 8,      altoHeader), XStringFormats.CenterLeft);
                gfx.DrawString("Descripción", fontHeader, XBrushes.Black, new XRect(xDesc + 4,     y, anchoDescripcion - 8, altoHeader), XStringFormats.CenterLeft);
                gfx.DrawString("Cantidad",    fontHeader, XBrushes.Black, new XRect(xCantidad + 4, y, anchoCantidad - 8,    altoHeader), XStringFormats.CenterRight);

                y += altoHeader;
            }

            // Banda de agrupación: una sola celda celeste de ancho completo (sin separadores
            // internos), para que el subtítulo resalte como una franja y no como otra fila de la tabla.
            void DibujarBandaGrupo(string texto)
            {
                gfx.DrawRectangle(penLinea, brushCeleste, xN, y, anchoTabla, altoGrupo);
                gfx.DrawString(texto, fontGrupo, XBrushes.Black, new XRect(xN + 6, y, anchoTabla - 12, altoGrupo), XStringFormats.CenterLeft);
                y += altoGrupo;
            }

            void NuevaPagina()
            {
                page = document.AddPage();
                page.Size = PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = margen;
                DibujarEncabezadoColumnas();
            }

            void AsegurarEspacio(double alto)
            {
                if (y + alto > page.Height - margen) NuevaPagina();
            }

            string fechaHora = fila.FechaStr;
            string tituloPrincipal = string.IsNullOrEmpty(fechaHora)
                ? $"Inventario — {fila.Codigo}"
                : $"Inventario — {fila.Codigo}   ({fechaHora})";
            gfx.DrawString(tituloPrincipal, fontTitulo, XBrushes.Black, new XPoint(margen, y + 12));
            y += 28;

            DibujarEncabezadoColumnas();

            var lineas = new List<(string prodDesc, string famDesc, string codigo, string desc, double cantidad)>();

            int uf = Sql.InventariosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.InventariosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.InventariosObj.ObtenerItem("documentoI", id)?.ToString() != fila.Id) continue;

                string artId  = Sql.InventariosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                string famId  = Sql.ArticulosObj.ObtenerItem("familia", artId)?.ToString() ?? "";
                string prodId = Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "";

                string prodDesc     = Sql.ProductosObj.ObtenerItem("descripcion", prodId)?.ToString() ?? "";
                string famDesc      = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString()  ?? "";
                string codigo       = Sql.ArticulosObj.ObtenerItem("codigo",      artId)?.ToString()  ?? "";
                string descArt      = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString()  ?? "";
                string modelo       = Sql.ArticulosObj.ObtenerItem("modelo",      artId)?.ToString()  ?? "";
                string descCompleta = FuncionesComunes.UnirVariables(descArt, famDesc, modelo);
                double cantidad     = Convert.ToDouble(Sql.InventariosObj.ObtenerItem("cantidad", id) ?? 0);
                if (cantidad <= 0) continue;

                lineas.Add((prodDesc, famDesc, codigo, descCompleta, cantidad));
            }

            var grupos = lineas
                .GroupBy(l => (l.prodDesc, l.famDesc))
                .OrderBy(g => g.Key.prodDesc, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key.famDesc, StringComparer.OrdinalIgnoreCase);

            int n = 0;
            foreach (var grupo in grupos)
            {
                AsegurarEspacio(altoGrupo + altoFila);
                DibujarBandaGrupo($"{grupo.Key.prodDesc} & {grupo.Key.famDesc}");

                foreach (var l in grupo)
                {
                    AsegurarEspacio(altoFila);
                    n++;
                    DibujarFilaDatos(n.ToString(), l.codigo, l.desc, l.cantidad.ToString("#,##0.##"));
                }
            }

            document.Save(filePath);
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class InventarioFila
    {
        public int      Linea         { get; set; }
        public string   Id            { get; set; } = "";
        public string   Codigo        { get; set; } = "";
        public DateTime Fecha         { get; set; }
        public string   FechaStr      { get; set; } = "";
        public double   CantidadTotal { get; set; }
    }
}
