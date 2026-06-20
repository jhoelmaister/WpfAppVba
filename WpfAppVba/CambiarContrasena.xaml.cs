using System.Windows;
using System.Windows.Controls;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class CambiarContrasena : Window
    {
        private static SqlData Sql => SqlData.Instance;

        public CambiarContrasena()
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
        }

        // ─── Mostrar / ocultar contraseña ──────────────────────────────────────
        // Valor efectivo del campo (sea el PasswordBox o el TextBox visible).
        private static string Valor(PasswordBox pwd, TextBox txt) =>
            pwd.Visibility == Visibility.Visible ? pwd.Password : txt.Text;

        // Alterna entre oculto (PasswordBox) y visible (TextBox), conservando el valor.
        private static void Alternar(PasswordBox pwd, TextBox txt, TextBlock ico, Button btn)
        {
            if (pwd.Visibility == Visibility.Visible)
            {
                txt.Text       = pwd.Password;
                pwd.Visibility = Visibility.Collapsed;
                txt.Visibility = Visibility.Visible;
                ico.Text       = "\uED1A";          // Segoe MDL2: Hide
                btn.ToolTip    = "Ocultar contraseña";
                txt.Focus();
                txt.CaretIndex = txt.Text.Length;
            }
            else
            {
                pwd.Password   = txt.Text;
                txt.Visibility = Visibility.Collapsed;
                pwd.Visibility = Visibility.Visible;
                ico.Text       = "\uE7B3";          // Segoe MDL2: RedEye
                btn.ToolTip    = "Mostrar contraseña";
                pwd.Focus();
            }
        }

        private void BtnVerActual_Click(object sender, RoutedEventArgs e)    => Alternar(PwdActual, TxtActualVisible, IcoActual, BtnVerActual);
        private void BtnVerNueva_Click(object sender, RoutedEventArgs e)     => Alternar(PwdNueva, TxtNuevaVisible, IcoNueva, BtnVerNueva);
        private void BtnVerConfirmar_Click(object sender, RoutedEventArgs e) => Alternar(PwdConfirmar, TxtConfirmarVisible, IcoConfirmar, BtnVerConfirmar);

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            // Verificación de conexión en 2 capas (label + chequeo real) antes de guardar.
            if (!FuncionesComunes.VerificarConexionParaGuardar(this)) return;

            string usuId = AppState.UsuarioActivo.ToString();
            string llaveActualBD = Sql.UsuariosObj.ObtenerItem("llave", usuId)?.ToString() ?? "";

            // Valor efectivo de cada campo (mostrado u oculto).
            string actual    = Valor(PwdActual, TxtActualVisible);
            string nueva     = Valor(PwdNueva, TxtNuevaVisible);
            string confirmar = Valor(PwdConfirmar, TxtConfirmarVisible);

            bool actualValida = PasswordHasher.Verificar(actual, llaveActualBD)
                || (!PasswordHasher.EsHash(llaveActualBD) && actual == llaveActualBD);

            if (!actualValida)
            {
                MessageBox.Show("La contraseña actual es incorrecta.", "Cambiar Contraseña",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                PwdActual.Focus();
                return;
            }

            if (string.IsNullOrEmpty(nueva))
            {
                MessageBox.Show("La nueva contraseña no puede estar vacía.", "Cambiar Contraseña",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                PwdNueva.Focus();
                return;
            }

            if (nueva != confirmar)
            {
                MessageBox.Show("La nueva contraseña y su confirmación no coinciden.", "Cambiar Contraseña",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                PwdConfirmar.Focus();
                return;
            }

            Sql.UsuariosObj.EstablecerItem("llave", usuId, PasswordHasher.Hashear(nueva));
            Sql.UsuariosObj.ExportarItems();

            MessageBox.Show("Contraseña actualizada correctamente.", "Cambiar Contraseña",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    }
}
