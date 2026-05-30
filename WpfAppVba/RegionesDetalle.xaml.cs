using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class RegionesDetalle : Window
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly string _idEditar;
        private readonly bool   _modoEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;

        /// <summary>ID de la región recién creada (solo modo "nuevo").</summary>
        public string? ItemCreadoId { get; private set; }

        public RegionesDetalle(string idEditar = "")
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            _idEditar   = idEditar;
            _modoEditar = !string.IsNullOrEmpty(idEditar);
            Loaded += (_, _) => CargarUserform();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            if (_modoEditar)
            {
                LblTitulo.Text       = "Editar Región";
                Box_Codigo.IsEnabled = false;
                Box_Codigo.Background = (System.Windows.Media.Brush)FindResource("ThemeBgReadOnly");
                Box_Codigo.Foreground = (System.Windows.Media.Brush)FindResource("ThemeTextoMuted");
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text       = "Nueva Región";
                Box_Codigo.IsEnabled = true;
                CargarParaNuevo();
            }

            _cargando   = false;
            _hayCambios = false;
        }

        private void CargarParaEditar()
        {
            string id = _idEditar;
            Box_Codigo.Text      = id;
            Box_Descripcion.Text = Sql.RegionesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
        }

        private void CargarParaNuevo()
        {
            long siguiente = Convert.ToInt64(Sql.RegionesObj.Maximo("id") ?? 0) + 1;
            Box_Codigo.Text = siguiente.ToString();
        }

        // ─── Detectar cambios en cualquier campo ──────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private bool Guardar()
            => _modoEditar ? GuardarEditar() : GuardarNuevo();

        private bool GuardarEditar()
        {
            string codigo = Box_Codigo.Text.Trim();
            try
            {
                Sql.RegionesObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.RegionesObj.EstablecerItem("edicion",     codigo, DateTime.Now);
                Sql.RegionesObj.EstablecerItem("usuarioE",    codigo, AppState.UsuarioActivo);

                Sql.RegionesObj.OrdenarData(("id", false));
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
            string codigo = Box_Codigo.Text.Trim();
            try
            {
                if (!Sql.RegionesObj.VerificarId(codigo, "id"))
                {
                    MessageBox.Show("El código ya existe", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                Sql.RegionesObj.Nuevo(codigo);
                Sql.RegionesObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.RegionesObj.EstablecerItem("emision",     codigo, DateTime.Now);
                Sql.RegionesObj.EstablecerItem("edicion",     codigo, DateTime.Now);
                Sql.RegionesObj.EstablecerItem("usuario",     codigo, AppState.UsuarioActivo);
                Sql.RegionesObj.EstablecerItem("usuarioE",    codigo, AppState.UsuarioActivo);

                Sql.RegionesObj.OrdenarData(("id", false));
                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                ItemCreadoId = codigo;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (Guardar()) { _hayCambios = false; Close(); }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        { _hayCambios = false; Close(); }

        // ─── Al cerrar: preguntar si hay cambios ──────────────────────────────
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_hayCambios) return;

            var res = MessageBox.Show("¿Guardar Cambios?", "Consola",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                bool ok = Guardar();
                e.Cancel = !ok;
            }
            else if (res == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
        }
    }
}
