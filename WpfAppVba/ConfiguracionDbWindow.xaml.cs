using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using WpfAppVba.Data;

namespace WpfAppVba
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
                // Modo edición: NO se exponen las credenciales (usuario / contraseña).
                LblTitulo.Text    = "Editar Servidor";
                LblHint.Text      = "Deja Usuario y Contraseña en blanco para conservar los actuales.";
                TxtNombre.Text    = editar.Nombre;
                TxtServidor.Text  = editar.Servidor;
                TxtBaseDatos.Text = editar.BaseDatos;
                // TxtUsuario y PwdContrasena quedan vacíos a propósito.
            }

            // Guardar deshabilitado hasta que la prueba sea exitosa.
            // Se suscribe DESPUÉS de poblar los campos para no disparar el reset en la carga inicial.
            BtnGuardar.IsEnabled = false;
            TxtServidor.TextChanged          += (_, _) => ResetearPrueba();
            TxtBaseDatos.TextChanged         += (_, _) => ResetearPrueba();
            TxtUsuario.TextChanged           += (_, _) => ResetearPrueba();
            PwdContrasena.PasswordChanged    += (_, _) => ResetearPrueba();
            TxtContrasenaVisible.TextChanged += (_, _) => ResetearPrueba();
        }

        private void ResetearPrueba()
        {
            _pruebaExitosa            = false;
            BtnGuardar.IsEnabled      = false;
            LblEstadoConexion.Text    = "";
        }

        // ─── Credenciales efectivas (en edición, vacío = conservar) ──────────
        private string UsuarioEfectivo()
        {
            string u = TxtUsuario.Text.Trim();
            if (string.IsNullOrEmpty(u) && _original != null) return _original.Usuario;
            return u;
        }

        private string ContrasenaEfectiva()
        {
            string c = ObtenerContrasena();
            if (string.IsNullOrEmpty(c) && _original != null) return _original.Contrasena;
            return c;
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
                TxtContrasenaVisible.Text        = PwdContrasena.Password;
                PwdContrasena.Visibility         = Visibility.Collapsed;
                TxtContrasenaVisible.Visibility  = Visibility.Visible;
                BtnTogglePwd.Content             = "Ocultar";
            }
            else
            {
                PwdContrasena.Password           = TxtContrasenaVisible.Text;
                TxtContrasenaVisible.Visibility  = Visibility.Collapsed;
                PwdContrasena.Visibility         = Visibility.Visible;
                BtnTogglePwd.Content             = "Ver";
            }
        }

        // ─── Probar conexión ─────────────────────────────────────────────────
        private async void BtnProbarConexion_Click(object sender, RoutedEventArgs e)
        {
            string servidor   = TxtServidor.Text.Trim();
            string baseDatos  = TxtBaseDatos.Text.Trim();
            string usuario    = UsuarioEfectivo();
            string contrasena = ContrasenaEfectiva();

            if (string.IsNullOrEmpty(servidor) || string.IsNullOrEmpty(baseDatos))
            {
                MessageBox.Show("Completa al menos Servidor y Base de datos.", "Probar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnProbarConexion.IsEnabled = false;
            BtnGuardar.IsEnabled        = false;
            LblEstadoConexion.Text      = "";
            try
            {
                string cs = $"Server={servidor};Database={baseDatos};User Id={usuario};Password={contrasena};" +
                            "Connect Timeout=10;TrustServerCertificate=True;";

                ResultadoValidacionEsquema? esquema = null;
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(cs);
                    conn.Open();
                    using var cmd = new SqlCommand("SELECT 1", conn);
                    cmd.ExecuteScalar();

                    esquema = EsquemaValidator.Validar(conn);
                });

                if (esquema != null && !esquema.EsCompatible)
                {
                    _pruebaExitosa                = false;
                    LblEstadoConexion.Text        = "Estructura de la base de datos incompatible";
                    LblEstadoConexion.Foreground  = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    MessageBox.Show(
                        "La base de datos conectó, pero su estructura no es compatible con la app:\n\n" +
                        EsquemaValidator.DescribirProblemas(esquema),
                        "Estructura incompatible", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    _pruebaExitosa                = true;
                    LblEstadoConexion.Text        = "Conexión exitosa";
                    LblEstadoConexion.Foreground  = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                }
            }
            catch
            {
                LblEstadoConexion.Text       = "No se pudo conectar";
                LblEstadoConexion.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
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

            string servidor  = TxtServidor.Text.Trim();
            string baseDatos = TxtBaseDatos.Text.Trim();

            if (string.IsNullOrEmpty(servidor) || string.IsNullOrEmpty(baseDatos))
            {
                MessageBox.Show("Completa al menos Servidor y Base de datos.", "Guardar conexión",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var s = new ServidorConexion
                {
                    Id         = _original?.Id ?? Guid.NewGuid().ToString("N"),
                    Nombre     = string.IsNullOrWhiteSpace(TxtNombre.Text) ? servidor : TxtNombre.Text.Trim(),
                    Servidor   = servidor,
                    BaseDatos  = baseDatos,
                    Usuario    = UsuarioEfectivo(),
                    Contrasena = ContrasenaEfectiva()
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

                // Reconfigurar la conexión global SOLO si el servidor guardado es el activo;
                // así, agregar o editar un servidor distinto al conectado solo lo registra.
                if (s.Id == ConexionConfig.ObtenerActivoId())
                    DatabaseConnection.Configurar(s.Servidor, s.BaseDatos, s.Usuario, s.Contrasena);

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
