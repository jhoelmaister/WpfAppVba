using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class SucursalesDetalle : Window
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly SucursalesGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;

        public SucursalesDetalle(SucursalesGeneral padre, string idEditar = "")
        {
            InitializeComponent();
            _padre    = padre;
            _idEditar = idEditar;
            Loaded   += (_, _) => CargarUserform();
        }

        // ─── Carga inicial ────────────────────────────────────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            if (AppState.EventoFormularioI == "modificar")
            {
                LblTitulo.Text       = "Editar Sucursal";
                Box_Codigo.IsEnabled = false;
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text       = "Nueva Sucursal";
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
            Box_Nit.Text         = Sql.SucursalesObj.ObtenerItem("nit",         id)?.ToString() ?? "";
            Box_Descripcion.Text = Sql.SucursalesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
            Box_Telefono.Text    = Sql.SucursalesObj.ObtenerItem("telefono",    id)?.ToString() ?? "";
            Box_Direccion.Text   = Sql.SucursalesObj.ObtenerItem("direccion",   id)?.ToString() ?? "";
            Box_Observacion.Text = Sql.SucursalesObj.ObtenerItem("observacion", id)?.ToString() ?? "";

            string regionId = Sql.SucursalesObj.ObtenerItem("region", id)?.ToString() ?? "";
            Box_Referido_Codigo.Text = regionId;
            Box_Referido_Descripcion.Text = Sql.RegionesObj.ObtenerItem("descripcion", regionId)?.ToString() ?? "";

            var fechaObj = Sql.SucursalesObj.ObtenerItem("fecha", id);
            if (fechaObj != null && DateTime.TryParse(fechaObj.ToString(), out DateTime fecha))
                Box_Fecha.SelectedDate = fecha;
            else
                Box_Fecha.SelectedDate = DateTime.Today;
        }

        private void CargarParaNuevo()
        {
            long siguiente = Convert.ToInt64(Sql.SucursalesObj.Maximo("id") ?? 0) + 1;
            Box_Codigo.Text        = siguiente.ToString();
            Box_Fecha.SelectedDate = DateTime.Today;
        }

        // ─── Actualizar descripción de la región referida ─────────────────────
        private void Box_Referido_Codigo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
            _hayCambios = true;
            string regionId = Box_Referido_Codigo.Text.Trim();
            Box_Referido_Descripcion.Text = regionId == ""
                ? ""
                : Sql.RegionesObj.ObtenerItem("descripcion", regionId)?.ToString() ?? "";
        }

        // ─── Detectar cambios en cualquier campo ──────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void Box_Fecha_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Guardar ──────────────────────────────────────────────────────────
        private bool Guardar()
        {
            return AppState.EventoFormularioI == "modificar"
                ? GuardarEditar()
                : GuardarNuevo();
        }

        private bool GuardarEditar()
        {
            string codigo = Box_Codigo.Text.Trim();
            try
            {
                Sql.SucursalesObj.EstablecerItem("nit",         codigo, Box_Nit.Text);
                Sql.SucursalesObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.SucursalesObj.EstablecerItem("telefono",    codigo, Box_Telefono.Text);
                Sql.SucursalesObj.EstablecerItem("region",      codigo, Box_Referido_Codigo.Text);
                Sql.SucursalesObj.EstablecerItem("direccion",   codigo, Box_Direccion.Text);
                Sql.SucursalesObj.EstablecerItem("observacion", codigo, Box_Observacion.Text);
                Sql.SucursalesObj.EstablecerItem("fecha",       codigo, Box_Fecha.SelectedDate ?? DateTime.Today);
                Sql.SucursalesObj.EstablecerItem("edicion",     codigo, DateTime.Now);
                Sql.SucursalesObj.EstablecerItem("usuario",     codigo, AppState.UsuarioActivo);

                Sql.SucursalesObj.OrdenarData(("id", false));
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
                if (!Sql.SucursalesObj.VerificarId(codigo, "id"))
                {
                    MessageBox.Show("El código ya existe", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                Sql.SucursalesObj.Nuevo(codigo);
                Sql.SucursalesObj.EstablecerItem("nit",         codigo, Box_Nit.Text);
                Sql.SucursalesObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.SucursalesObj.EstablecerItem("telefono",    codigo, Box_Telefono.Text);
                Sql.SucursalesObj.EstablecerItem("region",      codigo, Box_Referido_Codigo.Text);
                Sql.SucursalesObj.EstablecerItem("direccion",   codigo, Box_Direccion.Text);
                Sql.SucursalesObj.EstablecerItem("observacion", codigo, Box_Observacion.Text);
                Sql.SucursalesObj.EstablecerItem("fecha",       codigo, Box_Fecha.SelectedDate ?? DateTime.Today);
                Sql.SucursalesObj.EstablecerItem("emision",     codigo, DateTime.Now);
                Sql.SucursalesObj.EstablecerItem("edicion",     codigo, DateTime.Now);
                Sql.SucursalesObj.EstablecerItem("usuario",     codigo, AppState.UsuarioActivo);

                Sql.SucursalesObj.OrdenarData(("id", false));
                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ─── Validación de entrada ────────────────────────────────────────────
        private void Box_Nit_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        private void Box_Referido_Codigo_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        private void Box_Telefono_PreviewTextInput(object sender, TextCompositionEventArgs e)
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
