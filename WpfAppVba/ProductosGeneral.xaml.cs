using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ProductosGeneral : Window
    {
        private static SqlData Sql => SqlData.Instance;
        private readonly bool _modoSelector;

        /// <summary>Cuando se abre en modo selector, aquí queda el ID del producto elegido.</summary>
        public static string? ProductoSeleccionado { get; set; }

        public ProductosGeneral(bool modoSelector = false)
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            _modoSelector = modoSelector;
            Loaded += (_, _) =>
            {
                if (_modoSelector)
                    Title = "Seleccionar Producto";
                CargarProductos();
            };
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarProductos()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<ProductoFila>();
            int linea = 1;

            int uf = Sql.ProductosObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.ProductosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc = Sql.ProductosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    id.ToLower().Contains(busqueda))
                {
                    filas.Add(new ProductoFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Descripcion = desc
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<ProductoFila> FilasGrid =>
            Grid1.ItemsSource as List<ProductoFila> ?? new List<ProductoFila>();

        private ProductoFila ConstruirFila(string id, int linea)
        {
            return new ProductoFila
            {
                Linea       = linea,
                Id          = id,
                Descripcion = Sql.ProductosObj.ObtenerItem("descripcion", id)?.ToString() ?? ""
            };
        }

        private void Renumerar()
        {
            int n = 1;
            foreach (var f in FilasGrid) f.Linea = n++;
            Grid1.Items.Refresh();
        }

        // ─── Búsqueda ─────────────────────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarProductos();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarProductos();

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_modoSelector) Seleccionar();
            else               AbrirEditar();
        }

        // ─── Tecla Enter ──────────────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            if (_modoSelector) Seleccionar();
            else               AbrirEditar();
        }

        // ─── Modo selector ─────────────────────────────────────────────────────
        private void Seleccionar()
        {
            if (Grid1.SelectedItem is not ProductoFila fila) return;
            ProductoSeleccionado = fila.Id;
            Close();
        }

        // ─── Botones ───────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var detalle = new ProductosDetalle() { Owner = this };
            detalle.ShowDialog();
            if (detalle.ItemCreadoId == null) return;   // cancelado

            var nueva = ConstruirFila(detalle.ItemCreadoId, 0);
            FilasGrid.Add(nueva);
            Renumerar();
            Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not ProductoFila fila) return;

            var res = MessageBox.Show("¿Eliminar este producto?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.ProductosObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.ProductosObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.ProductosObj.Ocultar(fila.Id);
                Sql.ProductosObj.OrdenarData(("id", false));

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
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            Sql.ProductosObj.Actualizar();
            CargarProductos();
        }

        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not ProductoFila fila) return;
            string idSel = fila.Id;
            var detalle = new ProductosDetalle(fila.Id) { Owner = this };
            detalle.ShowDialog();

            var lista = FilasGrid;
            int idx   = lista.IndexOf(fila);
            if (idx >= 0)
            {
                var actualizada = ConstruirFila(idSel, fila.Linea);
                lista[idx] = actualizada;
                Renumerar();
                Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class ProductoFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
