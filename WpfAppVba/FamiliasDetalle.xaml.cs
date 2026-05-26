using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class FamiliasDetalle : Window
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly FamiliasGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;

        /// <summary>ID de la familia recién creada (solo modo "nuevo").</summary>
        public string? ItemCreadoId { get; private set; }

        public FamiliasDetalle(FamiliasGeneral padre, string idEditar = "")
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            _padre    = padre;
            _idEditar = idEditar;
            Loaded   += (_, _) => CargarUserform();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            if (AppState.EventoFormularioF == "modificar")
            {
                LblTitulo.Text       = "Editar Familia";
                Box_Codigo.IsEnabled = false;
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text       = "Nueva Familia";
                Box_Codigo.IsEnabled = true;
                CargarParaNuevo();
            }

            _cargando   = false;
            _hayCambios = false;
        }

        private void CargarParaEditar()
        {
            string id = _idEditar;
            Box_Codigo.Text = id;
            Box_Descripcion.Text = Sql.FamiliasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
            string productoId = Sql.FamiliasObj.ObtenerItem("producto", id)?.ToString() ?? "";
            Box_Referido_Codigo.Text = productoId;
            Box_Referido_Descripcion.Text = Sql.ProductosObj.ObtenerItem("descripcion", productoId)?.ToString() ?? "";
            Box_Observacion.Text = Sql.FamiliasObj.ObtenerItem("observacion", id)?.ToString() ?? "";
        }

        private void CargarParaNuevo()
        {
            long siguiente = Convert.ToInt64(Sql.FamiliasObj.Maximo("id") ?? 0) + 1;
            Box_Codigo.Text = siguiente.ToString();
        }

        // ─── Actualizar descripción del producto referido ─────────────────────
        private void Box_Referido_Codigo_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_cargando) return;
            _hayCambios = true;
            string productoId = Box_Referido_Codigo.Text.Trim();
            Box_Referido_Descripcion.Text = productoId == ""
                ? ""
                : Sql.ProductosObj.ObtenerItem("descripcion", productoId)?.ToString() ?? "";
        }

        // ─── Detectar cambios en cualquier campo ──────────────────────────────
        private void Campo_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private bool Guardar()
        {
            return AppState.EventoFormularioF == "modificar"
                ? GuardarEditar()
                : GuardarNuevo();
        }

        private bool GuardarEditar()
        {
            string codigo = Box_Codigo.Text.Trim();
            try
            {
                Sql.FamiliasObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.FamiliasObj.EstablecerItem("producto",    codigo, Box_Referido_Codigo.Text);
                Sql.FamiliasObj.EstablecerItem("observacion", codigo, Box_Observacion.Text);
                Sql.FamiliasObj.EstablecerItem("edicion",     codigo, DateTime.Now);

                Sql.FamiliasObj.OrdenarData(("id", false));
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
                if (!Sql.FamiliasObj.VerificarId(codigo, "id"))
                {
                    MessageBox.Show("El código ya existe", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                Sql.FamiliasObj.Nuevo(codigo);
                Sql.FamiliasObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.FamiliasObj.EstablecerItem("producto",    codigo, Box_Referido_Codigo.Text);
                Sql.FamiliasObj.EstablecerItem("observacion", codigo, Box_Observacion.Text);
                Sql.FamiliasObj.EstablecerItem("emision",     codigo, DateTime.Now);
                Sql.FamiliasObj.EstablecerItem("edicion",     codigo, DateTime.Now);
                Sql.FamiliasObj.EstablecerItem("usuario",     codigo, AppState.UsuarioActivo);

                Sql.FamiliasObj.OrdenarData(("id", false));
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

        // ─── Validación de entrada ────────────────────────────────────────────
        private void Box_Referido_Codigo_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        private void Box_Descripcion_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloLetras(sender, e, permitirEspacios: true);

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
