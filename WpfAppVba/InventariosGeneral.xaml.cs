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

        private bool _iniciado = false;

        public InventariosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarInventarios(); };
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
                    Codigo        = Sql.DocumentosIObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                    Fecha         = fecha,
                    FechaStr      = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                    CantidadTotal = cantidad
                });
            }

            Grid1.ItemsSource = lista;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<InventarioFila> FilasGrid =>
            Grid1.ItemsSource as List<InventarioFila> ?? new List<InventarioFila>();

        private InventarioFila ConstruirFilaInventario(string id, int linea)
        {
            var fechaObj = Sql.DocumentosIObj.ObtenerItem("fecha", id);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
            return new InventarioFila
            {
                Linea         = linea,
                Id            = id,
                Codigo        = Sql.DocumentosIObj.ObtenerItem("codigo", id)?.ToString() ?? "",
                Fecha         = fecha,
                FechaStr      = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                CantidadTotal = CalcularCantidad(id)
            };
        }

        private void Renumerar()
        {
            int n = 1;
            foreach (var f in FilasGrid) f.Linea = n++;
            Grid1.Items.Refresh();
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

        // ─── Tecla Enter = editar ─────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioI = "nuevo";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new InventariosDetalle(this);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                if (detalle.ItemCreadoId == null) return;   // cancelado
                var nueva = ConstruirFilaInventario(detalle.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                Renumerar();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña("Nuevo Inventario", detalle, "nuevo-inventario");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            CargarInventarios();
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not InventarioFila fila) return;

            // Solo se puede eliminar la apertura más reciente
            if (fila.Id != AppState.AperturaIdActiva)
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
                Sql.DocumentosIObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.DocumentosIObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.DocumentosIObj.Ocultar(fila.Id);

                Sql.InventariosObj.OrdenarData(("documentoI", false), ("indice", false));
                Sql.DocumentosIObj.OrdenarData(("id", false));

                int periodo = string.IsNullOrEmpty(AppState.PeriodoActivo)
                    ? DateTime.Now.Year
                    : int.Parse(AppState.PeriodoActivo);
                AppState.ActualizarBase(periodo);
                AppLoader.ConectarDocumentos(AppState.DataFechaInicio, AppState.DataFechaFinal);

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
            if (fila.Id != AppState.AperturaIdActiva)
            {
                MessageBox.Show("Solo se puede editar la última apertura.", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string idSel = fila.Id;
            int    linea = fila.Linea;
            AppState.EventoFormularioI = "editar";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new InventariosDetalle(this, fila.Id);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFilaInventario(idSel, linea);
                    lista[idx] = actualizada;
                    Renumerar();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña($"Inventario {idSel}", detalle, $"inventario-{idSel}");
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class InventarioFila
    {
        public int      Linea         { get; set; }
        public string   Id            { get; set; } = "";
        public string   Codigo        { get; set; } = "";
        public DateTime Fecha         { get; set; }
        public string   FechaStr      { get; set; } = "";
        public double   CantidadTotal { get; set; }
    }
}
