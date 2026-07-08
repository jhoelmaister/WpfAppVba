using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    /// <summary>
    /// Selector de sucursal (buscar + elegir), usado desde TraspasosDetalle
    /// para elegir la sucursal contraparte de un traspaso. La gestión completa
    /// (crear/editar/eliminar sucursales) se independizó a VisorEmpresa —esta
    /// copia de WpfAppVba quedó solo como picker de lectura.
    /// </summary>
    public partial class SucursalesGeneral : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;
        private bool _iniciado = false;

        public event Action? Cerrando;

        /// <summary>Código de la sucursal elegida (la llama TraspasosDetalle tras Cerrando).</summary>
        public static string? SucursalSeleccionada = null;

        public SucursalesGeneral()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarSucursales(); };
        }

        public void IntentarCerrar() => Cerrando?.Invoke();

        public static void OpenAsDialog(Window owner, string contexto = "", Action? onCerrado = null, UIElement? llamador = null)
        {
            var consola = owner as ConsolaMovimientos;
            if (consola == null) return;
            var ctrl = new SucursalesGeneral();
            ctrl.Cerrando += () => { consola.CerrarPestaña(ctrl); onCerrado?.Invoke(); consola.SeleccionarPestaña(llamador); };
            string titulo = string.IsNullOrEmpty(contexto) ? "Seleccionar Sucursal" : $"Seleccionar Sucursal ({contexto})";
            string clave  = string.IsNullOrEmpty(contexto) ? "seleccionar-sucursal"  : $"seleccionar-sucursal|{contexto}";
            consola.CerrarPestañaPorClave(clave);
            consola.AbrirPestaña(titulo, ctrl, clave);
        }

        // ─── Carga la lista completa ───────────────────────────────────────────
        public void CargarSucursales()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();
            var filas = new List<SucursalFila>();
            int linea = 1;

            int uf = Sql.SucursalesObj.ContarFilas;
            for (int ciclo = 1; ciclo <= uf; ciclo++)
            {
                var idObj = Sql.SucursalesObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string desc   = Sql.SucursalesObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
                string codigo = Sql.SucursalesObj.ObtenerItem("codigo",      id)?.ToString() ?? "";

                if (busqueda == "" ||
                    desc.ToLower().Contains(busqueda) ||
                    codigo.ToLower().Contains(busqueda))
                {
                    filas.Add(new SucursalFila
                    {
                        Linea       = linea++,
                        Id          = id,
                        Codigo      = codigo,
                        Descripcion = desc,
                        Tipo        = Sql.SucursalesObj.ObtenerItem("tipo", id)?.ToString() ?? "",
                        FechaStr    = FormatearFecha(id)
                    });
                }
            }

            Grid1.ItemsSource = filas;
        }

        // ─── Formatea la fecha completa (fecha + hora) de una sucursal ─────────
        private string FormatearFecha(string id)
        {
            var fechaObj = Sql.SucursalesObj.ObtenerItem("fecha", id);
            if (fechaObj != null && DateTime.TryParse(fechaObj.ToString(), out DateTime fecha))
                return $"{fecha:d} {fecha:HH:mm:ss}";
            return "";
        }

        // ─── Búsqueda ─────────────────────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
            => CargarSucursales();

        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => CargarSucursales();

        // ─── Doble clic / Enter → seleccionar ─────────────────────────────────
        private void Grid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Seleccionar();
        }

        private void Grid1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            Seleccionar();
        }

        private void Seleccionar()
        {
            if (Grid1.SelectedItem is not SucursalFila fila) return;
            SucursalSeleccionada = fila.Codigo;
            Cerrando?.Invoke();
        }

        private void BtnSeleccionar_Click(object sender, RoutedEventArgs e)
            => Seleccionar();

        // ─── Actualizar ─────────────────────────────────────────────────────────
        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            // Sin conexión no se puede refrescar desde SQL: avisar y no congelar.
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;

            Sql.SucursalesObj.Actualizar();
            CargarSucursales();
        }
    }

    // ─── Modelo de fila para el DataGrid ──────────────────────────────────────
    public class SucursalFila
    {
        public int    Linea       { get; set; }
        public string Id          { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Tipo        { get; set; } = "";
        public string FechaStr    { get; set; } = "";
    }
}
