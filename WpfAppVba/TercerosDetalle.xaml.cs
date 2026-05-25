using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class TercerosDetalle : Window
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly string _idEditar;   // solo en modo modificar
        private bool _hayCambios  = false;
        private bool _cargando    = true;    // evita disparar cambios al cargar

        public TercerosDetalle(string idEditar = "")
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            _idEditar = idEditar;
            Loaded  += (_, _) => CargarUserform();
        }

        // ─── Carga inicial (equivalente a cargarUserform) ─────────────────────
        private void CargarUserform()
        {
            _cargando = true;

            if (AppState.EventoFormularioL == "modificar")
            {
                LblTitulo.Text        = "Editar Tercero";
                Box_Codigo.IsEnabled  = false;
                CargarParaEditar();
            }
            else
            {
                LblTitulo.Text       = "Nuevo Tercero";
                Box_Codigo.IsEnabled = true;
                CargarParaNuevo();
            }

            _cargando    = false;
            _hayCambios  = false;
        }

        private void CargarParaEditar()
        {
            string id = _idEditar;
            Box_Codigo.Text      = id;
            Box_Nit.Text         = Sql.TercerosObj.ObtenerItem("nit",         id)?.ToString() ?? "";
            Box_Descripcion.Text = Sql.TercerosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
            Box_Contacto.Text    = Sql.TercerosObj.ObtenerItem("contacto",    id)?.ToString() ?? "";
            Box_Telefono.Text    = Sql.TercerosObj.ObtenerItem("telefono",    id)?.ToString() ?? "";
            Box_Direccion.Text   = Sql.TercerosObj.ObtenerItem("direccion",   id)?.ToString() ?? "";
            Box_Contacto2.Text   = Sql.TercerosObj.ObtenerItem("contacto2",   id)?.ToString() ?? "";
            Box_Telefono2.Text   = Sql.TercerosObj.ObtenerItem("telefono2",   id)?.ToString() ?? "";
            Box_Observacion.Text = Sql.TercerosObj.ObtenerItem("observacion", id)?.ToString() ?? "";
        }

        private void CargarParaNuevo()
        {
            long siguiente = Convert.ToInt64(Sql.TercerosObj.Maximo("id") ?? 0) + 1;
            Box_Codigo.Text = siguiente.ToString();
        }

        // ─── Detectar cambios en cualquier campo ──────────────────────────────
        private void Campo_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        // ─── Guardar (equivalente a guardarCambios) ───────────────────────────
        private bool Guardar()
        {
            return AppState.EventoFormularioL == "modificar"
                ? GuardarEditar()
                : GuardarNuevo();
        }

        private bool GuardarEditar()
        {
            string codigo = Box_Codigo.Text.Trim();
            try
            {
                Sql.TercerosObj.EstablecerItem("nit",         codigo, Box_Nit.Text);
                Sql.TercerosObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.TercerosObj.EstablecerItem("contacto",    codigo, Box_Contacto.Text);
                Sql.TercerosObj.EstablecerItem("telefono",    codigo, Box_Telefono.Text);
                Sql.TercerosObj.EstablecerItem("direccion",   codigo, Box_Direccion.Text);
                Sql.TercerosObj.EstablecerItem("contacto2",   codigo, Box_Contacto2.Text);
                Sql.TercerosObj.EstablecerItem("telefono2",   codigo, Box_Telefono2.Text);
                Sql.TercerosObj.EstablecerItem("observacion", codigo, Box_Observacion.Text);
                Sql.TercerosObj.EstablecerItem("edicion",     codigo, DateTime.Now);

                Sql.TercerosObj.OrdenarData(("id", false));
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
                if (!Sql.TercerosObj.VerificarId(codigo, "id"))
                {
                    MessageBox.Show("El número de documento ya existe", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                Sql.TercerosObj.Nuevo(codigo);
                Sql.TercerosObj.EstablecerItem("nit",         codigo, Box_Nit.Text);
                Sql.TercerosObj.EstablecerItem("descripcion", codigo, Box_Descripcion.Text);
                Sql.TercerosObj.EstablecerItem("contacto",    codigo, Box_Contacto.Text);
                Sql.TercerosObj.EstablecerItem("telefono",    codigo, Box_Telefono.Text);
                Sql.TercerosObj.EstablecerItem("direccion",   codigo, Box_Direccion.Text);
                Sql.TercerosObj.EstablecerItem("contacto2",   codigo, Box_Contacto2.Text);
                Sql.TercerosObj.EstablecerItem("telefono2",   codigo, Box_Telefono2.Text);
                Sql.TercerosObj.EstablecerItem("observacion", codigo, Box_Observacion.Text);
                Sql.TercerosObj.EstablecerItem("emision",     codigo, DateTime.Now);
                Sql.TercerosObj.EstablecerItem("edicion",     codigo, DateTime.Now);
                Sql.TercerosObj.EstablecerItem("usuario",     codigo, AppState.UsuarioActivo);

                Sql.TercerosObj.OrdenarData(("id", false));
                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // ─── Validación de entrada (equivalente a KeyPress en VBA) ───────────
        private void Box_Nit_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        private void Box_Telefono_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        private void Box_Descripcion_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloLetras(sender, e, permitirEspacios: true);

        // ─── Al cerrar: preguntar si hay cambios (equivalente a UserForm_QueryClose)
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_hayCambios) return;

            var res = MessageBox.Show("¿Guardar Cambios?", "Consola",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                bool ok = Guardar();
                e.Cancel = !ok; // si guardó mal, no cierra
            }
            else if (res == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
            // No → cierra sin guardar (e.Cancel = false por defecto)
        }
    }
}
