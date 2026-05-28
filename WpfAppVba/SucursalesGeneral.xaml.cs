using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class SucursalesGeneral : Window
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly bool _modoSelector;

        /// <summary>
        /// En modo selector (llamado desde TraspasosDetalle), el doble clic
        /// establece SucursalSeleccionada y cierra la ventana.
        /// </summary>
        public static string? SucursalSeleccionada = null;

        public SucursalesGeneral(bool modoSelector = false)
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            _modoSelector = modoSelector;
            Loaded += (_, _) => CargarSucursales();
        }

        // ─── Verificación de administrador ─────────────────────────────────────
        private bool EsAdmin()
            => Sql.UsuariosObj.ObtenerItem("tipo", AppState.UsuarioActivo.ToString())?.ToString() == "admin";

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarSucursales()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<SucursalFila>();
            int linea = 1;

            int uf = Sql.SucursalesObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.SucursalesObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc = Sql.SucursalesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string nit  = Sql.SucursalesObj.ObtenerItem("nit",         id)?.ToString() ?? "";
                string regionId   = Sql.SucursalesObj.ObtenerItem("region", id)?.ToString() ?? "";
                string regionDesc = Sql.RegionesObj.ObtenerItem("descripcion", regionId)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    nit.ToLower().Contains(busqueda) ||
                    regionDesc.ToLower().Contains(busqueda))
                {
                    filas.Add(new SucursalFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Nit         = nit,
                        Descripcion = desc,
                        Region      = regionDesc
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Búsqueda en tiempo real ───────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarSucursales();

        // ─── Doble clic ────────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_modoSelector)
            {
                if (Grid1.SelectedItem is not SucursalFila fila) return;
                SucursalSeleccionada = fila.Id;
                Close();
            }
            else
            {
                AbrirEditar();
            }
        }

        // ─── Botones ───────────────────────────────────────────────────────────

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            if (!EsAdmin())
            {
                MessageBox.Show("Se requieren permisos de administrador.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? idSel = (Grid1.SelectedItem as SucursalFila)?.Id;
            AppState.EventoFormularioI = "nuevo";
            var detalle = new SucursalesDetalle(this) { Owner = this };
            detalle.ShowDialog();
            CargarSucursales();
            string? enfocar = detalle.ItemCreadoId ?? idSel;
            if (enfocar != null)
            {
                var item = (Grid1.ItemsSource as System.Collections.Generic.List<SucursalFila>)
                           ?.Find(x => x.Id == enfocar);
                if (item != null) { Grid1.SelectedItem = item; Grid1.ScrollIntoView(item); }
            }
            Grid1.Focus();
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (!EsAdmin())
            {
                MessageBox.Show("Se requieren permisos de administrador.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Grid1.SelectedItem is not SucursalFila fila) return;

            var res = MessageBox.Show("¿Eliminar esta sucursal?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.SucursalesObj.Ocultar(fila.Id);
                Sql.SucursalesObj.OrdenarData(("id", false));
                CargarSucursales();
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            Sql.SucursalesObj.Actualizar();
            CargarSucursales();
        }

        private void AbrirEditar()
        {
            if (!EsAdmin())
            {
                MessageBox.Show("Se requieren permisos de administrador.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Grid1.SelectedItem is not SucursalFila fila) return;
            string idSel = fila.Id;
            AppState.EventoFormularioI = "modificar";
            var detalle = new SucursalesDetalle(this, fila.Id) { Owner = this };
            detalle.ShowDialog();
            CargarSucursales();
            var item = (Grid1.ItemsSource as System.Collections.Generic.List<SucursalFila>)
                       ?.Find(x => x.Id == idSel);
            if (item != null) { Grid1.SelectedItem = item; Grid1.ScrollIntoView(item); }
            Grid1.Focus();
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class SucursalFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Nit         { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Region      { get; set; } = "";
    }
}
