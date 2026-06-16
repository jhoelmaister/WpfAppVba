using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class LoginWindow : Window
    {
        private static SqlData Sql => SqlData.Instance;

        // Reintenta la conexión automáticamente mientras no haya internet.
        private DispatcherTimer? _reintentoTimer;

        public LoginWindow()
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            Loaded += LoginWindow_Loaded;
        }

        // ─── Al abrir: verificar config y cargar catálogos base ──────────────
        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BtnIngresar.IsEnabled   = false;
            TxtCuenta.IsEnabled     = false;
            TxtContrasena.IsEnabled = false;

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
            BtnIngresar.IsEnabled   = false;
            TxtCuenta.IsEnabled     = false;
            TxtContrasena.IsEnabled = false;
            MostrarEstado("Conectando a base de datos...", Colors.Gray);

            bool conectado;
            try
            {
                await Task.Run(() => AppLoader.ConectarProductos());
                conectado = true;
            }
            catch
            {
                conectado = false;
            }

            if (conectado)
            {
                // Conexión recuperada: limpiar aviso y habilitar el login.
                DetenerReintentos();
                MostrarEstado("", Colors.Gray);
                BtnIngresar.IsEnabled   = true;
                TxtCuenta.IsEnabled     = true;
                TxtContrasena.IsEnabled = true;
                TxtCuenta.Focus();
            }
            else
            {
                // Mensaje simple (sin volcar el error técnico de SQL Server).
                MostrarEstado("⚠ Sin conexión. Reintentando…", Colors.Orange);
                BtnIngresar.IsEnabled   = false;
                TxtCuenta.IsEnabled     = false;
                TxtContrasena.IsEnabled = false;
                ProgramarReintento();
            }
        }

        // ─── Auto-reconexión: reintenta cada 4 s hasta que vuelva el internet ─────
        private void ProgramarReintento()
        {
            if (_reintentoTimer == null)
            {
                _reintentoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                _reintentoTimer.Tick += async (_, _) =>
                {
                    _reintentoTimer!.Stop();                 // evita solapar intentos
                    await ConectarBaseDatosAsync();          // reprograma solo si sigue offline
                };
            }
            _reintentoTimer.Start();
        }

        private void DetenerReintentos() => _reintentoTimer?.Stop();

        // ─── Enter en contraseña dispara el login ──────────────────────────────
        private void TxtContrasena_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && BtnIngresar.IsEnabled)
                BtnIngresar_Click(sender, e);
        }

        // ─── Mostrar / ocultar contraseña ──────────────────────────────────────
        private void BtnVerContrasena_Click(object sender, RoutedEventArgs e)
        {
            if (TxtContrasena.Visibility == Visibility.Visible)
            {
                // Ocultar -> Mostrar: pasar el valor al TextBox visible.
                TxtContrasenaVisible.Text       = TxtContrasena.Password;
                TxtContrasena.Visibility        = Visibility.Collapsed;
                TxtContrasenaVisible.Visibility = Visibility.Visible;
                IcoVerContrasena.Text           = "\uED1A";   // Segoe MDL2: Hide
                BtnVerContrasena.ToolTip        = "Ocultar contraseña";
                TxtContrasenaVisible.Focus();
                TxtContrasenaVisible.CaretIndex = TxtContrasenaVisible.Text.Length;
            }
            else
            {
                // Mostrar -> Ocultar: devolver el valor al PasswordBox.
                TxtContrasena.Password          = TxtContrasenaVisible.Text;
                TxtContrasenaVisible.Visibility = Visibility.Collapsed;
                TxtContrasena.Visibility        = Visibility.Visible;
                IcoVerContrasena.Text           = "\uE7B3";   // Segoe MDL2: RedEye
                BtnVerContrasena.ToolTip        = "Mostrar contraseña";
                TxtContrasena.Focus();
            }
        }

        // ─── Configurar conexión desde el login ───────────────────────────────
        private async void BtnConfigurarConexion_Click(object sender, RoutedEventArgs e)
        {
            // Abrir el listado de servidores para agregar / editar / conectar.
            var dlg = new ConexionServidoresWindow { Owner = this };
            dlg.ShowDialog();

            // Tras gestionar los servidores, recargar la conexión activa y reconectar.
            if (ConexionConfig.HayConfiguracion())
            {
                DatabaseConnection.CargarDesdeConfiguracion();
                await ConectarBaseDatosAsync();
            }
        }

        // ─── Lógica de inicio de sesión (equivalente a CommandButton1_Click) ──
        private async void BtnIngresar_Click(object sender, RoutedEventArgs e)
        {
            string cuenta     = TxtCuenta.Text.Trim();
            string contrasena = TxtContrasena.Visibility == Visibility.Visible
                ? TxtContrasena.Password
                : TxtContrasenaVisible.Text;

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
                AppState.UsuarioActivo  = idEncontrado;
                AppState.TipoUsuario    = Sql.UsuariosObj.ObtenerItem("tipo",     idEncontrado)?.ToString() ?? "";
                AppState.EmpresaActiva  = Sql.UsuariosObj.ObtenerItem("empresa",  idEncontrado)?.ToString() ?? "";
                AppState.SucursalActiva = Sql.UsuariosObj.ObtenerItem("sucursal", idEncontrado)?.ToString() ?? "";
                AppState.RegionActiva   = Sql.SucursalesObj.ObtenerItem("region", AppState.SucursalActiva)?.ToString() ?? "";
                AppState.SesionActiva   = true;
                AppState.PeriodoActivo  = DateTime.Now.Year.ToString();

                Sql.UsuariosObj.EstablecerItem("estadoU", idEncontrado, "activo");
                Sql.UsuariosObj.ExportarItems();

                string temaUsuario = Sql.UsuariosObj.ObtenerItem("temaC", idEncontrado)?.ToString() ?? "";
                AppState.TemaActivo = temaUsuario.Trim().ToLowerInvariant() == ThemeManager.TemaOscuro
                    ? ThemeManager.TemaOscuro
                    : ThemeManager.TemaClaro;
                ThemeManager.AplicarTema(AppState.TemaActivo);

                // Recargar catálogos ya filtrados por la empresa del usuario.
                MostrarEstado("Cargando catálogos de la empresa...", Colors.Green);
                await Task.Run(() => AppLoader.ConectarProductos());

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
