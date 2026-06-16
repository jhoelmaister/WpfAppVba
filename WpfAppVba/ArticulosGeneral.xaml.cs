using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ArticulosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        public event Action? Cerrando;
        public void IntentarCerrar() => Cerrando?.Invoke();

        // Modo exportar: cuando se abre desde Traspasos/Inventarios/Pedidos (multi)
        private readonly Action<List<ArticuloExportado>>? _callbackExportar;
        // Modo single: buscar un solo artículo con doble clic (sin checkbox ni #)
        private readonly Action<ArticuloExportado>? _callbackSingle;
        // List en lugar de HashSet para conservar el orden de selección
        private readonly List<string> _seleccionados = new();

        // Modo de filtro activo: "todos" (carga inicial) | "busqueda" (TxtBuscar) | "familia" (Tree1)
        private string _modoFiltro = "todos";

        public bool ModoExportar => _callbackExportar != null;
        public bool ModoSingle   => _callbackSingle   != null;

        private bool _iniciado = false;

        /// <summary>Constructor sin parámetros requerido por el compilador XAML.</summary>
        public ArticulosGeneral() : this(null, null) { }

        public ArticulosGeneral(Action<List<ArticuloExportado>>? callbackExportar = null,
                                 Action<ArticuloExportado>?       callbackSingle   = null)
        {
            InitializeComponent();
            _callbackExportar = callbackExportar;
            _callbackSingle   = callbackSingle;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarArbol(); CargarArticulos(); ConfigurarModo(); };
        }

        /// <summary>Abre ArticulosGeneral como pestaña dentro de ConsolaMovimientos.</summary>
        public static void OpenAsTab(Window owner,
                                     Action<List<ArticuloExportado>>? callbackExportar = null,
                                     Action<ArticuloExportado>?        callbackSingle   = null,
                                     string                            contexto         = "",
                                     UIElement?                        llamador         = null)
        {
            var consola = owner as ConsolaMovimientos;
            if (consola == null) return;

            bool esMulti = callbackExportar != null;
            string titulo = esMulti
                ? (string.IsNullOrEmpty(contexto) ? "Importar Artículos" : $"Importar Artículos ({contexto})")
                : (string.IsNullOrEmpty(contexto) ? "Buscar Artículo"    : $"Buscar Artículo ({contexto})");
            string clave = esMulti
                ? $"importar-articulos|{contexto}"
                : $"buscar-articulo|{contexto}";

            var ctrl = new ArticulosGeneral(callbackExportar, callbackSingle);
            ctrl.Cerrando += () => { consola.CerrarPestaña(ctrl); consola.SeleccionarPestaña(llamador); };

            consola.CerrarPestañaPorClave(clave);
            consola.AbrirPestaña(titulo, ctrl, clave);
        }

        /// <summary>Abre ArticulosGeneral como diálogo modal dentro de una ventana temporal.
        /// Usado por formularios que aún se muestran como ventana (Traspasos, Correcciones, Inventarios).</summary>
        public static void OpenAsDialog(Window owner,
                                        Action<List<ArticuloExportado>>? callbackExportar = null,
                                        Action<ArticuloExportado>?        callbackSingle   = null)
        {
            var ctrl = new ArticulosGeneral(callbackExportar, callbackSingle);
            var win  = new Window
            {
                Content               = ctrl,
                Title                 = "Artículos",
                Width                 = 900,
                Height                = 560,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = owner,
                ShowInTaskbar         = false,
                Background            = System.Windows.Media.Brushes.WhiteSmoke,
                ResizeMode            = ResizeMode.CanResize
            };
            WindowHelper.AjustarAlEcran(win);
            win.ShowDialog();
        }

        // ─── Configurar modo exportar ─────────────────────────────────────────
        private void ConfigurarModo()
        {
            bool esDialog = ModoExportar || ModoSingle;

            PanelExportar.Visibility   = ModoExportar ? Visibility.Visible   : Visibility.Collapsed;
            BtnInformeExcel.Visibility = esDialog     ? Visibility.Collapsed : Visibility.Visible;
            BtnNuevo.Visibility        = esDialog     ? Visibility.Collapsed : Visibility.Visible;
            BtnInsertar.Visibility     = esDialog     ? Visibility.Collapsed : Visibility.Visible;
            BtnEditar.Visibility       = esDialog     ? Visibility.Collapsed : Visibility.Visible;
            BtnEliminar.Visibility     = (esDialog || !AppState.EsAdmin) ? Visibility.Collapsed : Visibility.Visible;

            // Columna checkbox (✓) y columna "#" solo visibles en modo importar
            var visibilidad = ModoExportar ? Visibility.Visible : Visibility.Collapsed;
            Grid1.Columns[0].Visibility = visibilidad;   // ✓ checkbox
            Grid1.Columns[1].Visibility = visibilidad;   // #  orden selección
        }

        // ─── Árbol de productos/familias ──────────────────────────────────────
        private void CargarArbol()
        {
            Tree1.Items.Clear();

            // Nodo raíz "Todos"
            var nodoTodos = new TreeViewItem { Header = "Todos", Tag = "todos" };

            // Productos
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

                // Familias de este producto
                int ufFam = Sql.FamiliasObj.ContarFilas;
                for (int j = 1; j <= ufFam; j++)
                {
                    var famIdObj = Sql.FamiliasObj.Mover(j);
                    if (famIdObj == null) continue;
                    string famId = famIdObj.ToString()!;
                    string famProd = Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "";
                    if (famProd != prodId) continue;

                    string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? famId;
                    var nodoFam = new TreeViewItem
                    {
                        Header = famDesc,
                        Tag    = $"familia:{famId}"
                    };
                    nodoProd.Items.Add(nodoFam);
                }

                nodoProd.IsExpanded = true;
                nodoTodos.Items.Add(nodoProd);
            }

            // Nodo "Sin Clasificar"
            var nodoSin = new TreeViewItem { Header = "Sin Clasificar", Tag = "sinclasificar" };
            nodoTodos.Items.Add(nodoSin);

            nodoTodos.IsExpanded = true;
            Tree1.Items.Add(nodoTodos);

            // Seleccionar primer nodo
            nodoTodos.IsSelected = true;
        }

        // ─── Carga la lista de artículos ──────────────────────────────────────
        public void CargarArticulos()
        {
            var lista = new List<ArticuloFila>();
            int linea = 1;
            double totalDisp = 0, totalStock = 0;

            // Filtros excluyentes: solo se aplica el del modo activo
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
                    if (tagFiltro == "sinclasificar")
                    {
                        string famDescCheck = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(famId) && !string.IsNullOrEmpty(famDescCheck)) continue;
                    }
                    else if (tagFiltro.StartsWith("familia:"))
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
                    // "todos" o vacío → sin filtro
                }

                string codigo    = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc      = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo    = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
                string famDesc   = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
                string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

                // Filtro de búsqueda
                if (!string.IsNullOrEmpty(busqueda))
                {
                    if (!codigo.ToLower().Contains(busqueda) &&
                        !descCompleta.ToLower().Contains(busqueda))
                        continue;
                }

                double stock  = StockCalculator.ContarStock(id,  DateTime.Now);
                double stock2 = StockCalculator.ContarStock2(id, DateTime.Now);

                int ordenIdx = _seleccionados.IndexOf(id);   // -1 si no está

                lista.Add(new ArticuloFila
                {
                    Linea          = linea++,
                    Id             = id,
                    Codigo         = codigo,
                    Descripcion    = descCompleta,
                    Disponible     = stock2,
                    Stock          = stock,
                    Seleccionado   = ordenIdx >= 0,
                    OrdenSeleccion = ordenIdx >= 0 ? ordenIdx + 1 : 0
                });

                totalDisp  += stock2;
                totalStock += stock;
            }

            Grid1.ItemsSource       = lista;
            TxtTotalArticulos.Text  = lista.Count.ToString("N0");
            TxtTotalDisponible.Text = totalDisp.ToString("N0");
            TxtTotalStock.Text      = totalStock.ToString("N0");
            LblSubtitulo.Text       = $"{lista.Count:N0} artículos";

            if (ModoExportar)
                LblSeleccionados.Text = $"Seleccionados: {_seleccionados.Count}";
        }

        // ─── Obtener filtro del árbol ─────────────────────────────────────────
        private string ObtenerTagFiltro()
        {
            if (Tree1.SelectedItem is TreeViewItem item)
                return item.Tag?.ToString() ?? "todos";
            return "todos";
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<ArticuloFila> FilasGrid =>
            Grid1.ItemsSource as List<ArticuloFila> ?? new List<ArticuloFila>();

        // Construye una sola fila para un artículo (incluye cálculo de stock).
        private ArticuloFila ConstruirFila(string id, int linea)
        {
            string famId  = Sql.ArticulosObj.ObtenerItem("familia",     id)?.ToString() ?? "";
            string codigo = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
            string desc   = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
            string modelo = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
            string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

            double stock  = StockCalculator.ContarStock(id,  DateTime.Now);
            double stock2 = StockCalculator.ContarStock2(id, DateTime.Now);
            int ordenIdx  = _seleccionados.IndexOf(id);

            return new ArticuloFila
            {
                Linea          = linea,
                Id             = id,
                Codigo         = codigo,
                Descripcion    = descCompleta,
                Disponible     = stock2,
                Stock          = stock,
                Seleccionado   = ordenIdx >= 0,
                OrdenSeleccion = ordenIdx >= 0 ? ordenIdx + 1 : 0
            };
        }

        // Renumera la columna Línea, recalcula totales desde el grid y refresca.
        private void RenumerarYActualizarTotales()
        {
            var lista = FilasGrid;
            int n = 1;
            double totalDisp = 0, totalStock = 0;
            foreach (var f in lista)
            {
                f.Linea     = n++;
                totalDisp  += f.Disponible;
                totalStock += f.Stock;
            }
            TxtTotalArticulos.Text  = lista.Count.ToString("N0");
            TxtTotalDisponible.Text = totalDisp.ToString("N0");
            TxtTotalStock.Text      = totalStock.ToString("N0");
            LblSubtitulo.Text       = $"{lista.Count:N0} artículos";
            if (ModoExportar)
                LblSeleccionados.Text = $"Seleccionados: {_seleccionados.Count}";
            Grid1.Items.Refresh();
        }

        private void EnfocarFila(ArticuloFila item)
        {
            Grid1.SelectedItem = item;
            Grid1.ScrollIntoView(item);
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
        }

        // ─── Eventos árbol y búsqueda ─────────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _modoFiltro = "familia";
            CargarArticulos();
        }

        private void Tree1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Si el usuario hace clic en el item ya seleccionado, SelectedItemChanged no se dispara.
            // Detectamos ese caso y recargamos manualmente.
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

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;   // evita que eventos de ratón pendientes quiten el foco al cerrar el diálogo
            EjecutarAccionFila();
        }

        // ─── Tecla Enter ──────────────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            EjecutarAccionFila();
        }

        private void EjecutarAccionFila()
        {
            if (ModoSingle)
            {
                if (Grid1.SelectedItem is not ArticuloFila fila) return;
                _callbackSingle!(new ArticuloExportado
                {
                    Id          = fila.Id,
                    Codigo      = fila.Codigo,
                    Descripcion = fila.Descripcion
                });
                CerrarDialogo();
            }
            else if (ModoExportar)
                ToggleSeleccion();
            else
                AbrirEditar();
        }

        // Cierra la pestaña o la ventana temporal según el contexto de apertura
        private void CerrarDialogo()
        {
            var w = Window.GetWindow(this);
            if (w is ConsolaMovimientos)
                Cerrando?.Invoke();
            else
                w?.Close();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioA = "nuevo";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = "Nuevo Artículo";
            var dlg = new ArticulosDetalle(this, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                if (dlg.ItemCreadoId == null) return;   // cancelado
                var nueva = ConstruirFila(dlg.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                RenumerarYActualizarTotales();
                EnfocarFila(nueva);
            };
            consola.AbrirPestaña(titulo, dlg, "nuevo-articulo");
        }

        private void BtnInsertar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;
            AppState.EventoFormularioA = "insertar";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = "Insertar Artículo";
            var dlg = new ArticulosDetalle(this, fila.Id, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                if (dlg.ItemCreadoId == null) return;   // cancelado
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                var nueva = ConstruirFila(dlg.ItemCreadoId, 0);
                if (idx >= 0) lista.Insert(idx + 1, nueva); else lista.Add(nueva);
                RenumerarYActualizarTotales();
                EnfocarFila(nueva);
            };
            consola.AbrirPestaña(titulo, dlg, "insertar-articulo");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;

            // Verificación de conexión en 2 capas antes de persistir el borrado.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            var res = MessageBox.Show("¿Eliminar este artículo?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            // Ocultar el artículo: su índice queda RESERVADO y NO se reutiliza, por lo
            // que NO se corren los índices de los demás artículos de la familia.
            Sql.ArticulosObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
            Sql.ArticulosObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
            Sql.ArticulosObj.Ocultar(fila.Id);

            Sql.ArticulosObj.OrdenarData(("familia", false), ("indice", false));

            // Quitar solo la fila eliminada del grid (sin recargar todo)
            _seleccionados.Remove(fila.Id);
            var lista = FilasGrid;
            int idx   = lista.IndexOf(fila);
            if (idx >= 0) lista.RemoveAt(idx);
            RenumerarYActualizarTotales();

            if (lista.Count > 0)
                EnfocarFila(lista[Math.Min(idx, lista.Count - 1)]);

            // Artículo eliminado → sincronizar AppSheets (todas las sucursales).
            SincronizarAppsheetsTrasCambio();
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            AppState.ActualizarProductos();
            CargarArbol();
            CargarArticulos();
        }

        /// <summary>
        /// Renumera los artículos ACTIVOS de una familia por su orden de índice actual,
        /// saltando los índices ya ocupados por artículos eliminados/ocultos de esa familia
        /// (que NO se reutilizan). No persiste: el llamador hace OrdenarData/ExportarItems.
        /// </summary>
        public static void RenumerarFamilia(string famId)
        {
            if (string.IsNullOrEmpty(famId)) return;
            var sql = SqlData.Instance;
            var reservados = sql.ArticulosObj.IndicesNoNormales("familia", famId);

            var activos = new List<(string id, int ind)>();
            int uf = sql.ArticulosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (sql.ArticulosObj.ObtenerItem("familia", id)?.ToString() != famId) continue;
                int ind = Convert.ToInt32(sql.ArticulosObj.ObtenerItem("indice", id) ?? 0);
                activos.Add((id, ind));
            }
            activos.Sort((a, b) => a.ind.CompareTo(b.ind));

            int next = 1;
            foreach (var (id, _) in activos)
            {
                while (reservados.Contains(next)) next++;
                sql.ArticulosObj.EstablecerItem("indice", id, next);
                next++;
            }
        }

        /// <summary>
        /// Re-sincroniza la tabla appsheets (todas las sucursales de la empresa activa)
        /// tras agregar o eliminar un artículo. Un fallo aquí NO revierte el cambio del
        /// artículo (ya persistido): solo se informa con una advertencia.
        /// </summary>
        public static void SincronizarAppsheetsTrasCambio()
        {
            try
            {
                AppsheetsSync.SincronizarTodasLasSucursales();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"El artículo se guardó, pero falló la sincronización de AppSheets:\n{ex.Message}",
                    "AppSheets", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnInformeExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InformeExcelArticulos
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
        }

        private void BtnExportar_Click(object sender, RoutedEventArgs e)
        {
            if (_callbackExportar == null) return;

            var exportados = new List<ArticuloExportado>();
            foreach (string id in _seleccionados)
            {
                string codigo = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc   = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
                string famId  = Sql.ArticulosObj.ObtenerItem("familia",     id)?.ToString() ?? "";
                string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
                exportados.Add(new ArticuloExportado
                {
                    Id          = id,
                    Codigo      = codigo,
                    Descripcion = FuncionesComunes.UnirVariables(desc, famDesc, modelo)
                });
            }

            _callbackExportar(exportados);
            CerrarDialogo();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────
        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;
            string idSel = fila.Id;
            int    linea = fila.Linea;
            AppState.EventoFormularioA = "modificar";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            string titulo = $"Artículo {fila.Codigo}";
            var dlg = new ArticulosDetalle(this, idSel, tituloTab: titulo);
            dlg.Cerrando += () =>
            {
                consola.CerrarPestaña(dlg);
                // Reconstruir solo esta fila en su lugar (el Id interno no cambia)
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFila(idSel, linea);
                    lista[idx] = actualizada;
                    RenumerarYActualizarTotales();
                    EnfocarFila(actualizada);
                }
            };
            consola.AbrirPestaña(titulo, dlg, $"articulo-{idSel}");
        }

        private void ToggleSeleccion()
        {
            if (Grid1.SelectedItem is not ArticuloFila fila) return;

            string idActual = fila.Id;   // guardar antes de recargar

            if (_seleccionados.Contains(idActual))
                _seleccionados.Remove(idActual);
            else
                _seleccionados.Add(idActual);

            CargarArticulos();

            // Restaurar selección y foco al mismo ítem
            var item = (Grid1.ItemsSource as System.Collections.Generic.List<ArticuloFila>)
                       ?.Find(x => x.Id == idActual);
            if (item != null) { Grid1.SelectedItem = item; Grid1.ScrollIntoView(item); }
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
        }
    }

    // ─── Modelos ──────────────────────────────────────────────────────────────
    public class ArticuloFila
    {
        public int    Linea          { get; set; }
        public string Id             { get; set; } = "";
        public string Codigo         { get; set; } = "";
        public string Descripcion    { get; set; } = "";
        public double Disponible     { get; set; }
        public double Stock          { get; set; }
        public bool   Seleccionado   { get; set; }
        public int    OrdenSeleccion { get; set; }         // 0 = no seleccionado
        public string OrdenStr       => OrdenSeleccion > 0 ? OrdenSeleccion.ToString() : "";
    }

    public class ArticuloExportado
    {
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
