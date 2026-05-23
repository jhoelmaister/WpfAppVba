using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class InventariosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        public InventariosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => CargarInventarios();
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarInventarios()
        {
            var lista = new List<InventarioFila>();
            int linea = 1;

            int uf = Sql.DocumentosIObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosIObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                var fechaObj = Sql.DocumentosIObj.ObtenerItem("fecha", id);
                DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
                double cantidad = CalcularCantidad(id);

                lista.Add(new InventarioFila
                {
                    Linea         = linea++,
                    Id            = id,
                    Fecha         = fecha,
                    FechaStr      = fecha != default ? fecha.ToString("dd/MM/yyyy HH:mm:ss") : "",
                    CantidadTotal = cantidad
                });
            }

            Grid1.ItemsSource = lista;
        }

        // ─── Calcula la cantidad total de un documento de inventario ──────────
        private double CalcularCantidad(string documentoI)
        {
            double cant = 0;
            int uf = Sql.InventariosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.InventariosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.InventariosObj.ObtenerItem("documentoI", id)?.ToString() != documentoI)
                    continue;

                cant += Convert.ToDouble(Sql.InventariosObj.ObtenerItem("cantidad", id) ?? 0);
            }
            return cant;
        }

        // ─── Doble clic = editar ───────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioI = "nuevo";
            var detalle = new InventariosDetalle(this);
            detalle.ShowDialog();
            CargarInventarios();
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not InventarioFila fila) return;

            // Solo se puede eliminar la apertura más reciente
            if (fila.Id != AppState.AperturaIdActiva.ToString())
            {
                MessageBox.Show("Solo se puede eliminar la última apertura.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show("¿Eliminar este inventario de apertura?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                // Ocultar todos los inventarios de este documentoI
                int uf = Sql.InventariosObj.ContarFilas;
                var idsEliminar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.InventariosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.InventariosObj.ObtenerItem("documentoI", id)?.ToString() == fila.Id)
                        idsEliminar.Add(id);
                }
                foreach (string id in idsEliminar)
                    Sql.InventariosObj.Ocultar(id);

                // Ocultar el documento de inventario
                Sql.DocumentosIObj.Ocultar(fila.Id);

                Sql.InventariosObj.OrdenarData(("documentoI", false), ("indice", false));
                Sql.DocumentosIObj.OrdenarData(("id", false));

                int periodo = string.IsNullOrEmpty(AppState.PeriodoActivo)
                    ? DateTime.Now.Year
                    : int.Parse(AppState.PeriodoActivo);
                AppState.ActualizarBase(periodo);
                AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);

                CargarInventarios();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Helper ───────────────────────────────────────────────────────────
        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not InventarioFila fila) return;

            // Solo se puede editar la apertura más reciente
            if (fila.Id != AppState.AperturaIdActiva.ToString())
            {
                MessageBox.Show("Solo se puede editar la última apertura.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string idSel = fila.Id;
            AppState.EventoFormularioI = "editar";
            var detalle = new InventariosDetalle(this, fila.Id);
            detalle.ShowDialog();
            CargarInventarios();
            var item = (Grid1.ItemsSource as System.Collections.Generic.List<InventarioFila>)
                       ?.Find(x => x.Id == idSel);
            if (item != null) { Grid1.SelectedItem = item; Grid1.ScrollIntoView(item); }
            Grid1.Focus();
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class InventarioFila
    {
        public int      Linea         { get; set; }
        public string   Id            { get; set; } = "";
        public DateTime Fecha         { get; set; }
        public string   FechaStr      { get; set; } = "";
        public double   CantidadTotal { get; set; }
    }
}
