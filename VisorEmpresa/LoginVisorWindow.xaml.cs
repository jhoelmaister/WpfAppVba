using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SistemaGestion;        // ConsolaMovimientos (nombre de clase de ConsolaVisor.xaml.cs)
using VisorEmpresa.Data;   // DatabaseConnection, ConexionConfig

namespace VisorEmpresa
{
    /// <summary>
    /// Login del visor. Mismo flujo que el de la app principal (configuración de
    /// conexión compartida, sonda con auto-reintento, validación puntual), con una
    /// diferencia clave: solo aceptan usuarios de tipo "admin" — el visor muestra
    /// datos de TODA la empresa y los usuarios comunes están acotados por sucursal.
    /// </summary>
    public partial class LoginVisorWindow : Window
    {
        // Reintenta la conexión automáticamente mientras no haya internet.
        private DispatcherTimer? _reintentoTimer;

        private readonly ActualizadorApp _actualizador = new();

        public LoginVisorWindow()
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            Loaded += LoginVisorWindow_Loaded;
        }

        // ─── Al abrir: si hay actualización pendiente, bloquear el login hasta
        //     que se actualice; si no, verificar config y sondear la base ──────
        private async void LoginVisorWindow_Loaded(object sender, RoutedEventArgs e)
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
                // Antes de loguear NO se descarga ninguna tabla; solo se verifica
                // que el servidor responda (el login se valida con consulta puntual).
                conectado = await Task.Run(() => DatabaseConnection.ConexionEstaActiva());
            }
            catch
            {
                conectado = false;
            }

            if (conectado)
            {
                DetenerReintentos();
                MostrarEstado("", Colors.Gray);
                HabilitarControles(true);
                TxtCuenta.Focus();
            }
            else
            {
                MostrarEstado("⚠ Sin conexión. Reintentando…", Colors.Orange);
                HabilitarControles(false);
                ProgramarReintento();
            }
        }

        // Habilita/deshabilita TODOS los controles de credenciales a la vez.
        private void HabilitarControles(bool habilitado)
        {
            BtnIngresar.IsEnabled           = habilitado;
            TxtCuenta.IsEnabled             = habilitado;
            TxtContrasena.IsEnabled         = habilitado;
            TxtContrasenaVisible.IsEnabled  = habilitado;
            BtnVerContrasena.IsEnabled      = habilitado;
            BtnConfigurarConexion.IsEnabled = habilitado;
        }

        // ─── Auto-reconexión: reintenta cada 4 s hasta que vuelva el internet ─
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

        // ─── Enter en contraseña dispara el login ─────────────────────────────
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
            // Abrir el listado de servidores para agregar / editar / conectar
            // (misma configuración cifrada que la app principal).
            var dlg = new ConexionServidoresWindow { Owner = this };
            dlg.ShowDialog();

            // Tras gestionar los servidores, recargar la conexión activa y reconectar.
            if (ConexionConfig.HayConfiguracion())
            {
                DatabaseConnection.CargarDesdeConfiguracion();
                await ConectarBaseDatosAsync();
            }
        }

        // ─── Lógica de inicio de sesión ────────────────────────────────────────
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

            UsuarioVisor? usuario = null;
            try
            {
                usuario = await Task.Run(() => ConsultasEmpresa.ValidarLogin(cuenta, contrasena));
            }
            catch
            {
                // Sin conexión al validar: tratar como credenciales no verificadas.
                usuario = null;
            }

            if (usuario == null)
            {
                MostrarEstado("Cuenta o contraseña incorrecta", Colors.Red);
                HabilitarControles(true);
                LimpiarContrasena();
                return;
            }

            // Gating por rol: el visor muestra la empresa completa, así que solo
            // entran administradores (los usuarios comunes operan por sucursal).
            if (usuario.Tipo.Trim().ToLowerInvariant() != "admin")
            {
                MostrarEstado("Acceso solo para administradores.", Colors.Red);
                HabilitarControles(true);
                LimpiarContrasena();
                return;
            }

            try
            {
                VisorState.UsuarioActivo = usuario.Id;
                VisorState.TipoUsuario   = usuario.Tipo.Trim().ToLowerInvariant();
                VisorState.EmpresaActiva = usuario.Empresa;

                // Estado global compartido con los formularios vinculados de la app
                // principal (Precios/Empresas/Sucursales/Usuarios leen AppState).
                AppState.UsuarioActivo = usuario.Id;
                AppState.TipoUsuario   = VisorState.TipoUsuario;
                AppState.EmpresaActiva = usuario.Empresa;
                AppState.SesionActiva  = true;
                AppState.PeriodoActivo = DateTime.Now.Year.ToString();
                // Sin sucursal activa: el visor trabaja a nivel de empresa completa.
                AppState.SucursalActiva = "";
                AppState.RegionActiva   = "";

                // Tema: el visor es independiente de la app principal, no lee ni
                // aplica usuarios.temaC. App.xaml.cs ya aplicó el preferido LOCAL de
                // esta PC (TemaVisor.CargarTemaLocal()) antes de mostrar el login;
                // solo se sincroniza AppState para los formularios vinculados.
                AppState.TemaActivo = VisorState.TemaActivo;

                // Cachés que usan los módulos de edición vinculados. Todas son
                // empresa-scoped (AppLoader NO filtra por sucursal aquí).
                MostrarEstado("Cargando datos de la cuenta...", Colors.Green);
                await Task.Run(() => AppLoader.ConectarUsuarios());

                MostrarEstado("Cargando catálogos de la empresa...", Colors.Green);
                await Task.Run(() => AppLoader.ConectarProductos());

                // Precalienta la caché de stock/disponible (ConsultasEmpresa.ObtenerStockEmpresa),
                // que de paso puebla ConectarCacheDashboardEmpresa (pedidos/traspasos/
                // correcciones/aperturas de TODA la empresa, sin filtro de sucursal ni
                // período): sin esto, esas consultas se disparaban recién al abrir la
                // primera pestaña (Artículos/Precios/Dashboard), sintiéndose como una
                // demora "sin motivo" con latencia de red alta. Al precalentarlas acá, esa
                // demora queda en el login (donde ya se muestra progreso) y las pestañas
                // (incluido el Dashboard: CargarMovimientos/CargarResumenPedidos/
                // CargarTraspasosInternos) abren instantáneas después.
                MostrarEstado("Calculando stock de la empresa...", Colors.Green);
                await Task.Run(() => ConsultasEmpresa.ObtenerStockEmpresa(usuario.Empresa));

                // Precalienta también Pedidos/Traspasos/Correcciones/FacturasGeneral —
                // TODA la empresa (sin filtro de sucursal), para el año activo. Estas
                // pantallas vuelven a llamar a los mismos ConectarCacheXxx al abrirse
                // (para quedar correctas si el usuario entra directo sin pasar por acá),
                // pero como ya quedó cargado con la misma clave (empresa, año), esa
                // segunda llamada es un no-op (ver memoización en ConsultasEmpresa) y la
                // pantalla arma la grilla al instante filtrando en memoria — el combo de
                // sucursal de cada pantalla ya no dispara una consulta SQL nueva.
                MostrarEstado("Cargando documentos de la empresa...", Colors.Green);
                int añoActivo = VisorState.AnioActivo;
                await Task.Run(() =>
                {
                    ConsultasEmpresa.ConectarCachePedidos(usuario.Empresa, añoActivo);
                    ConsultasEmpresa.ConectarCacheTraspasos(usuario.Empresa, añoActivo);
                    ConsultasEmpresa.ConectarCacheCorrecciones(usuario.Empresa, añoActivo);
                    ConsultasEmpresa.ConectarCacheFacturas(usuario.Empresa, añoActivo);
                });

                var main = new ConsolaMovimientos();   // la consola del visor (ConsolaVisor.xaml)
                main.Show();
                Close();
            }
            catch (Exception ex)
            {
                AppState.SesionActiva  = false;
                AppState.UsuarioActivo = "";
                MostrarEstado($"⚠ No se pudo cargar los datos: {ex.Message}", Colors.Orange);
                HabilitarControles(true);
            }
        }

        private void LimpiarContrasena()
        {
            TxtContrasena.Clear();
            TxtContrasenaVisible.Clear();
            if (TxtContrasena.Visibility == Visibility.Visible)
                TxtContrasena.Focus();
            else
                TxtContrasenaVisible.Focus();
        }

        private void MostrarEstado(string mensaje, Color color)
        {
            LblEstado.Text       = mensaje;
            LblEstado.Foreground = new SolidColorBrush(color);
        }
    }
}
