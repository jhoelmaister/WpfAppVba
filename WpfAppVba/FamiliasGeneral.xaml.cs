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
        public static string? FamiliaSeleccionada { get; private set; }

        public FamiliasGeneral(bool modoSelector = false)
        {
            InitializeComponent();
            _modoSelector = modoSelector;
            Loaded += (_, _) =>
            {
                if (_modoSelector)
                {
                    Title                     = "Seleccionar Familia";
                    PanelAdmin.Visibility     = Visibility.Collapsed;
                    BtnSeleccionar.Visibility = Visibility.Visible;
                }
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

        // ─── Búsqueda en tiempo real ───────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarFamilias();

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
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

        private void BtnSeleccionar_Click(object sender, RoutedEventArgs e)
            => Seleccionar();

        // ─── Botones ───────────────────────────────────────────────────────────

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioF = "nuevo";
            var detalle = new FamiliasDetalle(this);
            detalle.ShowDialog();
            CargarFamilias();
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
                CargarFamilias();
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
            AppState.EventoFormularioF = "modificar";
            var detalle = new FamiliasDetalle(this, fila.Id);
            detalle.ShowDialog();
            CargarFamilias();
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
