using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class LoginWindow : Window
    {
        private static SqlData Sql => SqlData.Instance;

        public LoginWindow()
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            Loaded += LoginWindow_Loaded;
        }

        // ─── Al abrir: verificar config y cargar catálogos base ──────────────
        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BtnIngresar.IsEnabled = false;

            if (!ConexionConfig.HayConfiguracion())
            {
                MostrarEstado("Configure la conexión a base de datos.", Colors.Orange);
                var dlg = new ConfiguracionDbWindow { Owner = this };
                if (dlg.ShowDialog() != true)
                {
                    Application.Current.Shutdown();
                    return;
                }
                // ConfiguracionDbWindow ya configuró DatabaseConnection al guardar
            }
            else
            {
                DatabaseConnection.CargarDesdeConfiguracion();
            }

            await ConectarBaseDatosAsync();
        }

        private async Task ConectarBaseDatosAsync()
        {
            BtnIngresar.IsEnabled = false;
            MostrarEstado("Conectando a base de datos...", Colors.Gray);
            try
            {
                await Task.Run(() => AppLoader.ConectarProductos());
                MostrarEstado("", Colors.Gray);
                BtnIngresar.IsEnabled = true;
                TxtCuenta.Focus();
            }
            catch (Exception ex)
            {
                MostrarEstado("Sin conexión: " + ex.Message, Colors.Red);
            }
        }

        // ─── Enter en contraseña dispara el login ──────────────────────────────
        private void TxtContrasena_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && BtnIngresar.IsEnabled)
                BtnIngresar_Click(sender, e);
        }

        // ─── Configurar conexión desde el login ───────────────────────────────
        private async void BtnConfigurarConexion_Click(object sender, RoutedEventArgs e)
        {
            // Abrir en modo edición si ya hay un servidor activo, para que al guardar
            // se actualice el servidor existente y DatabaseConnection quede reconfigurado.
            string activoId = ConexionConfig.ObtenerActivoId();
            ServidorConexion? servidorActivo = string.IsNullOrEmpty(activoId)
                ? null
                : ConexionConfig.ObtenerPorId(activoId);

            var dlg = new ConfiguracionDbWindow(servidorActivo) { Owner = this };
            if (dlg.ShowDialog() == true)
                await ConectarBaseDatosAsync();
        }

        // ─── Lógica de inicio de sesión (equivalente a CommandButton1_Click) ──
        private async void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string cuenta     = TxtCuenta.Text.Trim();
            string contrasena = TxtContrasena.Password;

            if (string.IsNullOrEmpty(cuenta) || string.IsNullOrEmpty(contrasena))
            {
                MostrarEstado("Completa usuario y contraseña", Colors.Orange);
                return;
            }

            BtnIngresar.IsEnabled   = false;
            TxtCuenta.IsEnabled     = false;
            TxtContrasena.IsEnabled = false;

            MostrarEstado("Verificando credenciales...", Colors.Green);

            string idEncontrado = "";
            bool encontrado = false;

            await Task.Run(() =>
            {
                int uf = Sql.UsuariosObj.ContarFilas;
                for (int ciclo = 1; ciclo <= uf; ciclo++)
                {
                    var idObj = Sql.UsuariosObj.Mover(ciclo);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;

                    string cuentaDb    = Sql.UsuariosObj.ObtenerItem("cuenta", id)?.ToString() ?? "";
                    string contrasenaDb = Sql.UsuariosObj.ObtenerItem("llave",  id)?.ToString() ?? "";

                    if (cuentaDb == cuenta && contrasenaDb == contrasena)
                    {
                        idEncontrado = id;
                        encontrado   = true;
                        break;
                    }
                }
            });

            if (encontrado)
            {
                AppState.UsuarioActivo  = Convert.ToInt64(idEncontrado);
                AppState.SucursalActiva = Convert.ToInt64(
                    Sql.UsuariosObj.ObtenerItem("sucursal", idEncontrado) ?? 0);
                AppState.RegionActiva   = Convert.ToInt64(
                    Sql.SucursalesObj.ObtenerItem("region", AppState.SucursalActiva.ToString()) ?? 0);
                AppState.SesionActiva   = true;
                AppState.PeriodoActivo  = DateTime.Now.Year.ToString();

                string temaUsuario = Sql.UsuariosObj.ObtenerItem("temaC", idEncontrado)?.ToString() ?? "";
                AppState.TemaActivo = temaUsuario.Trim().ToLowerInvariant() == ThemeManager.TemaOscuro
                    ? ThemeManager.TemaOscuro
                    : ThemeManager.TemaClaro;
                ThemeManager.AplicarTema(AppState.TemaActivo);

                MostrarEstado("Conectando a base de datos principal...", Colors.Green);
                await Task.Run(() => AppLoader.ConectarBases());

                MostrarEstado($"Actualizando datos del período {AppState.PeriodoActivo}...", Colors.Green);
                await Task.Run(() => AppState.ActualizarBase(DateTime.Now.Year));

                MostrarEstado("Cargando documentos...", Colors.Green);
                await Task.Run(() => AppLoader.ConectarDocumentos(
                    AppState.DataFechaInicio, AppState.DataFechaFinal));

                MostrarEstado("¡Conexión exitosa!", Colors.Green);

                var main = new ConsolaMovimientos();
                main.Show();
                Close();
            }
            else
            {
                MostrarEstado("Cuenta o contraseña incorrecta", Colors.Red);
                BtnIngresar.IsEnabled   = true;
                TxtCuenta.IsEnabled     = true;
                TxtContrasena.IsEnabled = true;
            }
        }

        private void MostrarEstado(string mensaje, Color color)
        {
            LblEstado.Text       = mensaje;
            LblEstado.Foreground = new SolidColorBrush(color);
        }
    }
}
