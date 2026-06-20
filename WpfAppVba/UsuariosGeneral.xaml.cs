using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class UsuariosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private bool _iniciado = false;

        public UsuariosGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarUsuarios(); };
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarUsuarios()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<UsuarioFila>();
            int linea = 1;

            int uf = Sql.UsuariosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.UsuariosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string cuenta    = Sql.UsuariosObj.ObtenerItem("cuenta",    id)?.ToString() ?? "";
                string nombres   = Sql.UsuariosObj.ObtenerItem("nombres",   id)?.ToString() ?? "";
                string apellidos = Sql.UsuariosObj.ObtenerItem("apellidos", id)?.ToString() ?? "";
                string tipo      = Sql.UsuariosObj.ObtenerItem("tipo",      id)?.ToString() ?? "";
                string estadoU   = Sql.UsuariosObj.ObtenerItem("estadoU",   id)?.ToString() ?? "";
                string codigo    = Sql.UsuariosObj.ObtenerItem("codigo",    id)?.ToString() ?? "";

                if (busqueda != "" &&
                    !cuenta.ToLower().Contains(busqueda) &&
                    !nombres.ToLower().Contains(busqueda) &&
                    !apellidos.ToLower().Contains(busqueda))
                    continue;

                filas.Add(new UsuarioFila
                {
                    Linea     = linea++,
                    Id        = id,
                    Codigo    = codigo,
                    Cuenta    = cuenta,
                    Nombres   = nombres,
                    Apellidos = apellidos,
                    Tipo      = tipo,
                    EstadoU   = estadoU
                });
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental ────────────────────────────
        private List<UsuarioFila> FilasGrid =>
            Grid1.ItemsSource as List<UsuarioFila> ?? new List<UsuarioFila>();

        private UsuarioFila ConstruirFilaUsuario(string id, int linea) => new UsuarioFila
        {
            Linea     = linea,
            Id        = id,
            Codigo    = Sql.UsuariosObj.ObtenerItem("codigo",    id)?.ToString() ?? "",
            Cuenta    = Sql.UsuariosObj.ObtenerItem("cuenta",    id)?.ToString() ?? "",
            Nombres   = Sql.UsuariosObj.ObtenerItem("nombres",   id)?.ToString() ?? "",
            Apellidos = Sql.UsuariosObj.ObtenerItem("apellidos", id)?.ToString() ?? "",
            Tipo      = Sql.UsuariosObj.ObtenerItem("tipo",      id)?.ToString() ?? "",
            EstadoU   = Sql.UsuariosObj.ObtenerItem("estadoU",   id)?.ToString() ?? ""
        };

        private void Renumerar()
        {
            int n = 1;
            foreach (var f in FilasGrid) f.Linea = n++;
            Grid1.Items.Refresh();
        }

        // ─── Búsqueda ────────────────────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarUsuarios();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarUsuarios();

        // ─── Doble clic / Enter ──────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AbrirEditar();
        }

        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AbrirEditar();
        }

        // ─── Botones ─────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioI = "nuevo";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new UsuariosDetalle(this);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                if (detalle.ItemCreadoId == null) return;
                var nueva = ConstruirFilaUsuario(detalle.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                Renumerar();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña("Nuevo Usuario", detalle, "nuevo-usuario");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            Sql.UsuariosObj.Actualizar();
            CargarUsuarios();
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not UsuarioFila fila) return;

            if (fila.Id == AppState.UsuarioActivo)
            {
                MessageBox.Show("No puedes eliminar tu propio usuario.", "Usuarios",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show($"¿Eliminar el usuario '{fila.Cuenta}'?", "Usuarios",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            // Verificación de conexión en 2 capas (label + chequeo real) antes de persistir.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            try
            {
                Sql.UsuariosObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.UsuariosObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.UsuariosObj.Ocultar(fila.Id);
                Sql.UsuariosObj.ExportarItems();

                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0) lista.RemoveAt(idx);
                Renumerar();

                if (lista.Count > 0)
                {
                    var sel = lista[Math.Min(idx, lista.Count - 1)];
                    Grid1.SelectedItem = sel; Grid1.ScrollIntoView(sel);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Usuarios", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Helper ──────────────────────────────────────────────────────────
        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not UsuarioFila fila) return;
            string idSel = fila.Id;
            int    linea = fila.Linea;
            AppState.EventoFormularioI = "modificar";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new UsuariosDetalle(this, fila.Id);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFilaUsuario(idSel, linea);
                    lista[idx] = actualizada;
                    Renumerar();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña($"Usuario {fila.Cuenta}", detalle, $"usuario-{idSel}");
        }
    }

    // ─── Modelo de fila para el DataGrid ────────────────────────────────────
    public class UsuarioFila
    {
        public int    Linea     { get; set; }
        public string Id        { get; set; } = "";
        public string Codigo    { get; set; } = "";
        public string Cuenta    { get; set; } = "";
        public string Nombres   { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public string Tipo      { get; set; } = "";
        public string EstadoU   { get; set; } = "";
    }
}
