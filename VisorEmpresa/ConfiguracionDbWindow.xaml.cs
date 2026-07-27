using System;
using System.Windows;
using System.Windows.Media;
using VisorEmpresa.Data;

namespace VisorEmpresa
{
    public partial class ConfiguracionDbWindow : Window
    {
        private readonly ServidorConexion? _original;
        private bool _pruebaExitosa = false;

        /// <summary>Servidor resultante tras guardar (null si se canceló).</summary>
        public ServidorConexion? Resultado { get; private set; }

        public ConfiguracionDbWindow() : this(null) { }

        public ConfiguracionDbWindow(ServidorConexion? editar)
        {
            InitializeComponent();
            _original = editar;

            if (editar != null)
            {
                LblTitulo.Text   = "Editar servidor";
                TxtNombre.Text   = editar.Nombre;
                TxtServidor.Text = editar.Servidor;
            }

            // Guardar deshabilitado hasta que la prueba sea exitosa.
            BtnGuardar.IsEnabled   = false;
            TxtServidor.TextChanged += (_, _) => ResetearPrueba();
        }

        private void ResetearPrueba()
        {
            _pruebaExitosa         = false;
            BtnGuardar.IsEnabled   = false;
            LblEstadoConexion.Text = "";
        }

        // ─── Probar conexión: confirma que el broker (y la base detrás) responda ──
        private async void BtnProbarConexion_Click(object sender, RoutedEventArgs e)
        {
            string servidor = TxtServidor.Text.Trim();

            if (string.IsNullOrEmpty(servidor))
            {
                MessageBox.Show("Completa la dirección del servidor.", "Probar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnProbarConexion.IsEnabled = false;
            BtnGuardar.IsEnabled        = false;
            LblEstadoConexion.Text      = "";
            try
            {
                bool ok = await AuthBrokerClient.PingAsync(servidor);
                _pruebaExitosa = ok;

                if (ok)
                {
                    LblEstadoConexion.Text       = "Conexión exitosa";
                    LblEstadoConexion.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                }
                else
                {
                    LblEstadoConexion.Text       = "No se pudo conectar";
                    LblEstadoConexion.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                }
            }
            finally
            {
                BtnProbarConexion.IsEnabled = true;
                BtnGuardar.IsEnabled        = _pruebaExitosa;
            }
        }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!_pruebaExitosa)
            {
                MessageBox.Show("Debes probar la conexión exitosamente antes de guardar.",
                                "Guardar conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string servidor = TxtServidor.Text.Trim();
            if (string.IsNullOrEmpty(servidor))
            {
                MessageBox.Show("Completa la dirección del servidor.", "Guardar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var s = new ServidorConexion
                {
                    Id       = _original?.Id ?? Guid.NewGuid().ToString("N"),
                    Nombre   = string.IsNullOrWhiteSpace(TxtNombre.Text) ? servidor : TxtNombre.Text.Trim(),
                    Servidor = servidor
                };

                if (_original != null)
                {
                    ConexionConfig.Actualizar(s);
                }
                else
                {
                    // Solo se registra. Agregar() marca activo automáticamente
                    // únicamente cuando aún no hay ningún servidor activo (primer registro).
                    ConexionConfig.Agregar(s);
                }

                Resultado    = s;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}", "Guardar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Cancelar ────────────────────────────────────────────────────────
        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
