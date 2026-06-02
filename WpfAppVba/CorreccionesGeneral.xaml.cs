using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class CorreccionesGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private string _filtroMovimiento = "";  // "" = todos
        private string _busqueda          = "";

        public CorreccionesGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => CargarCorrecciones();
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarCorrecciones()
        {
            if (Grid1 == null) return;

            var lista = new List<CorreccionFila>();
            int linea = 1;

            int uf = Sql.DocumentosCObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosCObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                // Filtrar por sucursal activa
                string suc = Sql.DocumentosCObj.ObtenerItem("sucursal", id)?.ToString() ?? "";
                if (suc != AppState.SucursalActiva.ToString()) continue;

                string movimiento = Sql.DocumentosCObj.ObtenerItem("movimiento", id)?.ToString() ?? "";
                string motivo     = Sql.DocumentosCObj.ObtenerItem("motivo",     id)?.ToString() ?? "";

                // Filtro por movimiento
                if (!string.IsNullOrEmpty(_filtroMovimiento) &&
                    !string.Equals(movimiento, _filtroMovimiento, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Filtro por búsqueda (id o motivo)
                if (!string.IsNullOrEmpty(_busqueda) &&
                    !id.ToLower().Contains(_busqueda) &&
                    !motivo.ToLower().Contains(_busqueda))
                    continue;

                var fechaObj = Sql.DocumentosCObj.ObtenerItem("fecha", id);
                DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;

                lista.Add(new CorreccionFila
                {
                    Linea         = linea++,
                    Id            = id,
                    Fecha         = fecha,
                    FechaStr      = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                    Movimiento    = movimiento,
                    Motivo        = motivo,
                    CantidadTotal = CalcularCantidad(id)
                });
            }

            Grid1.ItemsSource = lista;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<CorreccionFila> FilasGrid =>
            Grid1.ItemsSource as List<CorreccionFila> ?? new List<CorreccionFila>();

        private CorreccionFila ConstruirFila(string id, int linea)
        {
            var fechaObj = Sql.DocumentosCObj.ObtenerItem("fecha", id);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
            return new CorreccionFila
            {
                Linea         = linea,
                Id            = id,
                Fecha         = fecha,
                FechaStr      = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                Movimiento    = Sql.DocumentosCObj.ObtenerItem("movimiento", id)?.ToString() ?? "",
                Motivo        = Sql.DocumentosCObj.ObtenerItem("motivo",     id)?.ToString() ?? "",
                CantidadTotal = CalcularCantidad(id)
            };
        }

        private void Renumerar()
        {
            int n = 1;
            foreach (var f in FilasGrid) f.Linea = n++;
            Grid1.Items.Refresh();
        }

        // ─── Suma la cantidad total de las líneas de un documento ─────────────
        private static double CalcularCantidad(string documentoC)
        {
            double cant = 0;
            int uf = Sql.CorreccionesObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.CorreccionesObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.CorreccionesObj.ObtenerItem("documentoC", id)?.ToString() != documentoC)
                    continue;

                cant += Convert.ToDouble(Sql.CorreccionesObj.ObtenerItem("cantidad", id) ?? 0);
            }
            return cant;
        }

        // ─── Filtros / búsqueda ───────────────────────────────────────────────
        private void CboFiltroMovimiento_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Grid1 == null) return;
            string val = (CboFiltroMovimiento.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todos";
            _filtroMovimiento = val == "Todos" ? "" : val;
            CargarCorrecciones();
        }

        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _busqueda = TxtBuscar.Text.Trim().ToLower();
            CargarCorrecciones();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            _busqueda = TxtBuscar.Text.Trim().ToLower();
            CargarCorrecciones();
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
            AppState.EventoFormularioC = "nuevo";
            var detalle = new CorreccionesDetalle(this) { Owner = Window.GetWindow(this) };
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
            if (Grid1.SelectedItem is not CorreccionFila fila) return;

            var res = MessageBox.Show("¿Eliminar esta corrección y todas sus líneas?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            try
            {
                // Ocultar todas las líneas de este documentoC
                int uf = Sql.CorreccionesObj.ContarFilas;
                var idsOcultar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.CorreccionesObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.CorreccionesObj.ObtenerItem("documentoC", id)?.ToString() == fila.Id)
                        idsOcultar.Add(id);
                }
                foreach (string id in idsOcultar)
                    Sql.CorreccionesObj.Ocultar(id);

                // Ocultar el documento de corrección
                Sql.DocumentosCObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.DocumentosCObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.DocumentosCObj.Ocultar(fila.Id);

                Sql.CorreccionesObj.OrdenarData(("documentoC", false), ("indice", false));
                Sql.DocumentosCObj.OrdenarData(("fecha", false));

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
            if (Grid1.SelectedItem is not CorreccionFila fila) return;

            string idSel = fila.Id;
            AppState.EventoFormularioC = "editar";
            var detalle = new CorreccionesDetalle(this, fila.Id) { Owner = Window.GetWindow(this) };
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
    public class CorreccionFila
    {
        public int      Linea         { get; set; }
        public string   Id            { get; set; } = "";
        public DateTime Fecha         { get; set; }
        public string   FechaStr      { get; set; } = "";
        public string   Movimiento    { get; set; } = "";
        public string   Motivo        { get; set; } = "";
        public double   CantidadTotal { get; set; }
    }
}
