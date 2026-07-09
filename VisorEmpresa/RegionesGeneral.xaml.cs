using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SistemaGestion;
using SistemaGestion.Data;

namespace VisorEmpresa
{
    public partial class RegionesGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private readonly Action<string>? _callbackSeleccion;
        private bool _iniciado = false;

        public bool ModoSelector => _callbackSeleccion != null;
        public event Action? Cerrando;

        public RegionesGeneral(Action<string>? callbackSeleccion = null)
        {
            InitializeComponent();
            _callbackSeleccion = callbackSeleccion;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; ConfigurarModo(); CargarRegiones(); };
        }

        public void IntentarCerrar() => Cerrando?.Invoke();

        public static void OpenAsTab(Window owner, Action<string>? callbackSeleccion, string contexto, UIElement? llamador)
        {
            var consola = owner as ConsolaMovimientos;
            if (consola == null) return;
            var ctrl = new RegionesGeneral(callbackSeleccion);
            ctrl.Cerrando += () =>
            {
                consola.CerrarPestaña(ctrl);
                consola.SeleccionarPestaña(llamador);
            };
            string titulo = callbackSeleccion != null
                ? (string.IsNullOrEmpty(contexto) ? "Seleccionar Región" : $"Seleccionar Región ({contexto})")
                : "Regiones";
            if (callbackSeleccion != null)
            {
                string clave = string.IsNullOrEmpty(contexto)
                    ? "seleccionar-region"
                    : $"seleccionar-region|{contexto}";
                consola.CerrarPestañaPorClave(clave);
                consola.AbrirPestaña(titulo, ctrl, clave);
            }
            else
            {
                consola.AbrirPestaña(titulo, ctrl);
            }
        }

        private void ConfigurarModo()
        {
            if (ModoSelector)
            {
                BtnNuevo.Visibility       = Visibility.Collapsed;
                BtnEditar.Visibility      = Visibility.Collapsed;
                BtnEliminar.Visibility    = Visibility.Collapsed;
                BtnSeleccionar.Visibility = Visibility.Visible;
            }
            else if (!AppState.EsAdmin)
            {
                BtnNuevo.Visibility    = Visibility.Collapsed;
                BtnEditar.Visibility   = Visibility.Collapsed;
                BtnEliminar.Visibility = Visibility.Collapsed;
            }
        }

        // ─── Carga la lista ────────────────────────────────────────────────────
        public void CargarRegiones()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<RegionFila>();
            int linea = 1;

            int uf = Sql.RegionesObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.RegionesObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc   = Sql.RegionesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string codigo = Sql.RegionesObj.ObtenerItem("codigo",      id)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    codigo.ToLower().Contains(busqueda))
                {
                    filas.Add(new RegionFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Codigo      = codigo,
                        Descripcion = desc
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<RegionFila> FilasGrid =>
            Grid1.ItemsSource as List<RegionFila> ?? new List<RegionFila>();

        private RegionFila ConstruirFila(string id, int linea)
        {
            return new RegionFila
            {
                Linea       = linea,
                Id          = id,
                Codigo      = Sql.RegionesObj.ObtenerItem("codigo",      id)?.ToString() ?? "",
                Descripcion = Sql.RegionesObj.ObtenerItem("descripcion", id)?.ToString() ?? ""
            };
        }

        private void Renumerar()
        {
            int n = 1;
            foreach (var f in FilasGrid) f.Linea = n++;
            Grid1.Items.Refresh();
        }

        // ─── Búsqueda ─────────────────────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarRegiones();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarRegiones();

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (ModoSelector) Seleccionar();
            else               AbrirEditar();
        }

        // ─── Tecla Enter ──────────────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            if (ModoSelector) Seleccionar();
            else               AbrirEditar();
        }

        // ─── Modo selector ────────────────────────────────────────────────────
        private void Seleccionar()
        {
            if (Grid1.SelectedItem is not RegionFila fila) return;
            _callbackSeleccion?.Invoke(fila.Codigo);
            Cerrando?.Invoke();
        }

        private void BtnSeleccionar_Click(object sender, RoutedEventArgs e)
            => Seleccionar();

        // ─── Botones ──────────────────────────────────────────────────────────
        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new RegionesDetalle();
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                if (detalle.ItemCreadoId == null) return;
                var nueva = ConstruirFila(detalle.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                Renumerar();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña("Nueva Región", detalle, "nueva-region");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not RegionFila fila) return;

            // Verificación de conexión en 2 capas antes de persistir el borrado.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            var res = MessageBox.Show("¿Eliminar esta región?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.RegionesObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.RegionesObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.RegionesObj.Ocultar(fila.Id);
                Sql.RegionesObj.OrdenarData(("id", false));

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
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            Sql.RegionesObj.Actualizar();
            CargarRegiones();
        }

        private void AbrirEditar()
        {
            if (!AppState.EsAdmin) return;
            if (Grid1.SelectedItem is not RegionFila fila) return;
            string idSel = fila.Id;
            int    linea = fila.Linea;
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new RegionesDetalle(fila.Id);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFila(idSel, linea);
                    lista[idx] = actualizada;
                    Renumerar();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña($"Región {fila.Codigo}", detalle, $"region-{idSel}");
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class RegionFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}
