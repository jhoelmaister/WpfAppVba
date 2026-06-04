using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class TercerosGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private readonly bool _modoSelector;

        public event Action? Cerrando;
        public static string? TerceroSeleccionado { get; set; }

        public TercerosGeneral() : this(false) { }

        public TercerosGeneral(bool modoSelector = false)
        {
            InitializeComponent();
            _modoSelector = modoSelector;
            Loaded += (_, _) => CargarTerceros();
        }

        public void IntentarCerrar() => Cerrando?.Invoke();

        public static void OpenAsDialog(Window owner, bool modoSelector = false, string contexto = "", Action? onCerrado = null)
        {
            var consola = owner as ConsolaMovimientos;
            if (consola == null) return;
            var ctrl = new TercerosGeneral(modoSelector);
            ctrl.Cerrando += () => { consola.CerrarPestaña(ctrl); onCerrado?.Invoke(); };
            string titulo = !modoSelector ? "Terceros"
                : string.IsNullOrEmpty(contexto) ? "Seleccionar Tercero"
                : $"Seleccionar Tercero ({contexto})";
            consola.AbrirPestaña(titulo, ctrl);
        }

        // ─── Carga la lista (equivalente a cargarTerceros) ────────────────────
        public void CargarTerceros()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<TerceroFila>();
            int linea = 1;

            int uf = Sql.TercerosObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.TercerosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc = Sql.TercerosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string nit  = Sql.TercerosObj.ObtenerItem("nit",         id)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    nit.ToLower().Contains(busqueda))
                {
                    filas.Add(new TerceroFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Nit         = nit,
                        Descripcion = desc,
                        Telefono    = Sql.TercerosObj.ObtenerItem("telefono", id)?.ToString() ?? ""
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Helpers de actualización incremental del Grid1 ───────────────────
        private List<TerceroFila> FilasGrid =>
            Grid1.ItemsSource as List<TerceroFila> ?? new List<TerceroFila>();

        private TerceroFila ConstruirFilaTercero(string id, int linea)
        {
            return new TerceroFila
            {
                Linea       = linea,
                Id          = id,
                Nit         = Sql.TercerosObj.ObtenerItem("nit",         id)?.ToString() ?? "",
                Descripcion = Sql.TercerosObj.ObtenerItem("descripcion", id)?.ToString() ?? "",
                Telefono    = Sql.TercerosObj.ObtenerItem("telefono",    id)?.ToString() ?? ""
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
            => CargarTerceros();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarTerceros();

        // ─── Doble clic ───────────────────────────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_modoSelector) Seleccionar();
            else               AbrirEditar();
        }

        // ─── Tecla Enter ──────────────────────────────────────────────────────
        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            if (_modoSelector) Seleccionar();
            else               AbrirEditar();
        }

        // ─── Modo selector ────────────────────────────────────────────────────
        private void Seleccionar()
        {
            if (Grid1.SelectedItem is not TerceroFila fila) return;
            TerceroSeleccionado = fila.Id;
            Cerrando?.Invoke();
        }

        // ─── Botones ──────────────────────────────────────────────────────────

        private void BtnNuevo_Click(object sender, RoutedEventArgs e)
        {
            AppState.EventoFormularioL = "nuevo";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new TercerosDetalle();
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                if (detalle.ItemCreadoId == null) return;
                var nueva = ConstruirFilaTercero(detalle.ItemCreadoId, 0);
                FilasGrid.Add(nueva);
                Renumerar();
                Grid1.SelectedItem = nueva; Grid1.ScrollIntoView(nueva);
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña("Nuevo Tercero", detalle, "nuevo-tercero");
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
            => AbrirEditar();

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (Grid1.SelectedItem is not TerceroFila fila) return;

            var res = MessageBox.Show("¿Eliminar este tercero?", "Consola",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                Sql.TercerosObj.EstablecerItem("edicion",  fila.Id, DateTime.Now);
                Sql.TercerosObj.EstablecerItem("usuarioE", fila.Id, AppState.UsuarioActivo);
                Sql.TercerosObj.Ocultar(fila.Id);
                Sql.TercerosObj.OrdenarData(("id", false));

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
            Sql.TercerosObj.Actualizar();
            CargarTerceros();
        }

        private void AbrirEditar()
        {
            if (Grid1.SelectedItem is not TerceroFila fila) return;
            string idSel = fila.Id;
            int    linea = fila.Linea;
            AppState.EventoFormularioL = "modificar";
            var consola = Window.GetWindow(this) as ConsolaMovimientos;
            if (consola == null) return;
            var detalle = new TercerosDetalle(idSel);
            detalle.Cerrando += () =>
            {
                consola.CerrarPestaña(detalle);
                var lista = FilasGrid;
                int idx   = lista.IndexOf(fila);
                if (idx >= 0)
                {
                    var actualizada = ConstruirFilaTercero(idSel, linea);
                    lista[idx] = actualizada;
                    Renumerar();
                    Grid1.SelectedItem = actualizada; Grid1.ScrollIntoView(actualizada);
                }
                GridFocusHelper.EnfocarCeldaSeleccionada(Grid1);
            };
            consola.AbrirPestaña($"Tercero {idSel}", detalle);
        }
    }

    // ─── Modelo de fila para el DataGrid ─────────────────────────────────────
    public class TerceroFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Nit         { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Telefono    { get; set; } = "";
    }
}
