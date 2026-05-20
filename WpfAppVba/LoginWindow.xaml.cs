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
            Loaded += LoginWindow_Loaded;
        }

        // ─── Al abrir: carga usuarios y catálogos base ────────────────────────
        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
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

        // ─── Lógica de inicio de sesión (equivalente a CommandButton1_Click) ──
        private async void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string cuenta    = TxtCuenta.Text.Trim();
            string contrasena = TxtContrasena.Password;

            if (string.IsNullOrEmpty(cuenta) || string.IsNullOrEmpty(contrasena))
            {
                MostrarEstado("Completa usuario y contraseña", Colors.Orange);
                return;
            }

            // Deshabilitar controles (equivalente a VBA)
            BtnIngresar.IsEnabled   = false;
            TxtCuenta.IsEnabled     = false;
            TxtContrasena.IsEnabled = false;

            MostrarEstado("Verificando credenciales...", Colors.Green);

            // ── Buscar usuario en caché ──────────────────────────────────────
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
                // ── Establecer estado de sesión (equivalente a VBA) ──────────
                AppState.UsuarioActivo  = Convert.ToInt64(idEncontrado);
                AppState.SucursalActiva = Convert.ToInt64(
                    Sql.UsuariosObj.ObtenerItem("sucursal", idEncontrado) ?? 0);
                AppState.RegionActiva   = Convert.ToInt64(
                    Sql.SucursalesObj.ObtenerItem("region", AppState.SucursalActiva.ToString()) ?? 0);
                AppState.SesionActiva   = true;
                AppState.PeriodoActivo  = DateTime.Now.Year.ToString();

                MostrarEstado("Conectando a base de datos principal...", Colors.Green);
                await Task.Run(() => AppLoader.ConectarBases());

                MostrarEstado($"Actualizando datos del período {AppState.PeriodoActivo}...", Colors.Green);
                await Task.Run(() => AppState.ActualizarBase(DateTime.Now.Year));

                MostrarEstado("Cargando documentos...", Colors.Green);
                await Task.Run(() => AppLoader.ConectarDocumentos(
                    AppState.DataFechaInicio, AppState.DataFechaFinal));

                MostrarEstado("¡Conexión exitosa!", Colors.Green);

                // ── Abrir ventana principal ───────────────────────────────────
                var main = new MainWindow();
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
