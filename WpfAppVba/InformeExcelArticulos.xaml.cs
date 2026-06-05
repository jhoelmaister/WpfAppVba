using System;
using System.Collections.Generic;
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

            // ── Encabezados (solo texto, sin estilos) ─────────────────────
            ws.Cell(1, 1).Value = "Id";
            ws.Cell(1, 2).Value = "Categoría";
            ws.Cell(1, 3).Value = "Familia";
            ws.Cell(1, 4).Value = "Descripción Completa";
            ws.Cell(1, 5).Value = "Stock";

            // ── Recolectar datos ──────────────────────────────────────────
            int uf = Sql.ArticulosObj.ContarFilas;
            var datos = new List<(string id, string prodDesc, string catDesc, string famDesc, string descCompleta, double stock)>();

            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc  = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo = Sql.ArticulosObj.ObtenerItem("modelo",     id)?.ToString() ?? "";
                string famId = Sql.ArticulosObj.ObtenerItem("familia",     id)?.ToString() ?? "";
                string catId = Sql.ArticulosObj.ObtenerItem("Categoria",   id)?.ToString() ?? "";

                string famDesc  = Sql.FamiliasObj.ObtenerItem("descripcion",   famId)?.ToString() ?? "";
                string prodId   = Sql.FamiliasObj.ObtenerItem("producto",      famId)?.ToString() ?? "";
                string prodDesc = Sql.ProductosObj.ObtenerItem("descripcion",  prodId)?.ToString() ?? "";
                string catDesc  = Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "";

                string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);
                double stock        = StockCalculator.ContarStock(id, fechaCorte);

                datos.Add((id, prodDesc, catDesc, famDesc, descCompleta, stock));
            }

            // ── Ordenar por Producto → Familia → Id ──────────────────────
            datos.Sort((a, b) =>
            {
                int cmp = string.Compare(a.prodDesc, b.prodDesc, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                cmp = string.Compare(a.famDesc, b.famDesc, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                return string.Compare(a.id, b.id, StringComparison.OrdinalIgnoreCase);
            });

            // ── Escribir datos agrupados por Producto ─────────────────────
            int row = 2;
            string currentProduct = null!;

            foreach (var item in datos)
            {
                if (item.prodDesc != currentProduct)
                {
                    currentProduct = item.prodDesc;
                    ws.Cell(row, 1).Value = item.prodDesc;
                    row++;
                }

                ws.Cell(row, 1).Value = item.id;
                ws.Cell(row, 2).Value = item.catDesc;
                ws.Cell(row, 3).Value = item.famDesc;
                ws.Cell(row, 4).Value = item.descCompleta;
                ws.Cell(row, 5).Value = item.stock;
                row++;
            }

            wb.SaveAs(filePath);
        }
    }
}
