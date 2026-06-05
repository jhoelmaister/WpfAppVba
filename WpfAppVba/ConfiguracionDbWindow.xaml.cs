using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ConfiguracionDbWindow : Window
    {
        public ConfiguracionDbWindow()
        {
            InitializeComponent();
            CargarDatosGuardados();
        }

        private void CargarDatosGuardados()
        {
            var cfg = ConexionConfig.Cargar();
            if (cfg == null) return;
            TxtServidor.Text       = cfg.Value.servidor;
            TxtBaseDatos.Text      = cfg.Value.baseDatos;
            TxtUsuario.Text        = cfg.Value.usuario;
            PwdContrasena.Password = cfg.Value.contrasena;
        }

        private string ObtenerContrasena() =>
            TxtContrasenaVisible.Visibility == Visibility.Visible
                ? TxtContrasenaVisible.Text
                : PwdContrasena.Password;

        // ─── Mostrar / ocultar contraseña ────────────────────────────────────
        private void BtnTogglePwd_Click(object sender, RoutedEventArgs e)
        {
            if (PwdContrasena.Visibility == Visibility.Visible)
            {
                TxtContrasenaVisible.Text    = PwdContrasena.Password;
                PwdContrasena.Visibility     = Visibility.Collapsed;
                TxtContrasenaVisible.Visibility = Visibility.Visible;
                BtnTogglePwd.Content         = "Ocultar";
            }
            else
            {
                PwdContrasena.Password          = TxtContrasenaVisible.Text;
                TxtContrasenaVisible.Visibility = Visibility.Collapsed;
                PwdContrasena.Visibility        = Visibility.Visible;
                BtnTogglePwd.Content            = "Ver";
            }
        }

        // ─── Probar conexión ─────────────────────────────────────────────────
        private async void BtnProbarConexion_Click(object sender, RoutedEventArgs e)
        {
            string servidor   = TxtServidor.Text.Trim();
            string baseDatos  = TxtBaseDatos.Text.Trim();
            string usuario    = TxtUsuario.Text.Trim();
            string contrasena = ObtenerContrasena();

            if (string.IsNullOrEmpty(servidor) || string.IsNullOrEmpty(baseDatos))
            {
                MessageBox.Show("Completa al menos Servidor y Base de datos.", "Probar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnProbarConexion.IsEnabled = false;
            BtnGuardar.IsEnabled        = false;
            try
            {
                string cs = $"Server={servidor};Database={baseDatos};User Id={usuario};Password={contrasena};" +
                            "Connect Timeout=10;TrustServerCertificate=True;";
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(cs);
                    conn.Open();
                    using var cmd = new SqlCommand("SELECT 1", conn);
                    cmd.ExecuteScalar();
                });
                MessageBox.Show("Conexión exitosa.", "Probar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al conectar:\n{ex.Message}", "Probar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnProbarConexion.IsEnabled = true;
                BtnGuardar.IsEnabled        = true;
            }
        }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string servidor   = TxtServidor.Text.Trim();
            string baseDatos  = TxtBaseDatos.Text.Trim();
            string usuario    = TxtUsuario.Text.Trim();
            string contrasena = ObtenerContrasena();

            if (string.IsNullOrEmpty(servidor) || string.IsNullOrEmpty(baseDatos))
            {
                MessageBox.Show("Completa al menos Servidor y Base de datos.", "Guardar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ConexionConfig.Guardar(servidor, baseDatos, usuario, contrasena);
                DatabaseConnection.Configurar(servidor, baseDatos, usuario, contrasena);
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
