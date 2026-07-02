using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;   // AppState

namespace VisorEmpresa
{
    /// <summary>
    /// Vista de FACTURAS de toda la empresa, solo visualización: filtro por
    /// sucursal (todas o una) y por año, con corte por la apertura de cada
    /// sucursal — ver ConsultasEmpresa.CargarDocsFacturas.
    /// </summary>
    public partial class FacturasVisor : UserControl
    {
        private bool _iniciado;
        private bool _cargandoFiltros;
        private List<DocVisorFila> _docs = new();

        private class Opcion
        {
            public string Id    { get; }
            public string Texto { get; }
            public Opcion(string id, string texto) { Id = id; Texto = texto; }
            public override string ToString() => Texto;
        }

        public FacturasVisor()
        {
            InitializeComponent();
            Loaded += async (_, _) =>
            {
                if (_iniciado) return;
                _iniciado = true;
                await CargarFiltrosAsync();
            };
        }

        private string SucursalSeleccionada() => (CmbSucursal.SelectedItem as Opcion)?.Id ?? "";
        private int    AnioSeleccionado()     => VisorState.AnioActivo;   // año global (top bar de la consola)

        /// <summary>
        /// Recarga con los filtros globales actuales (empresa/año de la top bar).
        /// La llama la consola al cambiar el año; si el panel aún no se abrió,
        /// cargará solo con los valores vigentes en su primer Loaded.
        /// </summary>
        public async void RefrescarDatos()
        {
            if (!_iniciado) return;
            await CargarDatosAsync();
        }

        private async Task CargarFiltrosAsync()
        {
            _cargandoFiltros = true;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                string emp = AppState.EmpresaActiva;

                var sucursales = await Task.Run(() => ConsultasEmpresa.CargarSucursalesEmpresa(emp));
                var opciones = new List<Opcion> { new("", "Todas las sucursales") };
                opciones.AddRange(sucursales.Select(s => new Opcion(s.Id, s.Descripcion)));
                CmbSucursal.ItemsSource   = opciones;
                CmbSucursal.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar los filtros:\n{ex.Message}",
                                "Visor Empresa", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                _cargandoFiltros = false;
            }

            await CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            string emp  = AppState.EmpresaActiva;
            int    anio = AnioSeleccionado();
            string suc  = SucursalSeleccionada();

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                _docs = await Task.Run(() => ConsultasEmpresa.CargarDocsFacturas(emp, anio, suc));
                Grid1.ItemsSource = _docs;
                Grid2.ItemsSource = null;
                LblLineas.Text    = "Líneas del documento seleccionado";

                double importe = _docs.Sum(d => d.Importe);
                LblResumen.Text = $"{_docs.Count:N0} documentos · importe {importe:#,##0.##}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar las facturas:\n{ex.Message}",
                                "Visor Empresa", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void Grid1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Grid1.SelectedItem is not DocVisorFila fila)
            {
                Grid2.ItemsSource = null;
                return;
            }
            try
            {
                var lineas = await Task.Run(() => ConsultasEmpresa.CargarLineasFactura(fila.Id));
                Grid2.ItemsSource = lineas;
                LblLineas.Text    = $"Líneas de {fila.Codigo} — {fila.Sucursal}";
            }
            catch
            {
                Grid2.ItemsSource = null;
            }
        }

        private async void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            await CargarDatosAsync();
        }

        private async void Filtro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_cargandoFiltros || !_iniciado) return;
            await CargarDatosAsync();
        }
    }
}
