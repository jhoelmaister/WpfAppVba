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
    /// Vista de PEDIDOS de toda la empresa, solo visualización (sin CRUD):
    /// filtro por sucursal (todas o una) y por año. Cada sucursal solo aporta
    /// documentos posteriores a su apertura (MAX documentosI.fecha) — ver
    /// ConsultasEmpresa.CargarDocsPedidos.
    /// </summary>
    public partial class PedidosVisor : UserControl
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

        public PedidosVisor()
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
        private int    AnioSeleccionado()     => CmbAnio.SelectedItem is int a ? a : DateTime.Now.Year;

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

                var anios = await Task.Run(() => ConsultasEmpresa.CargarAnios(emp));
                CmbAnio.ItemsSource = anios;
                int idx = anios.IndexOf(DateTime.Now.Year);
                CmbAnio.SelectedIndex = idx >= 0 ? idx : (anios.Count > 0 ? 0 : -1);
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
                _docs = await Task.Run(() => ConsultasEmpresa.CargarDocsPedidos(emp, anio, suc));
                Grid1.ItemsSource = _docs;
                Grid2.ItemsSource = null;
                LblLineas.Text    = "Líneas del documento seleccionado";

                double unidades = _docs.Sum(d => d.Cantidad);
                double importe  = _docs.Sum(d => d.Importe);
                LblResumen.Text =
                    $"{_docs.Count:N0} documentos · {unidades:N0} unidades · importe {importe:#,##0.##}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudieron cargar los pedidos:\n{ex.Message}",
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
                var lineas = await Task.Run(() => ConsultasEmpresa.CargarLineasPedido(fila.Id));
                Grid2.ItemsSource = lineas;
                LblLineas.Text    = $"Líneas de {fila.Codigo} — {fila.Sucursal}";
            }
            catch
            {
                Grid2.ItemsSource = null;   // sin conexión: la grilla queda vacía, sin interrumpir
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
