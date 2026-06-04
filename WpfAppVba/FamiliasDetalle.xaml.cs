using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class FamiliasDetalle : UserControl
    {
        public event Action? Cerrando;

        private static SqlData Sql => SqlData.Instance;

        private readonly FamiliasGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;

        private bool _iniciado = false;
        private readonly string _tituloTab;

        /// <summary>ID de la familia recién creada (solo modo "nuevo").</summary>
        public string? ItemCreadoId { get; private set; }

        public FamiliasDetalle(FamiliasGeneral? padre = null, string idEditar = "", string tituloTab = "")
        {
            InitializeComponent();
            _padre     = padre;
            _idEditar  = idEditar;
            _tituloTab = tituloTab;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarUserform(); };
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
        private void Box_Referido_Codigo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
            _hayCambios = true;
            string productoId = Box_Referido_Codigo.Text.Trim();
            Box_Referido_Descripcion.Text = productoId == ""
                ? ""
                : Sql.ProductosObj.ObtenerItem("descripcion", productoId)?.ToString() ?? "";
        }

        // ─── Detectar cambios en cualquier campo ──────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
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
                Sql.FamiliasObj.EstablecerItem("usuarioE",    codigo, AppState.UsuarioActivo);

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
                Sql.FamiliasObj.EstablecerItem("usuarioE",    codigo, AppState.UsuarioActivo);

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

        // ─── Ver productos (modo selector, en pestaña) ────────────────────────
        private void BtnVerProductos_Click(object sender, RoutedEventArgs e)
        {
            ProductosGeneral.OpenAsTab(Window.GetWindow(this)!, id =>
            {
                if (!string.IsNullOrEmpty(id)) Box_Referido_Codigo.Text = id;
            }, contexto: _tituloTab, llamador: this);
        }

        // ─── Validación de entrada ────────────────────────────────────────────
        private void Box_Referido_Codigo_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        // ─── Botones Guardar / Cancelar ───────────────────────────────────────
        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (Guardar()) { _hayCambios = false; Cerrando?.Invoke(); }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        { _hayCambios = false; Cerrando?.Invoke(); }

        // ─── Llamado por el botón X de la pestaña para verificar cambios ──────
        public void IntentarCerrar()
        {
            if (!_hayCambios) { Cerrando?.Invoke(); return; }

            var res = MessageBox.Show("¿Guardar cambios?", "Consola",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes && Guardar()) Cerrando?.Invoke();
            else if (res == MessageBoxResult.No) Cerrando?.Invoke();
        }
    }
}
