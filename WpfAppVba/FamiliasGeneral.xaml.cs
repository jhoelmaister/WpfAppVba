using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class FamiliasGeneral : Window
    {
        private static SqlData Sql => SqlData.Instance;
        private readonly bool _modoSelector;

        /// <summary>
        /// Cuando se abre en modo selector, aquí queda el ID de la familia elegida.
        /// </summary>
        public static string? FamiliaSeleccionada { get; set; }

        public FamiliasGeneral(bool modoSelector = false)
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            _modoSelector = modoSelector;
            Loaded += (_, _) =>
            {
                if (_modoSelector)
                    Title = "Seleccionar Familia";
                CargarFamilias();
            };
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarFamilias()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<FamiliaFila>();
            int linea = 1;

            int uf = Sql.FamiliasObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.FamiliasObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc = Sql.FamiliasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string productoId = Sql.FamiliasObj.ObtenerItem("producto", id)?.ToString() ?? "";
                string productoDesc = Sql.ProductosObj.ObtenerItem("descripcion", productoId)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    id.ToLower().Contains(busqueda) ||
                    productoDesc.ToLower().Contains(busqueda))
                {
                    filas.Add(new FamiliaFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Descripcion = desc,
                        Producto    = productoDesc
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<FamiliaFila> FilasGrid =>
            Grid1.ItemsSource as List<FamiliaFila> ?? new List<FamiliaFila>();

        private FamiliaFila ConstruirFilaFamilia(string id, int linea)
        {
            string productoId   = Sql.FamiliasObj.ObtenerItem("producto",    id)?.ToString() ?? "";
            string productoDesc = Sql.ProductosObj.ObtenerItem("descripcion", productoId)?.ToString() ?? "";
            return new FamiliaFila
            {
                Linea       = linea,
                Id          = id,
                Descripcion = Sql.FamiliasObj.ObtenerItem("descripcion", id)?.ToString() ?? "",
                Producto    = productoDesc
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
            => CargarFamilias();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarFamilias();

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
            if (Grid1.SelectedItem is not FamiliaFila fila) return;
            FamiliaSeleccionada = fila.Id;
            Close();
        }

        // ─── Botones ───────────────────────────────────────────────────────────

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioF = "nuevo";
            var detalle = new FamiliasDetalle(this) { Owner = this };
            detalle.ShowDialog();
            if (detalle.ItemCreadoId == null) return;   // cancelado

            var nueva = ConstruirFilaFamilia(detalle.ItemCreadoId, 0);
            FilasGrid.Add(nueva);
            Renumerar();
            Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not FamiliaFila fila) return;

            var res = MessageBox.Show("¿Eliminar esta familia?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.FamiliasObj.Ocultar(fila.Id);
                Sql.FamiliasObj.OrdenarData(("id", false));

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
            Sql.FamiliasObj.Actualizar();
            CargarFamilias();
        }

        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not FamiliaFila fila) return;
            string idSel = fila.Id;
            AppState.EventoFormularioF = "modificar";
            var detalle = new FamiliasDetalle(this, fila.Id) { Owner = this };
            detalle.ShowDialog();

            var lista = FilasGrid;
            int idx   = lista.IndexOf(fila);
            if (idx >= 0)
            {
                var actualizada = ConstruirFilaFamilia(idSel, fila.Linea);
                lista[idx] = actualizada;
                Renumerar();
                Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class FamiliaFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Producto    { get; set; } = "";
    }
}
