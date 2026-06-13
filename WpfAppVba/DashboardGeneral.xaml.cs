using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    /// <summary>
    /// Panel Dashboard: totales en CANTIDAD (unidades) de la sucursal y período
    /// activos. Lee del caché ya filtrado (PedidosObj / TraspasosObj). Los
    /// segmentadores (Ventas / Compras / Entradas / Salidas) filtran los gráficos,
    /// la tabla y la tarjeta de total; las cantidades de cada tipo se muestran
    /// siempre (sin importar los segmentadores activos).
    /// </summary>
    public partial class DashboardGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private bool _iniciado;

        private readonly ObservableCollection<FamiliaCantFila> _familias = new();

        // Cache artículo → (categoría desc, familia desc) para no repetir lookups.
        private readonly Dictionary<string, (string cat, string fam)> _articuloMeta = new();

        private const double AlturaGrafico = 180;   // px de la zona de barras (meses)
        private const double AnchoBarraMax = 280;   // px de la barra más larga (categorías)

        public DashboardGeneral()
        {
            InitializeComponent();
            GridFamilias.ItemsSource = _familias;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; Recalcular(); };
        }

        // ─── Eventos ──────────────────────────────────────────────────────────
        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            AppState.ActualizarProductos();
            _articuloMeta.Clear();
            Recalcular();
        }

        private void Segmentador_Changed(object sender, RoutedEventArgs e)
        {
            // Los toggles pueden dispararse antes de InitializeComponent al aplicar
            // IsChecked por defecto; protegemos con _iniciado.
            if (!_iniciado) return;
            Recalcular();
        }

        // ─── Cálculo principal ────────────────────────────────────────────────
        private void Recalcular()
        {
            bool incVenta   = TglVentas.IsChecked == true;
            bool incCompra  = TglCompras.IsChecked == true;
            bool incEntrada = TglEntradas.IsChecked == true;
            bool incSalida  = TglSalidas.IsChecked == true;

            string suc = AppState.SucursalActiva;

            // Totales por tipo (siempre, independientes de los segmentadores).
            double totVenta = 0, totCompra = 0, totEntrada = 0, totSalida = 0;

            // Estructuras filtradas por segmentadores activos.
            var porMes      = new double[13];                 // 1..12
            var porCategoria= new Dictionary<string, double>();
            var porFamilia  = new Dictionary<string, double>();
            var articulosSet= new HashSet<string>();
            var documentos  = new HashSet<string>();
            double totalActivos = 0;

            void Acumular(bool activo, double cant, DateTime? fecha,
                          string artId, string docId)
            {
                if (!activo || cant == 0) return;
                totalActivos += cant;
                if (fecha.HasValue && fecha.Value.Month is >= 1 and <= 12)
                    porMes[fecha.Value.Month] += cant;

                var (cat, fam) = MetaArticulo(artId);
                porCategoria[cat] = porCategoria.GetValueOrDefault(cat) + cant;
                porFamilia[fam]   = porFamilia.GetValueOrDefault(fam) + cant;

                if (!string.IsNullOrEmpty(artId)) articulosSet.Add(artId);
                if (!string.IsNullOrEmpty(docId)) documentos.Add(docId);
            }

            // ── Pedidos (ventas / compras) ───────────────────────────────────
            int ufP = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= ufP; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string docP = Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() ?? "";
                if (docP == "") continue;
                string mov  = (Sql.DocumentosPObj.ObtenerItem("movimiento", docP)?.ToString() ?? "")
                              .Trim().ToLowerInvariant();
                double cant = ConvertirCantidad(Sql.PedidosObj.ObtenerItem("cantidad", id));
                string art  = Sql.PedidosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                DateTime? fecha = ConvertirFecha(Sql.DocumentosPObj.ObtenerItem("fecha", docP));

                if (mov == "venta")
                {
                    totVenta += cant;
                    Acumular(incVenta, cant, fecha, art, docP);
                }
                else if (mov == "compra")
                {
                    totCompra += cant;
                    Acumular(incCompra, cant, fecha, art, docP);
                }
            }

            // ── Traspasos (entradas / salidas) ───────────────────────────────
            int ufT = Sql.TraspasosObj.ContarFilas;
            for (int i = 1; i <= ufT; i++)
            {
                var idObj = Sql.TraspasosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string docT = Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() ?? "";
                if (docT == "") continue;
                string origen  = Sql.DocumentosTObj.ObtenerItem("origen",  docT)?.ToString() ?? "";
                string destino = Sql.DocumentosTObj.ObtenerItem("destino", docT)?.ToString() ?? "";
                double cant = ConvertirCantidad(Sql.TraspasosObj.ObtenerItem("cantidad", id));
                string art  = Sql.TraspasosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                DateTime? fecha = ConvertirFecha(Sql.DocumentosTObj.ObtenerItem("fecha", docT));

                // Salida: la sucursal activa es el origen. Entrada: es el destino.
                if (origen == suc)
                {
                    totSalida += cant;
                    Acumular(incSalida, cant, fecha, art, docT);
                }
                else if (destino == suc)
                {
                    totEntrada += cant;
                    Acumular(incEntrada, cant, fecha, art, docT);
                }
            }

            // ── Actualizar UI ────────────────────────────────────────────────
            LblVentasVal.Text   = Fmt(totVenta);
            LblComprasVal.Text  = Fmt(totCompra);
            LblEntradasVal.Text = Fmt(totEntrada);
            LblSalidasVal.Text  = Fmt(totSalida);

            LblTotalGeneral.Text = Fmt(totalActivos);
            LblMovimientos.Text  = Fmt(documentos.Count);
            LblArticulos.Text    = Fmt(articulosSet.Count);

            RenderMeses(porMes);
            RenderCategorias(porCategoria);
            RenderFamilias(porFamilia);
        }

        // ─── Render: gráfico de barras verticales por mes ────────────────────
        private void RenderMeses(double[] porMes)
        {
            PanelMeses.Children.Clear();
            double max = porMes.Max();

            if (max <= 0)
            {
                LblMesesVacio.Visibility = Visibility.Visible;
                PanelMeses.Visibility    = Visibility.Collapsed;
                return;
            }
            LblMesesVacio.Visibility = Visibility.Collapsed;
            PanelMeses.Visibility    = Visibility.Visible;

            var barBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x6F, 0xE3));
            for (int m = 1; m <= 12; m++)
            {
                double val = porMes[m];
                double h   = max > 0 ? Math.Max(val > 0 ? 3 : 0, val / max * AlturaGrafico) : 0;

                var columna = new StackPanel
                {
                    Width = 50,
                    Margin = new Thickness(3, 0, 3, 0),
                    VerticalAlignment = VerticalAlignment.Bottom
                };

                columna.Children.Add(new TextBlock
                {
                    Text = val > 0 ? Fmt(val) : "",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Tema("ThemeTexto", Color.FromRgb(0x20,0x20,0x20)),
                    Margin = new Thickness(0, 0, 0, 3)
                });

                var zona = new Grid { Height = AlturaGrafico };
                zona.Children.Add(new Border
                {
                    Width = 28,
                    Height = h,
                    Background = barBrush,
                    CornerRadius = new CornerRadius(4, 4, 0, 0),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                columna.Children.Add(zona);

                columna.Children.Add(new TextBlock
                {
                    Text = NombreMes(m),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Tema("ThemeTextoSec", Color.FromRgb(0x7A,0x7A,0x7A)),
                    Margin = new Thickness(0, 4, 0, 0)
                });

                PanelMeses.Children.Add(columna);
            }
        }

        // ─── Render: barras horizontales por categoría ───────────────────────
        private void RenderCategorias(Dictionary<string, double> porCategoria)
        {
            PanelCategorias.Children.Clear();
            var orden = porCategoria.Where(kv => kv.Value > 0)
                                    .OrderByDescending(kv => kv.Value)
                                    .ToList();
            if (orden.Count == 0)
            {
                LblCategoriasVacio.Visibility = Visibility.Visible;
                return;
            }
            LblCategoriasVacio.Visibility = Visibility.Collapsed;

            double max = orden[0].Value;
            var barBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));

            foreach (var (cat, val) in orden)
            {
                var fila = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lbl = new TextBlock
                {
                    Text = cat,
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Tema("ThemeTexto", Color.FromRgb(0x20,0x20,0x20))
                };
                Grid.SetColumn(lbl, 0);
                fila.Children.Add(lbl);

                var barra = new Border
                {
                    Height = 18,
                    Width = Math.Max(4, val / max * AnchoBarraMax),
                    Background = barBrush,
                    CornerRadius = new CornerRadius(4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(barra, 1);
                fila.Children.Add(barra);

                var valTxt = new TextBlock
                {
                    Text = Fmt(val),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Tema("ThemeTexto", Color.FromRgb(0x20,0x20,0x20))
                };
                Grid.SetColumn(valTxt, 2);
                fila.Children.Add(valTxt);

                PanelCategorias.Children.Add(fila);
            }
        }

        // ─── Render: tabla por familia ────────────────────────────────────────
        private void RenderFamilias(Dictionary<string, double> porFamilia)
        {
            _familias.Clear();
            double total = porFamilia.Values.Sum();
            foreach (var (fam, val) in porFamilia.Where(kv => kv.Value > 0)
                                                 .OrderByDescending(kv => kv.Value))
            {
                _familias.Add(new FamiliaCantFila
                {
                    Familia        = fam,
                    Cantidad       = val,
                    PorcentajeTexto = total > 0 ? (val / total).ToString("P1", CultureInfo.CurrentCulture) : "0%"
                });
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private (string cat, string fam) MetaArticulo(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return ("Sin categoría", "Sin familia");
            if (_articuloMeta.TryGetValue(artId, out var meta)) return meta;

            string catId = Sql.ArticulosObj.ObtenerItem("categoria", artId)?.ToString() ?? "";
            string famId = Sql.ArticulosObj.ObtenerItem("familia",   artId)?.ToString() ?? "";
            string cat = string.IsNullOrEmpty(catId)
                ? "Sin categoría"
                : Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "Sin categoría";
            string fam = string.IsNullOrEmpty(famId)
                ? "Sin familia"
                : Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "Sin familia";

            meta = (cat, fam);
            _articuloMeta[artId] = meta;
            return meta;
        }

        private static double ConvertirCantidad(object? v)
        {
            if (v is null or DBNull) return 0;
            return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out double d)
                || double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d)
                ? d : 0;
        }

        private static DateTime? ConvertirFecha(object? v)
        {
            if (v is null or DBNull) return null;
            if (v is DateTime dt) return dt;
            return DateTime.TryParse(v.ToString(), out DateTime f) ? f : (DateTime?)null;
        }

        private static string Fmt(double v) => v.ToString("N0", CultureInfo.CurrentCulture);

        // Brush del tema con fallback (evita excepción si la clave no resuelve).
        private Brush Tema(string clave, Color fallback) =>
            TryFindResource(clave) as Brush ?? new SolidColorBrush(fallback);

        private static string NombreMes(int m)
        {
            string[] meses = { "", "Ene", "Feb", "Mar", "Abr", "May", "Jun",
                               "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
            return (m >= 1 && m <= 12) ? meses[m] : "";
        }

        // ─── Fila de la tabla por familia ─────────────────────────────────────
        public class FamiliaCantFila
        {
            public string Familia         { get; set; } = "";
            public double Cantidad        { get; set; }
            public string CantidadTexto   => Cantidad.ToString("N0", CultureInfo.CurrentCulture);
            public string PorcentajeTexto { get; set; } = "";
        }
    }
}
