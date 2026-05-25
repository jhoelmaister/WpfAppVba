using System.Windows;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class CambiarContrasena : Window
    {
        private static SqlData Sql => SqlData.Instance;

        public CambiarContrasena()
        {
            InitializeComponent();
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string usuId = AppState.UsuarioActivo.ToString();
            string llaveActualBD = Sql.UsuariosObj.ObtenerItem("llave", usuId)?.ToString() ?? "";

            if (PwdActual.Password != llaveActualBD)
            {
                MessageBox.Show("La contraseña actual es incorrecta.", "Cambiar Contraseña",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                PwdActual.Focus();
                return;
            }

            if (string.IsNullOrEmpty(PwdNueva.Password))
            {
                MessageBox.Show("La nueva contraseña no puede estar vacía.", "Cambiar Contraseña",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                PwdNueva.Focus();
                return;
            }

            Sql.UsuariosObj.EstablecerItem("llave", usuId, PwdNueva.Password);
            Sql.UsuariosObj.ExportarItems();

            MessageBox.Show("Contraseña actualizada correctamente.", "Cambiar Contraseña",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    }
}
