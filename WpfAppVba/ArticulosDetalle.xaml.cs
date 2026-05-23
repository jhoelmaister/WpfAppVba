using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class ArticulosDetalle : Window
    {
        private static SqlData Sql => SqlData.Instance;

        private readonly ArticulosGeneral? _padre;
        private readonly string _idEditar;
        private bool _hayCambios = false;
        private bool _cargando   = true;

        public ArticulosDetalle(ArticulosGeneral? padre = null, string idEditar = "")
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
            Box_Identificador.IsEnabled = false;

            if (AppState.EventoFormularioA == "modificar")
            {
                LblTitulo.Text                = "Editar Artículo";
                Box_Codigo.IsEnabled          = false;
                BtnVerMovimientos.Visibility  = Visibility.Visible;
                CargarParaEditar();
            }
            else if (AppState.EventoFormularioA == "insertar")
            {
                LblTitulo.Text                = "Insertar Artículo";
                Box_Codigo.IsEnabled          = true;
                BtnVerMovimientos.Visibility  = Visibility.Collapsed;
                CargarParaInsertar();
            }
            else
            {
                LblTitulo.Text                = "Nuevo Artículo";
                Box_Codigo.IsEnabled          = true;
                BtnVerMovimientos.Visibility  = Visibility.Collapsed;
                CargarParaNuevo();
            }

            _cargando   = false;
            _hayCambios = false;
        }

        private void CargarParaEditar()
        {
            string id = _idEditar;
            Box_Identificador.Text = id;
            Box_Codigo.Text        = Sql.ArticulosObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
            Box_Indice.Text        = Sql.ArticulosObj.ObtenerItem("indice",      id)?.ToString() ?? "";

            string famId = Sql.ArticulosObj.ObtenerItem("familia", id)?.ToString() ?? "";
            Box_Identificador_Familia.Text = famId;
            ActualizarDescripcionFamilia();

            string indId = Sql.ArticulosObj.ObtenerItem("industria", id)?.ToString() ?? "";
            Box_Identificador_Industria.Text = indId;
            ActualizarDescripcionIndustria();

            string catId = Sql.ArticulosObj.ObtenerItem("Categoria", id)?.ToString() ?? "";
            Box_Identificador_Categoria.Text = catId;
            ActualizarDescripcionCategoria();

            Box_Descripcion.Text = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
            Box_Modelo.Text      = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
            Box_Observacion.Text = Sql.ArticulosObj.ObtenerItem("observacion", id)?.ToString() ?? "";
        }

        private void CargarParaNuevo()
        {
            long siguiente = Convert.ToInt64(Sql.ArticulosObj.Maximo("id") ?? 0) + 1;
            Box_Identificador.Text = siguiente.ToString();
            Box_Indice.Text        = "1";
        }

        private void CargarParaInsertar()
        {
            long siguiente = Convert.ToInt64(Sql.ArticulosObj.Maximo("id") ?? 0) + 1;
            Box_Identificador.Text = siguiente.ToString();

            // Tomar familia e indice del artículo de referencia
            string famRef = Sql.ArticulosObj.ObtenerItem("familia", _idEditar)?.ToString() ?? "";
            string indRef = Sql.ArticulosObj.ObtenerItem("indice",  _idEditar)?.ToString() ?? "1";

            Box_Identificador_Familia.Text = famRef;
            Box_Indice.Text                = indRef;
            ActualizarDescripcionFamilia();

            // Bloquear campos que no se deben cambiar en modo insertar
            Box_Identificador_Familia.IsEnabled = false;
            Box_Indice.IsEnabled                = false;
            BtnVerFamilias.IsEnabled            = false;
        }

        // ─── Cuando cambia la familia en modo "nuevo": indice = max(familia) + 1 ───
        private void RecalcularIndicePorFamilia()
        {
            string famId = Box_Identificador_Familia.Text.Trim();
            if (string.IsNullOrEmpty(famId)) return;

            int indiceMax = 0;
            int uf = Sql.ArticulosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.ArticulosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string fam = Sql.ArticulosObj.ObtenerItem("familia", id)?.ToString() ?? "";
                if (fam != famId) continue;

                int ind = Convert.ToInt32(Sql.ArticulosObj.ObtenerItem("indice", id) ?? 0);
                if (ind > indiceMax) indiceMax = ind;
            }

            Box_Indice.Text = (indiceMax + 1).ToString();
        }

        // ─── Actualizar descripciones de referidos ────────────────────────────
        private void ActualizarDescripcionFamilia()
        {
            string famId = Box_Identificador_Familia.Text.Trim();
            Box_Familia_Descripcion.Text = string.IsNullOrEmpty(famId)
                ? ""
                : Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "";
        }

        private void ActualizarDescripcionIndustria()
        {
            string id = Box_Identificador_Industria.Text.Trim();
            Box_Industria_Descripcion.Text = string.IsNullOrEmpty(id)
                ? ""
                : Sql.IndustriasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
        }

        private void ActualizarDescripcionCategoria()
        {
            string id = Box_Identificador_Categoria.Text.Trim();
            Box_Categoria_Descripcion.Text = string.IsNullOrEmpty(id)
                ? ""
                : Sql.CategoriasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
        }

        // ─── Detectar cambios ─────────────────────────────────────────────────
        private void Campo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_cargando) _hayCambios = true;
        }

        private void Box_Familia_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
            _hayCambios = true;
            ActualizarDescripcionFamilia();

            // Solo en modo "nuevo": auto-sugerir indice = max(familia) + 1
            if (AppState.EventoFormularioA == "nuevo")
                RecalcularIndicePorFamilia();
        }

        private void Box_Industria_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
            _hayCambios = true;
            ActualizarDescripcionIndustria();
        }

        private void Box_Categoria_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_cargando) return;
            _hayCambios = true;
            ActualizarDescripcionCategoria();
        }

        // ─── Validación de entrada ────────────────────────────────────────────
        private void Box_Numeros_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloNumeros(sender, e, permitirDecimales: false);

        private void Box_Letras_PreviewTextInput(object sender, TextCompositionEventArgs e)
            => FuncionesComunes.ValidarSoloLetras(sender, e, permitirEspacios: true);

        // ─── Ver familias (modo selector) ────────────────────────────────────
        private void BtnVerFamilias_Click(object sender, RoutedEventArgs e)
        {
            FamiliasGeneral.FamiliaSeleccionada = null;
            new FamiliasGeneral(modoSelector: true).ShowDialog();

            if (!string.IsNullOrEmpty(FamiliasGeneral.FamiliaSeleccionada))
            {
                // Asignar el ID — el TextChanged handler llama ActualizarDescripcionFamilia()
                Box_Identificador_Familia.Text = FamiliasGeneral.FamiliaSeleccionada;
            }
        }

        // ─── Ver movimientos del artículo ─────────────────────────────────────
        private void BtnVerMovimientos_Click(object sender, RoutedEventArgs e)
        {
            new MovimientosWindow(Box_Codigo.Text.Trim()).Show();
        }

        // ─── Guardar ─────────────────────────────────────────────────────────
        private bool Guardar()
        {
            return AppState.EventoFormularioA switch
            {
                "modificar" => GuardarEditar(),
                "insertar"  => GuardarInsertar(),
                _            => GuardarNuevo()
            };
        }

        private bool GuardarEditar()
        {
            string codigo = Box_Identificador.Text.Trim();
            try
            {
                Sql.ArticulosObj.EstablecerItem("indice",     codigo, Box_Indice.Text);
                Sql.ArticulosObj.EstablecerItem("familia",    codigo, Box_Identificador_Familia.Text);
                Sql.ArticulosObj.EstablecerItem("industria",  codigo, Box_Identificador_Industria.Text);
                Sql.ArticulosObj.EstablecerItem("Categoria",  codigo, Box_Identificador_Categoria.Text);
                Sql.ArticulosObj.EstablecerItem("descripcion",codigo, Box_Descripcion.Text);
                Sql.ArticulosObj.EstablecerItem("modelo",     codigo, Box_Modelo.Text);
                Sql.ArticulosObj.EstablecerItem("observacion",codigo, Box_Observacion.Text);
                Sql.ArticulosObj.EstablecerItem("edicion",    codigo, DateTime.Now);
                Sql.ArticulosObj.EstablecerItem("usuario",    codigo, AppState.UsuarioActivo);

                Sql.ArticulosObj.OrdenarData(("familia", false), ("indice", false));
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
                if (!Sql.ArticulosObj.VerificarId(codigo, "codigo"))
                {
                    MessageBox.Show("El código ya existe", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                string id = Box_Identificador.Text.Trim();
                Sql.ArticulosObj.Nuevo(id);
                Sql.ArticulosObj.EstablecerItem("codigo",     id, codigo);
                Sql.ArticulosObj.EstablecerItem("indice",     id, Box_Indice.Text);
                Sql.ArticulosObj.EstablecerItem("familia",    id, Box_Identificador_Familia.Text);
                Sql.ArticulosObj.EstablecerItem("industria",  id, Box_Identificador_Industria.Text);
                Sql.ArticulosObj.EstablecerItem("Categoria",  id, Box_Identificador_Categoria.Text);
                Sql.ArticulosObj.EstablecerItem("descripcion",id, Box_Descripcion.Text);
                Sql.ArticulosObj.EstablecerItem("modelo",     id, Box_Modelo.Text);
                Sql.ArticulosObj.EstablecerItem("observacion",id, Box_Observacion.Text);
                Sql.ArticulosObj.EstablecerItem("emision",    id, DateTime.Now);
                Sql.ArticulosObj.EstablecerItem("edicion",    id, DateTime.Now);
                Sql.ArticulosObj.EstablecerItem("usuario",    id, AppState.UsuarioActivo);

                Sql.ArticulosObj.OrdenarData(("familia", false), ("indice", false));
                AppState.ActualizarStocks();

                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool GuardarInsertar()
        {
            string codigo = Box_Codigo.Text.Trim();
            try
            {
                if (!Sql.ArticulosObj.VerificarId(codigo, "codigo"))
                {
                    MessageBox.Show("El código ya existe", "Consola",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                string famNueva = Box_Identificador_Familia.Text.Trim();
                int    indNuevo = Convert.ToInt32(Box_Indice.Text);

                // Bump: subir 1 a todos los indices >= indNuevo dentro de la misma familia
                int uf = Sql.ArticulosObj.ContarFilas;
                for (int i = 1; i <= uf; i++)
                {
                    var idObj = Sql.ArticulosObj.Mover(i);
                    if (idObj == null) continue;
                    string idIt = idObj.ToString()!;

                    string fam = Sql.ArticulosObj.ObtenerItem("familia", idIt)?.ToString() ?? "";
                    if (fam != famNueva) continue;

                    int ind = Convert.ToInt32(Sql.ArticulosObj.ObtenerItem("indice", idIt) ?? 0);
                    if (ind >= indNuevo)
                        Sql.ArticulosObj.EstablecerItem("indice", idIt, ind + 1);
                }

                // Crear el nuevo artículo en la posición indNuevo
                string id = Box_Identificador.Text.Trim();
                Sql.ArticulosObj.Nuevo(id);
                Sql.ArticulosObj.EstablecerItem("codigo",     id, codigo);
                Sql.ArticulosObj.EstablecerItem("indice",     id, indNuevo);
                Sql.ArticulosObj.EstablecerItem("familia",    id, famNueva);
                Sql.ArticulosObj.EstablecerItem("industria",  id, Box_Identificador_Industria.Text);
                Sql.ArticulosObj.EstablecerItem("Categoria",  id, Box_Identificador_Categoria.Text);
                Sql.ArticulosObj.EstablecerItem("descripcion",id, Box_Descripcion.Text);
                Sql.ArticulosObj.EstablecerItem("modelo",     id, Box_Modelo.Text);
                Sql.ArticulosObj.EstablecerItem("observacion",id, Box_Observacion.Text);
                Sql.ArticulosObj.EstablecerItem("emision",    id, DateTime.Now);
                Sql.ArticulosObj.EstablecerItem("edicion",    id, DateTime.Now);
                Sql.ArticulosObj.EstablecerItem("usuario",    id, AppState.UsuarioActivo);

                Sql.ArticulosObj.OrdenarData(("familia", false), ("indice", false));
                AppState.ActualizarStocks();

                MessageBox.Show("Guardado exitoso", "Consola", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Consola", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

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
            // No → cierra sin guardar
        }
    }
}
