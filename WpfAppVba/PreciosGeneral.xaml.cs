using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class PreciosGeneral : Window
    {
        private static SqlData Sql => SqlData.Instance;

        // Modo de filtro activo: "todos" | "busqueda" | "familia"
        private string _modoFiltro = "todos";

        public PreciosGeneral()
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            Loaded += (_, _) => { CargarArbol(); CargarArticulos(); };
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

            // Nodo "Sin Clasificar"
            var nodoSin = new TreeViewItem { Header = "Sin Clasificar", Tag = "sinclasificar" };
            nodoTodos.Items.Add(nodoSin);

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
            LblHistorial.Text = "Historial de precios:";
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

            int uf = Sql.PreciosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PreciosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() != articuloId) continue;

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

            return new PrecioHistFila
            {
                Linea    = linea,
                Id       = id,
                Fecha    = fecha,
                FechaStr = fecha != default ? $"{fecha:d}" : "",
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
                LblHistorial.Text = "Historial de precios:";
                return;
            }
            LblHistorial.Text = $"Historial de precios: {fila.Codigo} - {fila.Descripcion}";
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
            if (ArticuloSeleccionado is not PrecioArticuloFila fila)
            {
                MessageBox.Show("Seleccione un artículo primero.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var detalle = new PreciosDetalle(fila.Id, fila.Codigo, fila.Descripcion) { Owner = this };
            detalle.ShowDialog();
            if (detalle.ItemCreadoId == null) return;

            var nueva = ConstruirFilaPrecio(detalle.ItemCreadoId, 0);
            PreciosGrid.Add(nueva);
            RenumerarPrecios();
            GridPrecios.SelectedItem = nueva; GridPrecios.ScrollIntoView(nueva);
            GridFocusHelper.EnfocarCeldaSeleccionada(GridPrecios);
        }

        private void BtnEditarPrecio_Click(object sender, RoutedEventArgs e)
        {
            if (ArticuloSeleccionado is not PrecioArticuloFila art) return;
            if (GridPrecios.SelectedItem is not PrecioHistFila fila) return;

            string idSel = fila.Id;
            var detalle = new PreciosDetalle(art.Id, art.Codigo, art.Descripcion, fila.Id) { Owner = this };
            detalle.ShowDialog();

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
        }

        private void BtnEliminarPrecio_Click(object sender, RoutedEventArgs e)
        {
            if (GridPrecios.SelectedItem is not PrecioHistFila fila) return;

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
            Sql.ArticulosObj.Actualizar();
            Sql.PreciosObj.Actualizar();
            CargarArbol();
            CargarArticulos();
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
        public DateTime Fecha    { get; set; }
        public string   FechaStr { get; set; } = "";
        public string   Region   { get; set; } = "";
        public double   Precio   { get; set; }
    }
}
