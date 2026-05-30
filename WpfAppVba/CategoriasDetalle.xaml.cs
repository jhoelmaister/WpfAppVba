using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class CategoriasDetalle : Window
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly string _idEditar;
        private readonly bool   _modoEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;

        /// <summary>ID de la categoría recién creada (solo modo "nuevo").</summary>
        public string? ItemCreadoId { get; private set; }

        public CategoriasDetalle(string idEditar = "")
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            _idEditar   = idEditar;
            _modoEditar = !string.IsNullOrEmpty(idEditar);
            Loaded   += (_, _) => CargarUserform();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            if (_modoEditar)
            {
                LblTitulo.Text       = "Editar Categoría";
                Box_Codigo.IsEnabled = false;
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text       = "Nueva Categoría";
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
            Box_Descripcion.Text = Sql.CategoriasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
        }

        private void CargarParaNuevo()
        {
            long siguiente = Convert.ToInt64(Sql.CategoriasObj.Maximo("id") ?? 0) + 1;
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
                Sql.CategoriasObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.CategoriasObj.EstablecerItem("edicion",     codigo, DateTime.Now);
                Sql.CategoriasObj.EstablecerItem("usuarioE",    codigo, AppState.UsuarioActivo);

                Sql.CategoriasObj.OrdenarData(("id", false));
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
                if (!Sql.CategoriasObj.VerificarId(codigo, "id"))
                {
                    MessageBox.Show("El código ya existe", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                Sql.CategoriasObj.Nuevo(codigo);
                Sql.CategoriasObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.CategoriasObj.EstablecerItem("emision",     codigo, DateTime.Now);
                Sql.CategoriasObj.EstablecerItem("edicion",     codigo, DateTime.Now);
                Sql.CategoriasObj.EstablecerItem("usuario",     codigo, AppState.UsuarioActivo);
                Sql.CategoriasObj.EstablecerItem("usuarioE",    codigo, AppState.UsuarioActivo);

                Sql.CategoriasObj.OrdenarData(("id", false));
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
