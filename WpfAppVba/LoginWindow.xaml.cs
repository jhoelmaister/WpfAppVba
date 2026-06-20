using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private readonly ActualizadorApp _actualizador = new();

        public LoginWindow()
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            Loaded += LoginWindow_Loaded;
        }

        // ─── Al abrir: si hay actualización pendiente, bloquear el login hasta
        //     que se actualice; si no, verificar config y cargar catálogos base ──
        private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            HabilitarControles(false);

            try
            {
                if (await _actualizador.HayActualizacionAsync())
                {
                    MostrarBloqueActualizacionObligatoria();
                    return; // no continuar con el login hasta que se actualice
                }
            }
            catch
            {
                // Sin red o sin feed accesible: no bloquear el login por esto solo.
            }

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
            HabilitarControles(false);
            MostrarEstado("Conectando a base de datos...", Colors.Gray);

            bool conectado;
            try
            {
                // Antes de loguear NO se descarga ninguna tabla (ni siquiera 'usuarios':
                // contiene las contraseñas de todas las cuentas). Solo se verifica que
                // el servidor responda; el login se valida con una consulta puntual.
                conectado = await Task.Run(() => DatabaseConnection.ConexionEstaActiva());
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
                HabilitarControles(true);
                TxtCuenta.Focus();
            }
            else
            {
                // Mensaje simple (sin volcar el error técnico de SQL Server).
                MostrarEstado("⚠ Sin conexión. Reintentando…", Colors.Orange);
                HabilitarControles(false);
                ProgramarReintento();
            }
        }

        // Habilita/deshabilita TODOS los controles de credenciales a la vez (incluido
        // el botón del ojo y la caja de texto visible) para que nada quede editable
        // mientras se conecta o se valida el inicio de sesión.
        private void HabilitarControles(bool habilitado)
        {
            BtnIngresar.IsEnabled          = habilitado;
            TxtCuenta.IsEnabled            = habilitado;
            TxtContrasena.IsEnabled        = habilitado;
            TxtContrasenaVisible.IsEnabled = habilitado;
            BtnVerContrasena.IsEnabled     = habilitado;
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

        // ─── Actualización obligatoria antes de loguear (Velopack) ────────────
        private void MostrarBloqueActualizacionObligatoria()
        {
            PanelLogin.Visibility       = Visibility.Collapsed;
            BloqueActualizar.Visibility = Visibility.Visible;
            LblVersionNueva.Text        =
                $"Hay una nueva versión disponible ({_actualizador.VersionNueva}). " +
                "Debes actualizar para continuar.";
        }

        // Estado A → B: el usuario pulsa "Actualizar". Descarga en segundo plano con barra.
        private async void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            BtnActualizar.Visibility = Visibility.Collapsed;
            PanelDescarga.Visibility = Visibility.Visible;
            LblDescarga.Text         = "Descargando…";
            BarraDescarga.Value      = 0;

            double totalMB = _actualizador.TamañoDescargaMB;

            var progreso = new Progress<int>(p =>
            {
                BarraDescarga.Value = p;
                double bajadoMB = totalMB * p / 100.0;
                LblDescarga.Text = totalMB > 0
                    ? $"Descargando… {bajadoMB:0.0} / {totalMB:0.0} MB ({p}%)"
                    : $"Descargando… {p}%";
            });

            try
            {
                await _actualizador.DescargarAsync(progreso);
                // Estado B → C: lista. El usuario decide cuándo reiniciar.
                PanelDescarga.Visibility = Visibility.Collapsed;
                BtnReiniciar.Visibility  = Visibility.Visible;
            }
            catch
            {
                // Falló la descarga: volver al estado A para poder reintentar.
                PanelDescarga.Visibility = Visibility.Collapsed;
                BtnActualizar.Visibility = Visibility.Visible;
                MessageBox.Show(
                    "No se pudo descargar la actualización. Revisa tu conexión e inténtalo de nuevo.",
                    "Actualización", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Estado C: aplica lo descargado y reinicia la app ya actualizada.
        private void BtnReiniciar_Click(object sender, RoutedEventArgs e)
        {
            _actualizador.AplicarYReiniciar();
        }

        // ─── Enter en contraseña dispara el login ──────────────────────────────
        private void TxtContrasena_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && BtnIngresar.IsEnabled)
                BtnIngresar_Click(sender, e);
        }

        // ─── Mostrar / ocultar contraseña ──────────────────────────────────────
        private void BtnVerContrasena_Click(object sender, RoutedEventArgs e)
        {
            // Bloqueado mientras se conecta/valida: no alternar ni reactivar el campo.
            if (!BtnVerContrasena.IsEnabled) return;

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
                // Mostrar -> Ocultar: devolver el valor al PasswordBox, conservando la
                // posición del cursor que tenía en el cuadro de texto visible.
                int caret = TxtContrasenaVisible.CaretIndex;
                TxtContrasena.Password          = TxtContrasenaVisible.Text;
                TxtContrasenaVisible.Visibility = Visibility.Collapsed;
                TxtContrasena.Visibility        = Visibility.Visible;
                IcoVerContrasena.Text           = "\uE7B3";   // Segoe MDL2: RedEye
                BtnVerContrasena.ToolTip        = "Mostrar contraseña";
                TxtContrasena.Focus();
                PosicionarCursorPassword(TxtContrasena, caret);
            }
        }

        // El PasswordBox no expone CaretIndex público; se usa su método interno
        // Select(start, length) por reflexión para colocar el cursor donde estaba.
        private static void PosicionarCursorPassword(PasswordBox pb, int index)
        {
            try
            {
                int pos = Math.Max(0, Math.Min(index, pb.Password.Length));
                var select = typeof(PasswordBox).GetMethod("Select",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                select?.Invoke(pb, new object[] { pos, 0 });
            }
            catch { /* si la API interna cambia, queda el foco normal como respaldo */ }
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

            HabilitarControles(false);

            MostrarEstado("Verificando credenciales...", Colors.Green);

            string idEncontrado = "";
            bool encontrado = false;

            try
            {
                await Task.Run(() => idEncontrado = AppLoader.ValidarLogin(cuenta, contrasena));
                encontrado = !string.IsNullOrEmpty(idEncontrado);
            }
            catch
            {
                // Sin conexión al validar: tratar como credenciales no verificadas.
                encontrado = false;
            }

            if (encontrado)
            {
                try
                {
                    // Recién autenticado: ahora sí se cargan los catálogos de usuarios
                    // y empresas (ya no hace falta ocultarlos de un usuario sin loguear).
                    MostrarEstado("Cargando datos de la cuenta...", Colors.Green);
                    await Task.Run(() => AppLoader.ConectarUsuarios());

                    AppState.UsuarioActivo  = idEncontrado;
                    AppState.TipoUsuario    = Sql.UsuariosObj.ObtenerItem("tipo",     idEncontrado)?.ToString() ?? "";
                    AppState.EmpresaActiva  = Sql.UsuariosObj.ObtenerItem("empresa",  idEncontrado)?.ToString() ?? "";
                    AppState.SucursalActiva = Sql.UsuariosObj.ObtenerItem("sucursal", idEncontrado)?.ToString() ?? "";
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

                    // La región sale de la sucursal activa, ya disponible tras cargar
                    // sucursales en ConectarProductos.
                    AppState.RegionActiva = Sql.SucursalesObj.ObtenerItem("region", AppState.SucursalActiva)?.ToString() ?? "";

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
                catch (Exception ex)
                {
                    // Cancelar el inicio de sesión, avisar y desbloquear todo para
                    // reintentar. Se muestra el mensaje real: no toda falla acá es de
                    // conexión (puede ser un error de SQL real, p.ej. de esquema).
                    AppState.SesionActiva  = false;
                    AppState.UsuarioActivo = "";
                    MostrarEstado($"⚠ No se pudo cargar los datos: {ex.Message}", Colors.Orange);
                    HabilitarControles(true);
                }
            }
            else
            {
                MostrarEstado("Cuenta o contraseña incorrecta", Colors.Red);
                HabilitarControles(true);

                TxtContrasena.Clear();
                TxtContrasenaVisible.Clear();
                if (TxtContrasena.Visibility == Visibility.Visible)
                    TxtContrasena.Focus();
                else
                    TxtContrasenaVisible.Focus();
            }
        }

        private void MostrarEstado(string mensaje, Color color)
        {
            LblEstado.Text       = mensaje;
            LblEstado.Foreground = new SolidColorBrush(color);
        }
    }
}
