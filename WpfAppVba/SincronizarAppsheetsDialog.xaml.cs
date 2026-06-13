using System.Windows;

namespace WpfAppVba
{
    /// <summary>
    /// Diálogo de alcance para la sincronización de AppSheets.
    /// Expone la opción elegida por el usuario en <see cref="Opcion"/>.
    /// </summary>
    public partial class SincronizarAppsheetsDialog : Window
    {
        public enum OpcionSync
        {
            Cancelar,
            Todo,
            SucursalActiva
        }

        public OpcionSync Opcion { get; private set; } = OpcionSync.Cancelar;

        public SincronizarAppsheetsDialog()
        {
            InitializeComponent();
        }

        private void BtnTodo_Click(object sender, RoutedEventArgs e)
        {
            Opcion = OpcionSync.Todo;
            DialogResult = true;
        }

        private void BtnSucursal_Click(object sender, RoutedEventArgs e)
        {
            Opcion = OpcionSync.SucursalActiva;
            DialogResult = true;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Opcion = OpcionSync.Cancelar;
            DialogResult = false;
        }
    }
}
