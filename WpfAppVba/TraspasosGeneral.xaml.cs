using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class TraspasosGeneral : Window
    {
        private static SqlData Sql => SqlData.Instance;
        private string _mesActivo = "";

        public TraspasosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { CargarMeses(); CargarTraspasos(); };
        }

        // ─── Carga el árbol de meses ──────────────────────────────────────────
        private void CargarMeses()
        {
            Tree1.Items.Clear();
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };
            foreach (var mes in meses)
            {
                var item = new TreeViewItem { Header = mes, Tag = mes };
                Tree1.Items.Add(item);
            }

            // Seleccionar mes actual
            int mesActual = DateTime.Now.Month - 1;
            if (Tree1.Items[mesActual] is TreeViewItem ti)
            {
                ti.IsSelected = true;
                _mesActivo = meses[mesActual];
            }
        }

        // ─── Carga la lista de traspasos ──────────────────────────────────────
        public void CargarTraspasos()
        {
            var lista = new List<TraspasoFila>();
            int linea = 1;
            double totalCant = 0;
            string filtroEstado = ObtenerFiltroEstado();
            string busqueda = TxtBuscar.Text.ToLower();
            string tipoMov = AppState.TipoMovimiento.ToLower();

            int uf = Sql.DocumentosTObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosTObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                // Filtrar por sucursal activa según tipo de movimiento
                string campo = tipoMov == "salida" ? "origen" : "destino";
                string campOtro = tipoMov == "salida" ? "destino" : "origen";

                string sucursal = Sql.DocumentosTObj.ObtenerItem(campo, id)?.ToString() ?? "";
                if (sucursal != AppState.SucursalActiva.ToString()) continue;

                // Filtro por mes
                if (!string.IsNullOrEmpty(_mesActivo))
                {
                    var fechaObj2 = Sql.DocumentosTObj.ObtenerItem("fecha", id);
                    if (fechaObj2 == null) continue;
                    DateTime fecha2 = Convert.ToDateTime(fechaObj2);
                    string mesDoc = ObtenerNombreMes(fecha2.Month);
                    if (!string.Equals(mesDoc, _mesActivo, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var fechaDocObj = Sql.DocumentosTObj.ObtenerItem("fecha", id);
                DateTime fechaDoc = fechaDocObj != null ? Convert.ToDateTime(fechaDocObj) : default;

                string otroSucId = Sql.DocumentosTObj.ObtenerItem(campOtro, id)?.ToString() ?? "";
                string otroSucDesc = Sql.SucursalesObj.ObtenerItem("descripcion", otroSucId)?.ToString() ?? otroSucId;

                string estado = Sql.DocumentosTObj.ObtenerItem("estado", id)?.ToString() ?? "";
                string emitido = Sql.DocumentosTObj.ObtenerItem("emitido", id)?.ToString() ?? "";
                if (emitido != AppState.SucursalActiva.ToString() && estado == "pendiente")
                    estado = "pendiente revisar";

                // Filtro por estado
                if (!string.IsNullOrEmpty(filtroEstado) &&
                    !string.Equals(estado, filtroEstado, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Filtro por búsqueda
                if (!string.IsNullOrEmpty(busqueda))
                    if (!id.Contains(busqueda) && !otroSucDesc.ToLower().Contains(busqueda))
                        continue;

                double cant = CalcularCantidad(id);
                lista.Add(new TraspasoFila
                {
                    Linea       = linea++,
                    DocumentoT  = id,
                    FechaStr    = fechaDoc.ToString("dd/MM/yyyy HH:mm:ss"),
                    SucursalDesc = otroSucDesc,
                    Estado      = estado,
                    Cantidad    = cant
                });
                totalCant += cant;
            }

            Grid1.ItemsSource = lista;
            TxtTotalCantidad.Text = totalCant.ToString("F0");
            LblTipoMovimiento.Text = tipoMov == "salida" ? "Salidas de Productos" : "Entradas de Productos";
        }

        // ─── Obtener filtro de estado seleccionado ────────────────────────────
        private string ObtenerFiltroEstado()
        {
            if (BtnFiltroPendiente.IsChecked == true) return "pendiente";
            if (BtnFiltroEntregado.IsChecked == true) return "entregado";
            return ""; // todos
        }

        // ─── Nombre de mes a partir de número ────────────────────────────────
        private static string ObtenerNombreMes(int mes)
        {
            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio",
                                "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };
            return mes >= 1 && mes <= 12 ? meses[mes - 1] : "";
        }

        // ─── Sumar cantidad de artículos del documentoT ───────────────────────
        private static double CalcularCantidad(string documentoT)
        {
            double cant = 0;
            int uf = Sql.TraspasosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.TraspasosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() != documentoT) continue;
                cant += Convert.ToDouble(Sql.TraspasosObj.ObtenerItem("cantidad", id) ?? 0);
            }
            return cant;
        }

        // ─── Eventos de árbol y filtros ───────────────────────────────────────
        private void Tree1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (Tree1.SelectedItem is TreeViewItem ti)
                _mesActivo = ti.Tag?.ToString() ?? "";
            CargarTraspasos();
        }

        private void FiltroEstado_Checked(object sender, RoutedEventArgs e)
            => CargarTraspasos();

        // ─── Búsqueda ─────────────────────────────────────────────────────────
        private void TxtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CargarTraspasos();
        }

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarTraspasos();

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
            => AbrirEditar();

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioM = "nuevo";
            new TraspasosDetalle(this).ShowDialog();
            CargarTraspasos();
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not TraspasoFila fila) return;

            var res = MessageBox.Show("¿Eliminar este traspaso y todos sus artículos?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                // Ocultar el documento
                Sql.DocumentosTObj.Ocultar(fila.DocumentoT);

                // Ocultar todas las líneas del traspaso
                int uf = Sql.TraspasosObj.ContarFilas;
                var idsOcultar = new List<string>();
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.TraspasosObj.Mover(i);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;
                    if (Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() == fila.DocumentoT)
                        idsOcultar.Add(id);
                }
                foreach (var id in idsOcultar)
                    Sql.TraspasosObj.Ocultar(id);

                Sql.DocumentosTObj.OrdenarData(("fecha", false));
                Sql.TraspasosObj.OrdenarData(("documentoT", false), ("indice", false));
                CargarTraspasos();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar: {ex.Message}", "Consola",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            Sql.DocumentosTObj.Actualizar();
            Sql.TraspasosObj.Actualizar();
            CargarTraspasos();
        }

        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not TraspasoFila fila) return;
            AppState.EventoFormularioM = "editar";
            new TraspasosDetalle(this, fila.DocumentoT).ShowDialog();
            CargarTraspasos();
        }
    }

    // ─── Modelo de fila para el DataGrid ─────────────────────────────────────
    public class TraspasoFila
    {
        public int    Linea        { get; set; }
        public string DocumentoT   { get; set; } = "";
        public string FechaStr     { get; set; } = "";
        public string SucursalDesc { get; set; } = "";
        public string Estado       { get; set; } = "";
        public double Cantidad     { get; set; }
    }
}
