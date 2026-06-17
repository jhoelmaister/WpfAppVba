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

        // Cuando es true, los cambios de selección del árbol NO recargan la grilla
        // (se usa durante el refresco incremental de "Actualizar" para no recargar todo).
        private bool _suspenderEventosArbol = false;

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
            foreach (var d in ConstruirArbolDeseado())
                Tree1.Items.Add(CrearNodo(d, expandidoPorDefecto: true));

            // Seleccionar primer nodo (raíz "Todos")
            if (Tree1.Items.Count > 0 && Tree1.Items[0] is TreeViewItem raiz)
                raiz.IsSelected = true;
        }

        // Estructura "deseada" del árbol según la caché actual (fuente única para la
        // carga inicial y el refresco incremental).
        private List<NodoDeseado> ConstruirArbolDeseado()
        {
            var nodoTodos = new NodoDeseado { Tag = "todos", Header = "Todos" };

            int ufProd = Sql.ProductosObj.ContarFilas;
            for (int i = 1; i <= ufProd; i++)
            {
                var idObj = Sql.ProductosObj.Mover(i);
                if (idObj == null) continue;
                string prodId   = idObj.ToString()!;
                string prodDesc = Sql.ProductosObj.ObtenerItem("descripcion", prodId)?.ToString() ?? prodId;

                var nodoProd = new NodoDeseado { Tag = $"producto:{prodId}", Header = prodDesc };

                int ufFam = Sql.FamiliasObj.ContarFilas;
                for (int j = 1; j <= ufFam; j++)
                {
                    var famIdObj = Sql.FamiliasObj.Mover(j);
                    if (famIdObj == null) continue;
                    string famId = famIdObj.ToString()!;
                    if ((Sql.FamiliasObj.ObtenerItem("producto", famId)?.ToString() ?? "") != prodId) continue;

                    string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? famId;
                    nodoProd.Hijos.Add(new NodoDeseado { Tag = $"familia:{famId}", Header = famDesc });
                }

                nodoTodos.Hijos.Add(nodoProd);
            }

            return new List<NodoDeseado> { nodoTodos };
        }

        private static TreeViewItem CrearNodo(NodoDeseado d, bool expandidoPorDefecto)
        {
            var tvi = new TreeViewItem { Header = d.Header, Tag = d.Tag };
            foreach (var h in d.Hijos) tvi.Items.Add(CrearNodo(h, expandidoPorDefecto));
            if (d.Hijos.Count > 0) tvi.IsExpanded = expandidoPorDefecto;
            return tvi;
        }

        // Reconcilia el árbol existente con la estructura deseada: actualiza encabezados,
        // agrega nodos nuevos y quita los eliminados, preservando la expansión y selección
        // de los nodos que se conservan (no reconstruye todo).
        private void RefrescarArbolIncremental() => ReconciliarNodos(Tree1.Items, ConstruirArbolDeseado());

        private void ReconciliarNodos(ItemCollection existentes, List<NodoDeseado> deseados)
        {
            var porTag = new Dictionary<string, TreeViewItem>();
            foreach (var obj in existentes)
                if (obj is TreeViewItem t && t.Tag is string tag) porTag[tag] = t;

            var nuevos = new List<TreeViewItem>(deseados.Count);
            foreach (var d in deseados)
            {
                if (porTag.TryGetValue(d.Tag, out var ex))
                {
                    if (!Equals(ex.Header, d.Header)) ex.Header = d.Header;
                    ReconciliarNodos(ex.Items, d.Hijos);   // recurse (preserva IsExpanded)
                    nuevos.Add(ex);
                }
                else nuevos.Add(CrearNodo(d, expandidoPorDefecto: true));
            }

            // Solo tocar la colección si cambió la membresía o el orden (evita perder
            // la selección/expansión cuando no hubo cambios reales).
            bool igual = existentes.Count == nuevos.Count;
            if (igual)
                for (int i = 0; i < nuevos.Count; i++)
                    if (!ReferenceEquals(existentes[i], nuevos[i])) { igual = false; break; }

            if (!igual)
            {
                existentes.Clear();
                foreach (var n in nuevos) existentes.Add(n);
            }
        }

        private void RestaurarSeleccionArbol(string tag)
        {
            var nodo = BuscarNodoPorTag(Tree1.Items, tag) ?? BuscarNodoPorTag(Tree1.Items, "todos");
            if (nodo != null && !nodo.IsSelected) nodo.IsSelected = true;
        }

        private static TreeViewItem? BuscarNodoPorTag(ItemCollection items, string tag)
        {
            foreach (var obj in items)
            {
                if (obj is not TreeViewItem t) continue;
                if (t.Tag is string tg && tg == tag) return t;
                var hijo = BuscarNodoPorTag(t.Items, tag);
                if (hijo != null) return hijo;
            }
            return null;
        }

        // ─── Carga la lista de artículos ──────────────────────────────────────
        public void CargarArticulos()
        {
            Grid1.ItemsSource = ConstruirListaArticulos();
            RenumerarYActualizarTotales();
        }

        // Construye la lista de artículos según el filtro activo (sin tocar la UI).
        private List<ArticuloFila> ConstruirListaArticulos()
        {
            var lista = new List<ArticuloFila>();
            int linea = 1;

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
                    // "todos" o vacío → sin filtro
                }

                string codigo    = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string desc      = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string modelo    = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
                string famDesc   = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
                string catDesc   = ObtenerDescripcionCategoria(id);
                string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

                // Filtro de búsqueda (código, descripción completa o categoría)
                if (!string.IsNullOrEmpty(busqueda))
                {
                    if (!codigo.ToLower().Contains(busqueda) &&
                        !descCompleta.ToLower().Contains(busqueda) &&
                        !catDesc.ToLower().Contains(busqueda))
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
                    Categoria      = catDesc,
                    Descripcion    = descCompleta,
                    Disponible     = stock2,
                    Stock          = stock,
                    Seleccionado   = ordenIdx >= 0,
                    OrdenSeleccion = ordenIdx >= 0 ? ordenIdx + 1 : 0
                });
            }

            return lista;
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

        // Refresca la grilla con la caché actual sin recrear las filas: actualiza en
        // sitio las que ya existen (por Id), agrega las nuevas, quita las eliminadas y
        // reordena según el filtro activo. Conserva las instancias (y por tanto la
        // selección por referencia) de las filas que se mantienen.
        private void RefrescarGridIncremental()
        {
            var deseadas = ConstruirListaArticulos();

            var actuales = FilasGrid;
            var porId = new Dictionary<string, ArticuloFila>();
            foreach (var f in actuales) porId[f.Id] = f;

            var resultado = new List<ArticuloFila>(deseadas.Count);
            foreach (var d in deseadas)
            {
                if (porId.TryGetValue(d.Id, out var ex))
                {
                    ex.Codigo         = d.Codigo;
                    ex.Categoria      = d.Categoria;
                    ex.Descripcion    = d.Descripcion;
                    ex.Disponible     = d.Disponible;
                    ex.Stock          = d.Stock;
                    ex.Seleccionado   = d.Seleccionado;
                    ex.OrdenSeleccion = d.OrdenSeleccion;
                    resultado.Add(ex);
                }
                else resultado.Add(d);
            }

            actuales.Clear();
            actuales.AddRange(resultado);
            RenumerarYActualizarTotales();   // renumera Línea + totales + Items.Refresh()
        }

        private void RestaurarSeleccionGrid(string artId)
        {
            if (string.IsNullOrEmpty(artId)) return;
            var item = FilasGrid.Find(x => x.Id == artId);
            if (item != null) EnfocarFila(item);
        }

        // Construye una sola fila para un artículo (incluye cálculo de stock).
        private ArticuloFila ConstruirFila(string id, int linea)
        {
            string famId  = Sql.ArticulosObj.ObtenerItem("familia",     id)?.ToString() ?? "";
            string codigo = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
            string desc   = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
            string modelo = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
            string catDesc = ObtenerDescripcionCategoria(id);
            string descCompleta = FuncionesComunes.UnirVariables(desc, famDesc, modelo);

            double stock  = StockCalculator.ContarStock(id,  DateTime.Now);
            double stock2 = StockCalculator.ContarStock2(id, DateTime.Now);
            int ordenIdx  = _seleccionados.IndexOf(id);

            return new ArticuloFila
            {
                Linea          = linea,
                Id             = id,
                Codigo         = codigo,
                Categoria      = catDesc,
                Descripcion    = descCompleta,
                Disponible     = stock2,
                Stock          = stock,
                Seleccionado   = ordenIdx >= 0,
                OrdenSeleccion = ordenIdx >= 0 ? ordenIdx + 1 : 0
            };
        }

        // Descripción de la categoría de un artículo (columna 'Categoria' del artículo).
        private string ObtenerDescripcionCategoria(string id)
        {
            string catId = Sql.ArticulosObj.ObtenerItem("Categoria", id)?.ToString() ?? "";
            if (string.IsNullOrEmpty(catId)) return "";
            return Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "";
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
            if (_suspenderEventosArbol) return;   // refresco incremental en curso
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

            // Recordar el enfoque actual para restaurarlo tras el refresco.
            string tagArbolSel = (Tree1.SelectedItem as TreeViewItem)?.Tag?.ToString() ?? "todos";
            string artSelId    = (Grid1.SelectedItem as ArticuloFila)?.Id ?? "";

            // Recargar la caché desde SQL (única fuente de datos).
            AppState.ActualizarProductos();

            // Árbol: refresco incremental (alta/baja/edición de nodos) preservando
            // expansión y selección; sin disparar la recarga de la grilla por el evento.
            _suspenderEventosArbol = true;
            RefrescarArbolIncremental();
            RestaurarSeleccionArbol(tagArbolSel);
            _suspenderEventosArbol = false;

            // Grilla: refresco incremental preservando selección y foco.
            RefrescarGridIncremental();
            RestaurarSeleccionGrid(artSelId);
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

        // Nodo "deseado" del árbol (estructura ligera para la reconciliación incremental).
        private sealed class NodoDeseado
        {
            public string Tag    = "";
            public string Header = "";
            public List<NodoDeseado> Hijos = new();
        }
    }

    // ─── Modelos ──────────────────────────────────────────────────────────────
    public class ArticuloFila
    {
        public int    Linea          { get; set; }
        public string Id             { get; set; } = "";
        public string Codigo         { get; set; } = "";
        public string Categoria      { get; set; } = "";
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
