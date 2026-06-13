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
    /// activos. Lee del caché ya filtrado (PedidosObj / TraspasosObj). El
    /// segmentador (Ventas / Compras / Entradas / Salidas) es de selección única
    /// y filtra los gráficos, la tabla y la tarjeta de total; las cantidades de
    /// cada tipo se muestran siempre en sus chips.
    /// El gráfico por mes es apilado por categoría (cada categoría con su color).
    /// </summary>
    public partial class DashboardGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private bool _iniciado;

        private readonly ObservableCollection<FamiliaCantFila> _familias  = new();
        private readonly ObservableCollection<ArticuloCantFila> _articulos = new();

        // Cache artículo → (categoría desc, familia desc, descripción, código) para no repetir lookups.
        private readonly Dictionary<string, (string cat, string fam, string desc, string cod)> _articuloMeta = new();

        // Color asignado a cada categoría en el render actual.
        private readonly Dictionary<string, Brush> _colorCat = new();

        private const double AlturaGrafico = 200;   // px de la zona de barras (meses)
        private const double AnchoBarraMax = 280;   // px de la barra más larga (categorías)

        // Paleta de colores para categorías (se cicla si hay más categorías que colores).
        private static readonly Color[] Paleta =
        {
            Color.FromRgb(0x4A,0x6F,0xE3), Color.FromRgb(0x2E,0x7D,0x32),
            Color.FromRgb(0xFB,0x8C,0x00), Color.FromRgb(0xE5,0x39,0x35),
            Color.FromRgb(0x8E,0x24,0xAA), Color.FromRgb(0x00,0xAC,0xC1),
            Color.FromRgb(0xFD,0xD8,0x35), Color.FromRgb(0x6D,0x4C,0x41),
            Color.FromRgb(0xEC,0x40,0x7A), Color.FromRgb(0x7C,0xB3,0x42),
            Color.FromRgb(0x5C,0x6B,0xC0), Color.FromRgb(0x26,0xA6,0x9A),
        };

        public DashboardGeneral()
        {
            InitializeComponent();
            GridFamilias.ItemsSource  = _familias;
            GridArticulos.ItemsSource = _articulos;
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
            // RadioButton dispara Checked al aplicar IsChecked por defecto antes de
            // que el panel esté listo; protegemos con _iniciado.
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

            // Totales por tipo (siempre, independientes del segmentador).
            double totVenta = 0, totCompra = 0, totEntrada = 0, totSalida = 0;

            // Estructuras filtradas por el segmentador activo.
            var porMes       = new double[13];                          // total del mes (1..12)
            var porMesCat    = new Dictionary<int, Dictionary<string, double>>();
            var porCategoria = new Dictionary<string, double>();
            var porFamilia   = new Dictionary<string, double>();
            var porArticulo  = new Dictionary<string, double>();
            var articulosSet = new HashSet<string>();
            var documentos   = new HashSet<string>();
            double totalActivos = 0;

            void Acumular(bool activo, double cant, DateTime? fecha, string artId, string docId)
            {
                if (!activo || cant == 0) return;
                totalActivos += cant;

                var (cat, fam, _, _) = MetaArticulo(artId);

                if (fecha.HasValue && fecha.Value.Month is >= 1 and <= 12)
                {
                    int mm = fecha.Value.Month;
                    porMes[mm] += cant;
                    if (!porMesCat.TryGetValue(mm, out var d)) { d = new(); porMesCat[mm] = d; }
                    d[cat] = d.GetValueOrDefault(cat) + cant;
                }

                porCategoria[cat] = porCategoria.GetValueOrDefault(cat) + cant;
                porFamilia[fam]   = porFamilia.GetValueOrDefault(fam) + cant;

                if (!string.IsNullOrEmpty(artId))
                {
                    articulosSet.Add(artId);
                    porArticulo[artId] = porArticulo.GetValueOrDefault(artId) + cant;
                }
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

            // ── Asignar colores por categoría (orden estable por cantidad) ────
            AsignarColores(porCategoria);

            // ── Actualizar UI ────────────────────────────────────────────────
            LblVentasVal.Text   = Fmt(totVenta);
            LblComprasVal.Text  = Fmt(totCompra);
            LblEntradasVal.Text = Fmt(totEntrada);
            LblSalidasVal.Text  = Fmt(totSalida);

            LblTotalGeneral.Text = Fmt(totalActivos);
            LblMovimientos.Text  = Fmt(documentos.Count);
            LblArticulos.Text    = Fmt(articulosSet.Count);

            RenderLeyenda(porCategoria);
            RenderMeses(porMes, porMesCat);
            RenderCategorias(porCategoria);
            RenderFamilias(porFamilia);
            RenderArticulos(porArticulo, totalActivos);
        }

        // ─── Colores por categoría ────────────────────────────────────────────
        private void AsignarColores(Dictionary<string, double> porCategoria)
        {
            _colorCat.Clear();
            var cats = porCategoria.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
            for (int i = 0; i < cats.Count; i++)
                _colorCat[cats[i]] = new SolidColorBrush(Paleta[i % Paleta.Length]);
        }

        private Brush ColorDe(string cat) =>
            _colorCat.TryGetValue(cat, out var b) ? b : new SolidColorBrush(Colors.Gray);

        // ─── Leyenda de categorías ────────────────────────────────────────────
        private void RenderLeyenda(Dictionary<string, double> porCategoria)
        {
            PanelLeyenda.Children.Clear();
            foreach (var cat in porCategoria.Where(kv => kv.Value > 0)
                                            .OrderByDescending(kv => kv.Value)
                                            .Select(kv => kv.Key))
            {
                var chip = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 14, 4),
                    VerticalAlignment = VerticalAlignment.Center
                };
                chip.Children.Add(new Border
                {
                    Width = 12, Height = 12, CornerRadius = new CornerRadius(3),
                    Background = ColorDe(cat),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                });
                chip.Children.Add(new TextBlock
                {
                    Text = cat, FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Tema("ThemeTexto", Color.FromRgb(0x20, 0x20, 0x20))
                });
                PanelLeyenda.Children.Add(chip);
            }
        }

        // ─── Gráfico de barras agrupadas por mes (una barra por categoría) ──
        private void RenderMeses(double[] porMes, Dictionary<int, Dictionary<string, double>> porMesCat)
        {
            PanelMeses.Children.Clear();

            // Escala = máximo valor (categoría, mes), ya que las barras son por categoría.
            double max = porMesCat.Values.SelectMany(d => d.Values).DefaultIfEmpty(0).Max();

            if (max <= 0)
            {
                LblMesesVacio.Visibility = Visibility.Visible;
                PanelMeses.Visibility    = Visibility.Collapsed;
                return;
            }
            LblMesesVacio.Visibility = Visibility.Collapsed;
            PanelMeses.Visibility    = Visibility.Visible;

            // Orden estable de categorías (por total) para agrupar igual en cada mes.
            var ordenCats = _colorCat.Keys.ToList();

            for (int m = 1; m <= 12; m++)
            {
                double totalMes = porMes[m];
                porMesCat.TryGetValue(m, out var catsMes);

                var columna = new StackPanel
                {
                    Margin = new Thickness(6, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Bottom
                };

                // Grupo de barras (una por categoría con valor > 0 en el mes)
                var grupo = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                if (catsMes != null)
                {
                    foreach (var cat in ordenCats)
                    {
                        double val = catsMes.GetValueOrDefault(cat);
                        if (val <= 0) continue;

                        var barCol = new StackPanel
                        {
                            Margin = new Thickness(2, 0, 2, 0),
                            VerticalAlignment = VerticalAlignment.Bottom
                        };
                        // Valor (cantidad por categoría y mes) ARRIBA de la barra
                        barCol.Children.Add(new TextBlock
                        {
                            Text = Fmt(val),
                            FontSize = 9, FontWeight = FontWeights.SemiBold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = Tema("ThemeTexto", Color.FromRgb(0x20, 0x20, 0x20)),
                            Margin = new Thickness(0, 0, 0, 2)
                        });
                        // Zona de altura fija → barras alineadas al mismo eje base
                        var zona = new Grid { Height = AlturaGrafico };
                        zona.Children.Add(new Border
                        {
                            Width = 18,
                            Height = Math.Max(2, val / max * AlturaGrafico),
                            Background = ColorDe(cat),
                            CornerRadius = new CornerRadius(3, 3, 0, 0),
                            VerticalAlignment = VerticalAlignment.Bottom
                        });
                        barCol.Children.Add(zona);
                        grupo.Children.Add(barCol);
                    }
                }
                columna.Children.Add(grupo);

                // Título del mes
                columna.Children.Add(new TextBlock
                {
                    Text = NombreMes(m),
                    FontSize = 11, FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Tema("ThemeTexto", Color.FromRgb(0x20, 0x20, 0x20)),
                    Margin = new Thickness(0, 4, 0, 0)
                });

                // Total del mes, DEBAJO del título del mes
                columna.Children.Add(new TextBlock
                {
                    Text = totalMes > 0 ? Fmt(totalMes) : "",
                    FontSize = 11, FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Tema("ThemeTextoSec", Color.FromRgb(0x7A, 0x7A, 0x7A))
                });

                PanelMeses.Children.Add(columna);
            }
        }

        // ─── Barras horizontales por categoría (color propio) ────────────────
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
                    Foreground = Tema("ThemeTexto", Color.FromRgb(0x20, 0x20, 0x20))
                };
                Grid.SetColumn(lbl, 0);
                fila.Children.Add(lbl);

                var barra = new Border
                {
                    Height = 18,
                    Width = Math.Max(4, val / max * AnchoBarraMax),
                    Background = ColorDe(cat),
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
                    Foreground = Tema("ThemeTexto", Color.FromRgb(0x20, 0x20, 0x20))
                };
                Grid.SetColumn(valTxt, 2);
                fila.Children.Add(valTxt);

                PanelCategorias.Children.Add(fila);
            }
        }

        // ─── Tabla por familia ────────────────────────────────────────────────
        private void RenderFamilias(Dictionary<string, double> porFamilia)
        {
            _familias.Clear();
            double total = porFamilia.Values.Sum();
            foreach (var (fam, val) in porFamilia.Where(kv => kv.Value > 0)
                                                 .OrderByDescending(kv => kv.Value))
            {
                _familias.Add(new FamiliaCantFila
                {
                    Familia         = fam,
                    Cantidad        = val,
                    PorcentajeTexto = total > 0 ? (val / total).ToString("P1", CultureInfo.CurrentCulture) : "0%"
                });
            }
        }

        // ─── Lista de artículos (Familia · Descripción · Cantidad) ───────────
        private void RenderArticulos(Dictionary<string, double> porArticulo, double total)
        {
            _articulos.Clear();
            foreach (var (artId, val) in porArticulo.Where(kv => kv.Value > 0)
                                                    .OrderByDescending(kv => kv.Value))
            {
                var (_, fam, desc, cod) = MetaArticulo(artId);
                _articulos.Add(new ArticuloCantFila
                {
                    Codigo          = cod,
                    Familia         = fam,
                    Descripcion     = desc,
                    Cantidad        = val,
                    PorcentajeTexto = total > 0 ? (val / total).ToString("P1", CultureInfo.CurrentCulture) : "0%"
                });
            }
            LblArticulosTotal.Text = Fmt(total);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private (string cat, string fam, string desc, string cod) MetaArticulo(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return ("Sin categoría", "Sin familia", "(sin artículo)", "");
            if (_articuloMeta.TryGetValue(artId, out var meta)) return meta;

            string catId = Sql.ArticulosObj.ObtenerItem("categoria", artId)?.ToString() ?? "";
            string famId = Sql.ArticulosObj.ObtenerItem("familia",   artId)?.ToString() ?? "";
            string cat = string.IsNullOrEmpty(catId)
                ? "Sin categoría"
                : Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "Sin categoría";
            string fam = string.IsNullOrEmpty(famId)
                ? "Sin familia"
                : Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "Sin familia";
            string desc = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
            string cod  = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? "";

            meta = (cat, fam, desc, cod);
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

        // ─── Filas de tablas ──────────────────────────────────────────────────
        public class FamiliaCantFila
        {
            public string Familia         { get; set; } = "";
            public double Cantidad        { get; set; }
            public string CantidadTexto   => Cantidad.ToString("N0", CultureInfo.CurrentCulture);
            public string PorcentajeTexto { get; set; } = "";
        }

        public class ArticuloCantFila
        {
            public string Codigo          { get; set; } = "";
            public string Familia         { get; set; } = "";
            public string Descripcion     { get; set; } = "";
            public double Cantidad        { get; set; }
            public string CantidadTexto   => Cantidad.ToString("N0", CultureInfo.CurrentCulture);
            public string PorcentajeTexto { get; set; } = "";
        }
    }
}
