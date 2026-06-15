using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class EmpresasGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private readonly Action<string>? _callbackSeleccion;
        private bool _iniciado = false;

        public bool ModoSelector => _callbackSeleccion != null;
        public event Action? Cerrando;

        public EmpresasGeneral(Action<string>? callbackSeleccion = null)
        {
            InitializeComponent();
            _callbackSeleccion = callbackSeleccion;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; ConfigurarModo(); CargarEmpresas(); };
        }

        public void IntentarCerrar() => Cerrando?.Invoke();

        public static void OpenAsTab(Window owner, Action<string>? callbackSeleccion, string contexto, UIElement? llamador)
        {
            var consola = owner as ConsolaMovimientos;
            if (consola == null) return;
            var ctrl = new EmpresasGeneral(callbackSeleccion);
            ctrl.Cerrando += () =>
            {
                consola.CerrarPestaña(ctrl);
                consola.SeleccionarPestaña(llamador);
            };
            string titulo = callbackSeleccion != null
                ? (string.IsNullOrEmpty(contexto) ? "Seleccionar Empresa" : $"Seleccionar Empresa ({contexto})")
                : "Empresas";
            if (callbackSeleccion != null)
            {
                string clave = string.IsNullOrEmpty(contexto)
                    ? "seleccionar-empresa"
                    : $"seleccionar-empresa|{contexto}";
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
        public void CargarEmpresas()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<EmpresaFila>();
            int linea = 1;

            int uf = Sql.EmpresasObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.EmpresasObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc   = Sql.EmpresasObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string codigo = Sql.EmpresasObj.ObtenerItem("codigo",      id)?.ToString() ?? "";
                string signo  = Sql.EmpresasObj.ObtenerItem("signo",       id)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    codigo.ToLower().Contains(busqueda) ||
                    signo.ToLower().Contains(busqueda))
                {
                    filas.Add(new EmpresaFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Codigo      = codigo,
                        Descripcion = desc,
                        Signo       = signo
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<EmpresaFila> FilasGrid =>
            Grid1.ItemsSource as List<EmpresaFila> ?? new List<EmpresaFila>();

        private EmpresaFila ConstruirFila(string id, int linea)
        {
            return new EmpresaFila
            {
                Linea       = linea,
                Id          = id,
                Codigo      = Sql.EmpresasObj.ObtenerItem("codigo",      id)?.ToString() ?? "",
                Descripcion = Sql.EmpresasObj.ObtenerItem("descripcion", id)?.ToString() ?? "",
                Signo       = Sql.EmpresasObj.ObtenerItem("signo",       id)?.ToString() ?? ""
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
            => CargarEmpresas();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarEmpresas();

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
            if (Grid1.SelectedItem is not EmpresaFila fila) return;
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
            var detalle = new EmpresasDetalle();
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
            consola.AbrirPestaña("Nueva Empresa", detalle, "nueva-empresa");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not EmpresaFila fila) return;

            // Verificación de conexión en 2 capas antes de persistir el borrado.
            if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return;

            var res = MessageBox.Show("¿Eliminar esta empresa?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.EmpresasObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.EmpresasObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.EmpresasObj.Ocultar(fila.Id);
                Sql.EmpresasObj.OrdenarData(("id", false));

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

            Sql.EmpresasObj.Actualizar();
            CargarEmpresas();
        }

        private void AbrirEditar()
        {
            if (!AppState.EsAdmin) return;
            if (Grid1.SelectedItem is not EmpresaFila fila) return;
            string idSel = fila.Id;
            int    linea = fila.Linea;
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new EmpresasDetalle(fila.Id);
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
            consola.AbrirPestaña($"Empresa {fila.Codigo}", detalle, $"empresa-{idSel}");
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class EmpresaFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Signo       { get; set; } = "";
    }
}
