using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class PreciosDetalle : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly string _articuloId;
        private readonly string _idEditar;
        private readonly string _regionId;
        private readonly string _regionDesc;
        private readonly bool   _modoEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;
        private bool _iniciado   = false;
        private string _tituloTab = "";

        public event Action? Cerrando;

        /// <summary>ID del registro de precio recién creado (solo modo "nuevo").</summary>
        public string? ItemCreadoId { get; private set; }

        public PreciosDetalle(string articuloId, string articuloCodigo, string articuloDescripcion,
                              string idEditar = "", string regionId = "", string regionDesc = "")
        {
            InitializeComponent();
            _articuloId = articuloId;
            _idEditar   = idEditar;
            _regionId   = regionId;
            _regionDesc = regionDesc;
            _modoEditar = !string.IsNullOrEmpty(idEditar);
            _tituloTab  = string.IsNullOrEmpty(idEditar) ? $"nuevo-precio-{articuloId}" : $"precio-{idEditar}";

            Loaded += (_, _) =>
            {
                if (_iniciado) return;
                _iniciado = true;
                Box_Articulo.Text = $"{articuloCodigo} - {articuloDescripcion}";
                CargarUserform();
            };
        }

        public void IntentarCerrar()
        {
            if (!_hayCambios) { Cerrando?.Invoke(); return; }

            var res = MessageBox.Show("¿Guardar Cambios?", "Consola",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes && Guardar()) Cerrando?.Invoke();
            else if (res == MessageBoxResult.No)          Cerrando?.Invoke();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            if (_modoEditar)
            {
                LblTitulo.Text = "Editar Precio";
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text = "Nuevo Precio";
                var ahora = DateTime.Now;
                Box_Fecha.SelectedDate = ahora.Date;
                Box_Hora.Text          = ahora.ToString("HH:mm:ss");
                Box_Region_Codigo.Text = Sql.RegionesObj.ObtenerItem("codigo", _regionId)?.ToString() ?? "";
                ActualizarDescripcionRegion();
            }

            _cargando   = false;
            _hayCambios = false;
        }

        private void CargarParaEditar()
        {
            string id = _idEditar;

            var fechaObj = Sql.PreciosObj.ObtenerItem("fecha", id);
            DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : DateTime.Now;
            Box_Fecha.SelectedDate = fecha.Date;
            Box_Hora.Text          = fecha.ToString("HH:mm:ss");

            string regionId = Sql.PreciosObj.ObtenerItem("region", id)?.ToString() ?? "";
            Box_Region_Codigo.Text = Sql.RegionesObj.ObtenerItem("codigo", regionId)?.ToString() ?? "";
            ActualizarDescripcionRegion();

            var precioObj = Sql.PreciosObj.ObtenerItem("precio", id);
            Box_Precio.Text = precioObj != null
                ? Convert.ToDouble(precioObj).ToString("N2", CultureInfo.CurrentCulture)
                : "";
        }

        // ─── Resolver el id (UUID) de la región a partir del código digitado ──
        private string ResolverRegionId()
        {
            string cod = Box_Region_Codigo.Text.Trim();
            return cod == "" ? "" : Sql.RegionesObj.BuscarIdentificador("codigo", cod);
        }

        // ─── Descripción de la región referida ────────────────────────────────
        private void ActualizarDescripcionRegion()
        {
            string regionId = ResolverRegionId();
            Box_Region_Descripcion.Text = regionId == ""
                ? ""
                : Sql.RegionesObj.ObtenerItem("descripcion", regionId)?.ToString() ?? "";
        }

        // ─── Detectar cambios ─────────────────────────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void Box_Region_Codigo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
            _hayCambios = true;
            ActualizarDescripcionRegion();
        }

        private void Box_Fecha_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Validación de entrada ────────────────────────────────────────────
        private void Box_Numeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        private void Box_Decimales_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: true);

        private void Box_Precio_LostFocus(object sender, RoutedEventArgs e)
        {
            double valor = ParsearPrecio();
            if (valor > 0)
            {
                _cargando = true;
                Box_Precio.Text = valor.ToString("N2", CultureInfo.CurrentCulture);
                _cargando = false;
            }
        }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private bool Guardar()
            => _modoEditar ? GuardarEditar() : GuardarNuevo();

        private bool GuardarEditar()
        {
            string id = _idEditar;
            try
            {
                Sql.PreciosObj.EstablecerItem("fecha",    id, ObtenerFechaHora());
                Sql.PreciosObj.EstablecerItem("region",   id, ResolverRegionId());
                Sql.PreciosObj.EstablecerItem("precio",   id, ParsearPrecio());
                Sql.PreciosObj.EstablecerItem("edicion",  id, DateTime.Now);
                Sql.PreciosObj.EstablecerItem("usuarioE", id, AppState.UsuarioActivo);

                Sql.PreciosObj.OrdenarData(("fecha", false));
                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool GuardarNuevo()
        {
            try
            {
                string id = Guid.NewGuid().ToString();

                // Código del precio = signo de la región activa + correlativo
                string signo  = Sql.RegionesObj.ObtenerItem("signo", AppState.RegionActiva)?.ToString() ?? "";
                int    numero = Sql.PreciosObj.SiguienteNumeroDoc(signo, "region", AppState.RegionActiva);
                string codigoPrecio = $"{signo.ToUpper()}{numero}";

                Sql.PreciosObj.Nuevo(id);
                Sql.PreciosObj.EstablecerItem("codigo",   id, codigoPrecio);
                Sql.PreciosObj.EstablecerItem("articulo", id, _articuloId);
                Sql.PreciosObj.EstablecerItem("fecha",    id, ObtenerFechaHora());
                Sql.PreciosObj.EstablecerItem("region",   id, ResolverRegionId());
                Sql.PreciosObj.EstablecerItem("precio",   id, ParsearPrecio());
                Sql.PreciosObj.EstablecerItem("emision",  id, DateTime.Now);
                Sql.PreciosObj.EstablecerItem("edicion",  id, DateTime.Now);
                Sql.PreciosObj.EstablecerItem("usuario",  id, AppState.UsuarioActivo);
                Sql.PreciosObj.EstablecerItem("usuarioE", id, AppState.UsuarioActivo);

                Sql.PreciosObj.OrdenarData(("fecha", false));
                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                ItemCreadoId = id;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ─── Combina la fecha (DatePicker) con la hora (TextBox) ──────────────
        private DateTime ObtenerFechaHora()
        {
            DateTime fecha = Box_Fecha.SelectedDate ?? DateTime.Today;
            if (TimeSpan.TryParse(Box_Hora.Text.Trim(), CultureInfo.InvariantCulture, out var ts))
                return fecha.Date + ts;
            return fecha.Date;
        }

        private double ParsearPrecio()
        {
            string txt = Box_Precio.Text.Trim();
            if (double.TryParse(txt, NumberStyles.Any, CultureInfo.CurrentCulture, out double p)) return p;
            txt = txt.Replace(",", ".");
            return double.TryParse(txt, NumberStyles.Any, CultureInfo.InvariantCulture, out double p2) ? p2 : 0;
        }

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (Guardar()) { _hayCambios = false; Cerrando?.Invoke(); }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        { _hayCambios = false; Cerrando?.Invoke(); }
    }
}
