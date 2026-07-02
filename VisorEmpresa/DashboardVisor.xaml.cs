using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace VisorEmpresa
{
    /// <summary>
    /// Sección Dashboard del visor: totales de TODA la empresa (todas las
    /// sucursales, sin filtro por sucursal) en unidades. Render de gráficos
    /// adaptado del DashboardGeneral de la app principal; los datos salen de
    /// consultas agregadas (ConsultasEmpresa), no de las cachés por sucursal.
    /// El segmentador (Ventas / Compras) es de selección única y filtra los
    /// gráficos y las tarjetas; las cantidades de cada tipo se muestran siempre
    /// en sus chips.
    /// </summary>
    public partial class DashboardVisor : UserControl
    {
        private bool _iniciado;
        private bool _cargandoFiltros;   // evita recargas mientras se llenan los combos

        // Datos del año/empresa activos (se recargan con Actualizar o al cambiar filtros).
        private List<MovimientoFila> _filas = new();
        private Dictionary<string, (int Documentos, int Articulos)> _resumen = new();
        private double _traspasosUnidades;

        // Color asignado a cada categoría en el render actual.
        private readonly Dictionary<string, Brush> _colorCat = new();

        private double _alturaGrafico = 280;        // px de la zona de barras (meses); dinámico por resolución
        private const double ReservaEtiquetaY = 16; // px reservados arriba de la barra más alta para su etiqueta de valor

        // Cache del último cálculo del gráfico por mes (para re-render al cambiar el alto disponible).
        private double[]? _porMesCache;
        private Dictionary<int, Dictionary<string, double>>? _porMesCatCache;
        private double _niceMax;

        // Paleta de colores (se cicla si hay más series que colores). Misma que la app principal.
        private static readonly Color[] Paleta =
        {
            Color.FromRgb(0x4A,0x6F,0xE3), Color.FromRgb(0x2E,0x7D,0x32),
            Color.FromRgb(0xFB,0x8C,0x00), Color.FromRgb(0xE5,0x39,0x35),
            Color.FromRgb(0x8E,0x24,0xAA), Color.FromRgb(0x00,0xAC,0xC1),
            Color.FromRgb(0xFD,0xD8,0x35), Color.FromRgb(0x6D,0x4C,0x41),
            Color.FromRgb(0xEC,0x40,0x7A), Color.FromRgb(0x7C,0xB3,0x42),
            Color.FromRgb(0x5C,0x6B,0xC0), Color.FromRgb(0x26,0xA6,0x9A),
        };

        // Item de los combos del encabezado (Id oculto + texto visible).
        private class Opcion
        {
            public string Id    { get; }
            public string Texto { get; }
            public Opcion(string id, string texto) { Id = id; Texto = texto; }
            public override string ToString() => Texto;
        }

        public DashboardVisor()
        {
            InitializeComponent();
            Loaded += async (_, _) =>
            {
                if (_iniciado) return;
                _iniciado = true;
                await CargarFiltrosYDatosAsync();
            };
        }

        /// <summary>
        /// Re-render tras un cambio de tema (los brushes de los gráficos se
        /// resuelven al dibujar). La llama la consola al alternar claro/oscuro.
        /// </summary>
        public void RefrescarTema()
        {
            if (_iniciado) Recalcular();
        }

        // ─── Filtros del encabezado (empresa / año) ───────────────────────────
        private async Task CargarFiltrosYDatosAsync()
        {
            _cargandoFiltros = true;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var empresas = await Task.Run(ConsultasEmpresa.CargarEmpresas);
                var opciones = empresas.Select(e => new Opcion(e.Id, e.Descripcion)).ToList();
                CmbEmpresa.ItemsSource = opciones;

                // Preselección: la empresa del usuario logueado, o la primera.
                int idx = opciones.FindIndex(o => o.Id == VisorState.EmpresaActiva);
                CmbEmpresa.SelectedIndex = idx >= 0 ? idx : (opciones.Count > 0 ? 0 : -1);

                await RepoblarAniosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar las empresas:\n{ex.Message}",
                                "Visor Empresa", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                _cargandoFiltros = false;
            }

            await CargarDatosAsync();
        }

        private async Task RepoblarAniosAsync()
        {
            string emp       = EmpresaSeleccionada();
            int anioPrevio   = CmbAnio.SelectedItem is int a ? a : 0;

            var anios = await Task.Run(() => ConsultasEmpresa.CargarAnios(emp));
            CmbAnio.ItemsSource = anios;

            int idx = anios.IndexOf(anioPrevio);
            if (idx < 0) idx = anios.IndexOf(DateTime.Now.Year);
            if (idx < 0) idx = 0;
            CmbAnio.SelectedIndex = anios.Count > 0 ? idx : -1;
        }

        private string EmpresaSeleccionada() =>
            (CmbEmpresa.SelectedItem as Opcion)?.Id ?? "";

        private int AnioSeleccionado() =>
            CmbAnio.SelectedItem is int a ? a : DateTime.Now.Year;

        private async void CmbEmpresa_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoFiltros || !_iniciado) return;

            _cargandoFiltros = true;
            try     { await RepoblarAniosAsync(); }
            catch   { /* el error de datos se reporta en CargarDatosAsync */ }
            finally { _cargandoFiltros = false; }

            await CargarDatosAsync();
        }

        private async void CmbAnio_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoFiltros || !_iniciado) return;
            await CargarDatosAsync();
        }

        // ─── Carga de datos (consultas agregadas a nivel empresa) ─────────────
        private async Task CargarDatosAsync()
        {
            string emp  = EmpresaSeleccionada();
            int    anio = AnioSeleccionado();

            if (string.IsNullOrEmpty(emp))
            {
                _filas = new List<MovimientoFila>();
                _resumen = new Dictionary<string, (int, int)>();
                _traspasosUnidades = 0;
                Recalcular();
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                var filas    = await Task.Run(() => ConsultasEmpresa.CargarMovimientos(emp, anio));
                var resumen  = await Task.Run(() => ConsultasEmpresa.CargarResumenPedidos(emp, anio));
                var traspasos = await Task.Run(() => ConsultasEmpresa.CargarTraspasosInternos(emp, anio));

                _filas = filas;
                _resumen = resumen;
                _traspasosUnidades = traspasos.Unidades;
                VisorState.EmpresaActiva = emp;

                Recalcular();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar los datos:\n{ex.Message}",
                                "Visor Empresa", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ─── Eventos ──────────────────────────────────────────────────────────
        private async void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            await CargarDatosAsync();
        }

        private void Segmentador_Changed(object sender, RoutedEventArgs e)
        {
            // RadioButton dispara Checked al aplicar IsChecked por defecto antes de
            // que la ventana esté lista; protegemos con _iniciado.
            if (!_iniciado) return;
            Recalcular();
        }

        // ─── Cálculo principal (en memoria, sobre las filas agregadas) ────────
        private void Recalcular()
        {
            string seg = TglCompras.IsChecked == true ? "compra" : "venta";

            // Totales por tipo (siempre, independientes del segmentador).
            double totVenta = 0, totCompra = 0;
            foreach (var f in _filas)
            {
                if (f.Movimiento == "venta")  totVenta  += f.Cantidad;
                if (f.Movimiento == "compra") totCompra += f.Cantidad;
            }

            // Estructuras filtradas por el segmento activo.
            var porMes       = new double[13];                          // total del mes (1..12)
            var porMesCat    = new Dictionary<int, Dictionary<string, double>>();
            var porCategoria = new Dictionary<string, double>();
            var porSucursal  = new Dictionary<string, double>();        // clave: id de sucursal
            var nombreSuc    = new Dictionary<string, string>();
            double totalSeg  = 0;

            foreach (var f in _filas)
            {
                if (f.Movimiento != seg || f.Cantidad == 0) continue;
                totalSeg += f.Cantidad;

                if (f.Mes is >= 1 and <= 12)
                {
                    porMes[f.Mes] += f.Cantidad;
                    if (!porMesCat.TryGetValue(f.Mes, out var d)) { d = new(); porMesCat[f.Mes] = d; }
                    d[f.Categoria] = d.GetValueOrDefault(f.Categoria) + f.Cantidad;
                }

                porCategoria[f.Categoria] = porCategoria.GetValueOrDefault(f.Categoria) + f.Cantidad;

                porSucursal[f.SucursalId] = porSucursal.GetValueOrDefault(f.SucursalId) + f.Cantidad;
                nombreSuc[f.SucursalId]   = f.Sucursal;
            }

            // ── Tarjetas ─────────────────────────────────────────────────────
            LblVentasVal.Text   = Fmt(totVenta);
            LblComprasVal.Text  = Fmt(totCompra);
            LblUnidadesSeg.Text = Fmt(totalSeg);

            _resumen.TryGetValue(seg, out var res);
            LblDocumentos.Text    = Fmt(res.Documentos);
            LblArticulos.Text     = Fmt(res.Articulos);
            LblSucursalesMov.Text = Fmt(porSucursal.Count(kv => kv.Value != 0));
            LblTraspasos.Text     = Fmt(_traspasosUnidades);

            // ── Gráficos ─────────────────────────────────────────────────────
            AsignarColores(porCategoria);

            _porMesCache    = porMes;
            _porMesCatCache = porMesCat;

            RenderLeyenda(porCategoria);
            RenderMeses(porMes, porMesCat);
            RenderCategorias(porCategoria);
            RenderSucursales(porSucursal, nombreSuc, totalSeg);
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
            PanelEjeY.Children.Clear();
            PanelGrid.Children.Clear();

            // Escala = máximo valor (categoría, mes), ya que las barras son por categoría.
            double max = porMesCat.Values.SelectMany(d => d.Values).DefaultIfEmpty(0).Max();

            if (max <= 0)
            {
                LblMesesVacio.Visibility = Visibility.Visible;
                ZonaMeses.Visibility     = Visibility.Collapsed;
                return;
            }
            LblMesesVacio.Visibility = Visibility.Collapsed;
            ZonaMeses.Visibility     = Visibility.Visible;

            _niceMax = NiceCeil(max);
            PanelEjeY.Height = _alturaGrafico + ReservaEtiquetaY;
            RenderEjeY();

            // Orden estable de categorías (por total) para agrupar igual en cada mes.
            var ordenCats = _colorCat.Keys.ToList();

            for (int m = 1; m <= 12; m++)
            {
                double totalMes = porMes[m];
                if (totalMes <= 0) continue;   // solo meses con registros
                porMesCat.TryGetValue(m, out var catsMes);

                var columna = new StackPanel
                {
                    Margin = new Thickness(6, 0, 6, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                };

                // Zona de altura fija → barras alineadas al mismo eje base. Se reserva
                // espacio extra arriba (ReservaEtiquetaY) para que la etiqueta de valor
                // de la barra más alta no quede recortada por el ScrollViewer horizontal.
                var zona  = new Grid { Height = _alturaGrafico + ReservaEtiquetaY };
                var grupo = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom
                };

                if (catsMes != null)
                {
                    foreach (var cat in ordenCats)
                    {
                        double val = catsMes.GetValueOrDefault(cat);
                        if (val <= 0) continue;

                        // Columna [valor encima][barra], alineada al fondo: el valor
                        // queda pegado justo arriba de la barra y sube/baja con ella.
                        var barCol = new StackPanel
                        {
                            Margin = new Thickness(2, 0, 2, 0),
                            VerticalAlignment = VerticalAlignment.Bottom,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        barCol.Children.Add(new TextBlock
                        {
                            Text = Fmt(val),
                            FontSize = 9, FontWeight = FontWeights.SemiBold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = Tema("ThemeTexto", Color.FromRgb(0x20, 0x20, 0x20)),
                            Margin = new Thickness(0, 0, 0, 2)
                        });
                        barCol.Children.Add(new Border
                        {
                            Width = 20,
                            Height = Math.Max(2, val / _niceMax * _alturaGrafico),
                            Background = ColorDe(cat),
                            CornerRadius = new CornerRadius(3, 3, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Center
                        });
                        grupo.Children.Add(barCol);
                    }
                }
                zona.Children.Add(grupo);
                columna.Children.Add(zona);

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

            // Redibujar la cuadrícula con el ancho real una vez completado el layout.
            Dispatcher.BeginInvoke(new Action(DibujarCuadricula),
                                   System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // Eje Y: 5 marcas (0, ¼, ½, ¾, máx) alineadas con la zona de barras.
        private void RenderEjeY()
        {
            var brush = Tema("ThemeTextoSec", Color.FromRgb(0x7A, 0x7A, 0x7A));
            for (int i = 0; i <= 4; i++)
            {
                double frac = i / 4.0;
                double y    = _alturaGrafico + ReservaEtiquetaY - frac * _alturaGrafico;
                var lbl = new TextBlock
                {
                    Text = Fmt(_niceMax * frac),
                    FontSize = 10,
                    Foreground = brush,
                    Width = 42,
                    TextAlignment = TextAlignment.Right
                };
                Canvas.SetLeft(lbl, 0);
                Canvas.SetTop(lbl, y - 7);
                PanelEjeY.Children.Add(lbl);
            }
        }

        // Cuadrícula horizontal detrás de las barras (se redibuja al cambiar el ancho).
        private void PanelPlot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Al minimizar la ventana el alto llega a ~0; ignorarlo para no borrar el
            // gráfico (se re-renderiza al restaurar con un alto válido).
            if (e.NewSize.Height <= 0) return;

            // Alto disponible para las barras (reservando ~46 px para el nombre del
            // mes y su total debajo, más ReservaEtiquetaY para la etiqueta de valor
            // arriba de la barra más alta). Re-renderiza el gráfico por mes si cambió.
            double nueva = Math.Max(120, e.NewSize.Height - 46 - ReservaEtiquetaY);
            if (_porMesCache != null && Math.Abs(nueva - _alturaGrafico) > 1)
            {
                _alturaGrafico = nueva;
                RenderMeses(_porMesCache, _porMesCatCache!);
            }
            else
            {
                DibujarCuadricula();
            }
        }

        private void DibujarCuadricula()
        {
            PanelGrid.Children.Clear();
            if (_niceMax <= 0) return;
            double w = PanelMeses.ActualWidth > 0 ? PanelMeses.ActualWidth : PanelPlot.ActualWidth;
            if (w <= 0) return;

            var brush = Tema("ThemeBorde", Color.FromRgb(0xDD, 0xDD, 0xDD));
            for (int i = 0; i <= 4; i++)
            {
                double y = _alturaGrafico + ReservaEtiquetaY - (i / 4.0) * _alturaGrafico;
                var linea = new System.Windows.Shapes.Rectangle
                {
                    Width = w,
                    Height = 1,
                    Fill = brush,
                    Opacity = 0.6
                };
                Canvas.SetLeft(linea, 0);
                Canvas.SetTop(linea, y);
                PanelGrid.Children.Add(linea);
            }
        }

        // Redondea hacia arriba a un número "lindo" (1/2/2.5/5 × 10^k) para el eje.
        private static double NiceCeil(double v)
        {
            if (v <= 0) return 0;
            double exp  = Math.Floor(Math.Log10(v));
            double pot  = Math.Pow(10, exp);
            double frac = v / pot;
            double nice = frac <= 1 ? 1 : frac <= 2 ? 2 : frac <= 2.5 ? 2.5 : frac <= 5 ? 5 : 10;
            return nice * pot;
        }

        // ─── Barras horizontales por categoría (color propio por categoría) ──
        private void RenderCategorias(Dictionary<string, double> porCategoria)
        {
            var orden = porCategoria.Where(kv => kv.Value > 0)
                                    .OrderByDescending(kv => kv.Value)
                                    .ToList();
            if (orden.Count == 0)
            {
                LblCategoriasVacio.Visibility = Visibility.Visible;
                PanelCategorias.Children.Clear();
                return;
            }
            LblCategoriasVacio.Visibility = Visibility.Collapsed;

            var datos = orden.Select(kv => (kv.Key, kv.Value, ColorDe(kv.Key), Fmt(kv.Value))).ToList();
            RenderBarrasH(PanelCategorias, datos);
        }

        // ─── Barras horizontales por sucursal (cantidad + participación %) ───
        private void RenderSucursales(Dictionary<string, double> porSucursal,
                                      Dictionary<string, string> nombres, double total)
        {
            var orden = porSucursal.Where(kv => kv.Value > 0)
                                   .OrderByDescending(kv => kv.Value)
                                   .ToList();
            if (orden.Count == 0)
            {
                LblSucursalesVacio.Visibility = Visibility.Visible;
                PanelSucursales.Children.Clear();
                return;
            }
            LblSucursalesVacio.Visibility = Visibility.Collapsed;

            var datos = new List<(string label, double val, Brush color, string valorTexto)>();
            for (int i = 0; i < orden.Count; i++)
            {
                var (id, val) = (orden[i].Key, orden[i].Value);
                string pct = total > 0
                    ? (val / total).ToString("P1", CultureInfo.CurrentCulture)
                    : "0%";
                datos.Add((
                    nombres.GetValueOrDefault(id, "(sin nombre)"),
                    val,
                    new SolidColorBrush(Paleta[i % Paleta.Length]),
                    $"{Fmt(val)} · {pct}"));
            }
            RenderBarrasH(PanelSucursales, datos);
        }

        // ─── Helper: barras horizontales que LLENAN el ancho disponible ──────
        // Usa columnas en proporción (star): la barra más larga = 100% del ancho
        // de la zona de barras. El contenedor debe estar en un ScrollViewer con
        // scroll horizontal deshabilitado (para que el ancho sea finito).
        // Igual que en DashboardGeneral, con la etiqueta de valor parametrizada
        // (permite mostrar "cantidad · %" en la comparativa por sucursal).
        private const double LabelW = 130;

        private void RenderBarrasH(Panel destino,
                                   List<(string label, double val, Brush color, string valorTexto)> datos)
        {
            destino.Children.Clear();
            if (datos.Count == 0) return;

            double max = NiceCeil(datos.Max(d => d.val));
            if (max <= 0) return;

            var lineBrush  = Tema("ThemeBorde", Color.FromRgb(0xDD, 0xDD, 0xDD));
            var textBrush  = Tema("ThemeTexto", Color.FromRgb(0x20, 0x20, 0x20));
            var mutedBrush = Tema("ThemeTextoSec", Color.FromRgb(0x7A, 0x7A, 0x7A));

            // Contenedor: cuadrícula (detrás) + filas (delante)
            var cont = new Grid();

            // Cuadrícula vertical: 4 columnas iguales con línea a la derecha
            var gl = new Grid { Margin = new Thickness(LabelW, 0, 0, 0) };
            for (int i = 0; i < 4; i++)
            {
                gl.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var line = new Border
                {
                    BorderBrush = lineBrush,
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    Opacity = 0.6
                };
                Grid.SetColumn(line, i);
                gl.Children.Add(line);
            }
            cont.Children.Add(gl);

            // Filas (etiqueta + barra proporcional + valor)
            var filas = new StackPanel();
            foreach (var (label, val, color, valorTexto) in datos)
            {
                var fila = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelW) });
                fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = label, FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    Foreground = textBrush
                };
                Grid.SetColumn(lbl, 0);
                fila.Children.Add(lbl);

                // 3 columnas: col0=val Stars (barra), col1=Auto (etiqueta justo
                // después), col2=(max-val) Stars (espacio restante). La etiqueta
                // aparece pegada al borde derecho de su barra, no al del contenedor.
                var bz = new Grid();
                bz.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, val), GridUnitType.Star) });
                bz.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                bz.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.0001, max - val), GridUnitType.Star) });

                var bar = new Border
                {
                    Height = 24,
                    Background = color,
                    CornerRadius = new CornerRadius(4),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(bar, 0);
                bz.Children.Add(bar);

                var v = new TextBlock
                {
                    Text = valorTexto, FontSize = 11, FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    Foreground = textBrush
                };
                Grid.SetColumn(v, 1);
                bz.Children.Add(v);

                Grid.SetColumn(bz, 1);
                fila.Children.Add(bz);

                filas.Children.Add(fila);
            }
            cont.Children.Add(filas);
            destino.Children.Add(cont);

            // Eje X: 0 a la izquierda + marcas 25/50/75/100% del máximo
            var eje = new Grid { Margin = new Thickness(LabelW, 4, 0, 0) };
            for (int i = 0; i < 4; i++)
            {
                eje.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var t = new TextBlock
                {
                    Text = Fmt(max * (i + 1) / 4.0),
                    FontSize = 10, TextAlignment = TextAlignment.Right,
                    Foreground = mutedBrush
                };
                Grid.SetColumn(t, i);
                eje.Children.Add(t);
            }
            var cero = new TextBlock
            {
                Text = "0", FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = mutedBrush
            };
            Grid.SetColumn(cero, 0);
            eje.Children.Add(cero);
            destino.Children.Add(eje);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
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
    }
}
