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
            Loaded += (_, _) =>
            {
                if (_modoSelector)
                    Title = "Seleccionar Sucursal";
                CargarSucursales();
            };
        }

        // ─── Verificación de administrador ─────────────────────────────────────
        private bool EsAdmin()
            => Sql.UsuariosObj.ObtenerItem("tipo", AppState.UsuarioActivo.ToString())?.ToString() == "admin";

        // ─── Carga la lista completa ───────────────────────────────────────────
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

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda))
                {
                    filas.Add(new SucursalFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Descripcion = desc,
                        FechaStr    = FormatearFecha(id)
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental ──────────────────────────────
        private List<SucursalFila> FilasGrid =>
            Grid1.ItemsSource as List<SucursalFila> ?? new List<SucursalFila>();

        private SucursalFila ConstruirFilaSucursal(string id, int linea)
        {
            return new SucursalFila
            {
                Linea       = linea,
                Id          = id,
                Descripcion = Sql.SucursalesObj.ObtenerItem("descripcion", id)?.ToString() ?? "",
                FechaStr    = FormatearFecha(id)
            };
        }

        // ─── Formatea la fecha completa (fecha + hora) de una sucursal ─────────
        private string FormatearFecha(string id)
        {
            var fechaObj = Sql.SucursalesObj.ObtenerItem("fecha", id);
            if (fechaObj != null && System.DateTime.TryParse(fechaObj.ToString(), out System.DateTime fecha))
                return $"{fecha:d} {fecha:HH:mm:ss}";
            return "";
        }

        private void Renumerar()
        {
            int n = 1;
            foreach (var f in FilasGrid) f.Linea = n++;
            Grid1.Items.Refresh();
        }

        // ─── Búsqueda ─────────────────────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarSucursales();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarSucursales();

        // ─── Doble clic ────────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_modoSelector) Seleccionar();
            else               AbrirEditar();
        }

        // ─── Tecla Enter ───────────────────────────────────────────────────────
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
            if (Grid1.SelectedItem is not SucursalFila fila) return;
            SucursalSeleccionada = fila.Id;
            Close();
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

            AppState.EventoFormularioI = "nuevo";
            var detalle = new SucursalesDetalle(this) { Owner = this };
            detalle.ShowDialog();
            if (detalle.ItemCreadoId == null) return;

            var nueva = ConstruirFilaSucursal(detalle.ItemCreadoId, 0);
            FilasGrid.Add(nueva);
            Renumerar();
            Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
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
                Sql.SucursalesObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.SucursalesObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.SucursalesObj.Ocultar(fila.Id);
                Sql.SucursalesObj.OrdenarData(("id", false));

                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0) lista.RemoveAt(idx);
                Renumerar();

                if (lista.Count > 0)
                {
                    var sel = lista[System.Math.Min(idx, lista.Count - 1)];
                    Grid1.SelectedItem = sel; Grid1.ScrollIntoView(sel);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
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

            var lista = FilasGrid;
            int idx   = lista.IndexOf(fila);
            if (idx >= 0)
            {
                var actualizada = ConstruirFilaSucursal(idSel, fila.Linea);
                lista[idx] = actualizada;
                Renumerar();
                Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
            }
            GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class SucursalFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string FechaStr    { get; set; } = "";
    }
}
