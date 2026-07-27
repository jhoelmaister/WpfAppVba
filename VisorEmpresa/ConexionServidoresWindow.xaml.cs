using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VisorEmpresa.Data;

namespace VisorEmpresa
{
    /// <summary>
    /// Formulario de gestión de brokers de autenticación registrados (lista,
    /// agregar, editar, eliminar y conectar). Se abre desde el login mediante
    /// BtnConfigurarConexion.
    /// </summary>
    public partial class ConexionServidoresWindow : Window
    {
        private string? _lastSelectedServId;

        public ConexionServidoresWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => RefrescarServidores();
        }

        // Fila de la lista de servidores.
        private class ServidorVista
        {
            public string Id       { get; set; } = "";
            public string Nombre   { get; set; } = "";
            public string Servidor { get; set; } = "";
            public bool   EsActivo { get; set; }
            public string Activo   => EsActivo ? "●" : "";
        }

        private void RefrescarServidores()
        {
            string activo = ConexionConfig.ObtenerActivoId();
            var items = ConexionConfig.CargarLista()
                .Select(s => new ServidorVista
                {
                    Id       = s.Id,
                    Nombre   = s.Nombre,
                    Servidor = s.Servidor,
                    EsActivo = s.Id == activo
                })
                .ToList();
            LstServidores.ItemsSource = items;
        }

        private ServidorVista? ServidorSeleccionado() =>
            LstServidores.SelectedItem as ServidorVista;

        private ServidorVista? ServidorSeleccionadoOUltimo()
        {
            var sel = ServidorSeleccionado();
            if (sel == null && _lastSelectedServId != null)
                sel = (LstServidores.ItemsSource as List<ServidorVista>)
                          ?.FirstOrDefault(s => s.Id == _lastSelectedServId);
            return sel;
        }

        private void LstServidores_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LstServidores.SelectedItem is ServidorVista sv)
                _lastSelectedServId = sv.Id;
        }

        private void BtnAgregarServidor_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ConfiguracionDbWindow { Owner = this };
            if (dlg.ShowDialog() == true)
                RefrescarServidores();
        }

        private void LstServidores_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EditarServidorSeleccionado();
        }

        private void BtnEditarServidor_Click(object sender, RoutedEventArgs e)
        {
            EditarServidorSeleccionado();
        }

        private void EditarServidorSeleccionado()
        {
            var sel = ServidorSeleccionadoOUltimo();
            if (sel == null)
            {
                MessageBox.Show("Selecciona un servidor de la lista.", "Editar servidor",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var original = ConexionConfig.ObtenerPorId(sel.Id);
            if (original == null) { RefrescarServidores(); return; }

            var dlg = new ConfiguracionDbWindow(original) { Owner = this };
            if (dlg.ShowDialog() == true)
                RefrescarServidores();
        }

        private void BtnEliminarServidor_Click(object sender, RoutedEventArgs e)
        {
            var sel = ServidorSeleccionadoOUltimo();
            if (sel == null)
            {
                MessageBox.Show("Selecciona un servidor de la lista.", "Eliminar servidor",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var r = MessageBox.Show($"¿Eliminar el servidor \"{sel.Nombre}\"?", "Eliminar servidor",
                                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;

            ConexionConfig.Eliminar(sel.Id);
            RefrescarServidores();
        }

        private async void BtnConectarServidor_Click(object sender, RoutedEventArgs e)
        {
            var sel = ServidorSeleccionadoOUltimo();
            if (sel == null)
            {
                MessageBox.Show("Selecciona un servidor de la lista.", "Conectar",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var s = ConexionConfig.ObtenerPorId(sel.Id);
            if (s == null) { RefrescarServidores(); return; }

            // Antes de marcarlo activo, solo verifica que el broker responda — ya
            // no hay credenciales de SQL Server para probar acá: esas se validan
            // recién al loguear (ver LoginVisorWindow.BtnIngresar_Click).
            BtnConectarServidor.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                bool ok = await AuthBrokerClient.PingAsync(s.Servidor);
                if (!ok)
                {
                    MessageBox.Show($"No se pudo conectar al servidor \"{sel.Nombre}\".",
                                    "Conectar", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Marcar el servidor elegido como activo. El formulario permanece
                // abierto; el login reconecta al cerrarlo.
                ConexionConfig.EstablecerActivo(sel.Id);
                RefrescarServidores();

                MessageBox.Show($"Servidor activo establecido: \"{sel.Nombre}\".", "Conectar",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                BtnConectarServidor.IsEnabled = true;
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
