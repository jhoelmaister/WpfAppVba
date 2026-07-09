using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ClosedXML.Excel;
using Microsoft.Win32;
using SistemaGestion;
using SistemaGestion.Data;

namespace VisorEmpresa
{
    /// <summary>
    /// Duplicado de SistemaGestion.InformeExcelArticulos para el visor: mismo
    /// informe (nombre + fecha/hora de corte), pero el stock se calcula con
    /// ConsultasEmpresa.ObtenerStockEmpresa (a nivel de EMPRESA) en vez de
    /// StockCalculator.ContarStock, que depende de AppState.SucursalActiva/
    /// AperturaActiva — nunca poblados en VisorEmpresa (habría dado stock 0
    /// siempre, sin ningún error visible).
    /// </summary>
    public partial class InformeExcelArticulos : Window
    {
        private static SqlData Sql => SqlData.Instance;

        public InformeExcelArticulos()
        {
            InitializeComponent();
            var ahora = DateTime.Now;
            TxtFecha.Text = ahora.ToString("dd/MM/yyyy");
            TxtHora.Text  = ahora.ToString("HH:mm:ss");
            ActualizarNombreArchivo();
        }

        // ─── Actualiza la preview del nombre de archivo ───────────────────────
        private void ActualizarNombreArchivo()
        {
            string nombre = TxtNombre.Text.Trim();
            string prefijoFecha = ObtenerPrefijoFecha();
            string nombreBase   = string.IsNullOrEmpty(nombre)
                ? $"{prefijoFecha} informe"
                : $"{prefijoFecha} informe {nombre}";

            TxtNombrePreview.Text = $"Nombre de archivo: {SanitizarNombre(nombreBase)}.xlsx";
        }

        private string ObtenerPrefijoFecha()
        {
            string fechaStr = TxtFecha.Text.Trim();
            string horaStr  = TxtHora.Text.Trim();

            if (DateTime.TryParseExact(fechaStr,
                    new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime fecha))
            {
                if (TimeSpan.TryParse(horaStr, out TimeSpan hora))
                {
                    var dt = fecha.Date + hora;
                    return dt.ToString("yyyyMMdd HHmmss");
                }
                return fecha.ToString("yyyyMMdd") + " 000000";
            }
            return DateTime.Now.ToString("yyyyMMdd HHmmss");
        }

        private static string SanitizarNombre(string nombre)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre;
        }

        // ─── Auto-numeración si el archivo ya existe ──────────────────────────
        private static string ResolverRutaUnica(string directorio, string nombreBase)
        {
            string ruta = Path.Combine(directorio, $"{nombreBase}.xlsx");
            if (!File.Exists(ruta)) return ruta;

            int n = 1;
            while (true)
            {
                ruta = Path.Combine(directorio, $"{nombreBase} ({n}).xlsx");
                if (!File.Exists(ruta)) return ruta;
                n++;
            }
        }

        // ─── Handlers de cambio ───────────────────────────────────────────────
        private void TxtNombre_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => ActualizarNombreArchivo();

        private void TxtFechaHora_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => ActualizarNombreArchivo();

        // ─── Botón Crear ──────────────────────────────────────────────────────
        private void BtnCrearInforme_Click(object sender, RoutedEventArgs e)
        {
            string nombre = TxtNombre.Text.Trim();

            // ── Validar fecha ─────────────────────────────────────────────
            if (!DateTime.TryParseExact(TxtFecha.Text.Trim(),
                    new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out DateTime fechaBase))
            {
                MessageBox.Show("Fecha inválida. Use el formato dd/mm/aaaa.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFecha.Focus();
                return;
            }

            // ── Combinar fecha + hora de corte ────────────────────────────
            DateTime fechaCorte = fechaBase.Date;
            if (TimeSpan.TryParse(TxtHora.Text.Trim(), out TimeSpan hora))
                fechaCorte = fechaBase.Date + hora;

            // ── Construir nombre base del archivo ─────────────────────────
            string prefijoFecha = ObtenerPrefijoFecha();
            string nombreBase   = string.IsNullOrEmpty(nombre)
                ? $"{prefijoFecha} informe"
                : $"{prefijoFecha} informe {nombre}";
            nombreBase = SanitizarNombre(nombreBase);

            // ── Explorador de guardado ────────────────────────────────────
            var dlg = new SaveFileDialog
            {
                Title            = "Guardar informe Excel",
                FileName         = nombreBase,
                DefaultExt       = ".xlsx",
                Filter           = "Excel (*.xlsx)|*.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dlg.ShowDialog(this) != true) return;

            // ── Auto-numeración si el archivo ya existe ───────────────────
            string directorio   = Path.GetDirectoryName(dlg.FileName) ?? "";
            string sinExt       = Path.GetFileNameWithoutExtension(dlg.FileName);
            string rutaFinal    = ResolverRutaUnica(directorio, sinExt);

            try
            {
                BtnCrearInforme.IsEnabled = false;
                BtnCrearInforme.Content   = "Generando…";

                GenerarExcel(rutaFinal, fechaCorte);

                Close();
                Process.Start(new ProcessStartInfo(rutaFinal) { UseShellExecute = true });
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
            ws.Cell(1, 1).Value = "Productos";
            ws.Cell(1, 2).Value = "Código";
            ws.Cell(1, 3).Value = "Categoría";
            ws.Cell(1, 4).Value = "Familia";
            ws.Cell(1, 5).Value = "Descripción Completa";
            ws.Cell(1, 6).Value = "Stock";

            // Stock de TODA la empresa (todas las sucursales) a la fecha de corte.
            var stockEmpresa = ConsultasEmpresa.ObtenerStockEmpresa(AppState.EmpresaActiva, fechaCorte).Totales;

            // ── Recolectar datos ──────────────────────────────────────────
            int uf = Sql.ArticulosObj.ContarFilas;
            var datos = new List<(string id, string codigo, string prodDesc, string catDesc, string famDesc, string descCompleta, double stock)>();

            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string codigo = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc   = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
                string famId  = Sql.ArticulosObj.ObtenerItem("familia",     id)?.ToString() ?? "";
                string catId  = Sql.ArticulosObj.ObtenerItem("Categoria",   id)?.ToString() ?? "";

                string famDesc  = Sql.FamiliasObj.ObtenerItem("descripcion",   famId)?.ToString() ?? "";
                string prodId   = Sql.FamiliasObj.ObtenerItem("producto",      famId)?.ToString() ?? "";
                string prodDesc = Sql.ProductosObj.ObtenerItem("descripcion",  prodId)?.ToString() ?? "";
                string catDesc  = Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "";

                string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);
                stockEmpresa.TryGetValue(id, out var totales);

                datos.Add((id, codigo, prodDesc, catDesc, famDesc, descCompleta, totales.Stock));
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

            // ── Escribir datos (una fila por artículo) ────────────────────
            int row = 2;
            foreach (var item in datos)
            {
                ws.Cell(row, 1).Value = item.prodDesc;
                ws.Cell(row, 2).Value = item.codigo;
                ws.Cell(row, 3).Value = item.catDesc;
                ws.Cell(row, 4).Value = item.famDesc;
                ws.Cell(row, 5).Value = item.descCompleta;
                ws.Cell(row, 6).Value = item.stock;
                row++;
            }

            wb.SaveAs(filePath);
        }
    }
}
