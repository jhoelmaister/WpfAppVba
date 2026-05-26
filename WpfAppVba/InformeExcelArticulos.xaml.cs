using System;
using System.IO;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.Win32;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class InformeExcelArticulos : Window
    {
        private static SqlData Sql => SqlData.Instance;

        public InformeExcelArticulos()
        {
            InitializeComponent();
            TxtFecha.Text = DateTime.Now.ToString("dd/MM/yyyy");
        }

        private void BtnCrearInforme_Click(object sender, RoutedEventArgs e)
        {
            // ── Validar nombre ────────────────────────────────────────────
            string nombre = TxtNombre.Text.Trim();
            if (string.IsNullOrEmpty(nombre))
            {
                MessageBox.Show("Ingrese un nombre para el informe.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNombre.Focus();
                return;
            }

            // ── Validar fecha ─────────────────────────────────────────────
            if (!DateTime.TryParseExact(TxtFecha.Text.Trim(),
                    new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime fechaCorte))
            {
                MessageBox.Show("Fecha inválida. Use el formato dd/mm/aaaa.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFecha.Focus();
                return;
            }

            // ── Nombre de archivo sugerido ────────────────────────────────
            string ahora = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string nombreArchivo = $"informe{nombre}{ahora}";
            foreach (char c in Path.GetInvalidFileNameChars())
                nombreArchivo = nombreArchivo.Replace(c, '_');

            // ── Explorador de guardado ────────────────────────────────────
            var dlg = new SaveFileDialog
            {
                Title            = "Guardar informe Excel",
                FileName         = nombreArchivo,
                DefaultExt       = ".xlsx",
                Filter           = "Excel (*.xlsx)|*.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dlg.ShowDialog(this) != true) return;

            try
            {
                BtnCrearInforme.IsEnabled = false;
                BtnCrearInforme.Content   = "Generando…";

                GenerarExcel(dlg.FileName, fechaCorte);

                MessageBox.Show($"Informe generado correctamente:\n{dlg.FileName}", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el informe:\n{ex.Message}", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnCrearInforme.IsEnabled = true;
                BtnCrearInforme.Content   = "Crear Informe";
            }
        }

        // ─── Generación del Excel ─────────────────────────────────────────────
        private void GenerarExcel(string filePath, DateTime fechaCorte)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Artículos");

            // ── Encabezados ───────────────────────────────────────────────
            ws.Cell(1, 1).Value = "Código";
            ws.Cell(1, 2).Value = "Categoría";
            ws.Cell(1, 3).Value = "Familia";
            ws.Cell(1, 4).Value = "Descripción Completa";
            ws.Cell(1, 5).Value = "Stock";

            var hRow = ws.Row(1);
            hRow.Style.Font.Bold                  = true;
            hRow.Style.Fill.BackgroundColor        = XLColor.FromHtml("#1A73E8");
            hRow.Style.Font.FontColor              = XLColor.White;
            hRow.Style.Alignment.Horizontal        = XLAlignmentHorizontalValues.Center;

            // ── Datos ─────────────────────────────────────────────────────
            int row = 2;
            int uf  = Sql.ArticulosObj.ContarFilas;

            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string codigo  = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc    = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo  = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
                string famId   = Sql.ArticulosObj.ObtenerItem("familia",     id)?.ToString() ?? "";

                string famDesc  = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
                string prodId   = Sql.FamiliasObj.ObtenerItem("producto",    famId)?.ToString() ?? "";
                string catDesc  = Sql.ProductosObj.ObtenerItem("descripcion", prodId)?.ToString() ?? "";

                string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);
                double stock        = StockCalculator.ContarStock(id, fechaCorte);

                ws.Cell(row, 1).Value = codigo;
                ws.Cell(row, 2).Value = catDesc;
                ws.Cell(row, 3).Value = famDesc;
                ws.Cell(row, 4).Value = descCompleta;
                ws.Cell(row, 5).Value = stock;
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.##";

                // Zebra striping en filas pares
                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");

                row++;
            }

            // ── Ajustar ancho de columnas ─────────────────────────────────
            ws.Columns().AdjustToContents();
            // Limitar descripción completa para que no quede demasiado ancha
            if (ws.Column(4).Width > 60) ws.Column(4).Width = 60;

            // ── Congelar primera fila ─────────────────────────────────────
            ws.SheetView.FreezeRows(1);

            wb.SaveAs(filePath);
        }
    }
}
