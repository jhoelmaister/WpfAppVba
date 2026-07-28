using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SistemaGestion.Data;

namespace SistemaGestion
{
    /// <summary>
    /// Pestaña-selector para validar un pedido desde una factura: arriba la lista
    /// de pedidos (con buscador por código / cliente-proveedor / referencia),
    /// abajo las líneas del pedido elegido con un check por línea, y al pie
    /// "Validar pedido" / "Cancelar".
    ///
    /// Al validar deja el resultado en <see cref="PedidoValidado"/> (id del
    /// documentosP, que va a `documentosF.relacion`) y en
    /// <see cref="LineasValidadas"/>: las líneas tildadas ya sumadas **por
    /// categoría** — una línea de factura por categoría, no una por artículo.
    /// Si se cancela, ambas quedan en null.
    /// </summary>
    public partial class ValidarPedido : System.Windows.Controls.UserControl
    {
        private static SqlData Sql => SqlData.Instance;

        public event Action? Cerrando;

        /// <summary>Id del documentosP validado (null si se canceló).</summary>
        public static string? PedidoValidado { get; set; }

        /// <summary>
        /// Líneas de factura resultantes: las líneas tildadas agrupadas por
        /// categoría (null si se canceló).
        /// </summary>
        public static List<FacturaLineaValidada>? LineasValidadas { get; set; }

        private readonly List<ValidarPedidoFila> _pedidos = new();
        private readonly List<ValidarItemFila>   _items   = new();
        private bool _iniciado = false;

        public ValidarPedido()
        {
            InitializeComponent();
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarPedidos(); };
        }

        public void IntentarCerrar() => Cerrando?.Invoke();

        public static void OpenAsDialog(Window owner, string contexto = "",
                                        Action? onCerrado = null, UIElement? llamador = null)
        {
            if (owner is not ConsolaMovimientos consola) return;

            var ctrl = new ValidarPedido();
            ctrl.Cerrando += () => { consola.CerrarPestaña(ctrl); onCerrado?.Invoke(); consola.SeleccionarPestaña(llamador); };

            string titulo = string.IsNullOrEmpty(contexto)
                            ? "Validar pedido"
                            : $"Validar pedido ({contexto})";
            // Una sola pestaña por llamador (mismo criterio que los demás selectores).
            string clave = string.IsNullOrEmpty(contexto) ? "validar-pedido" : $"validar-pedido|{contexto}";
            consola.CerrarPestañaPorClave(clave);
            consola.AbrirPestaña(titulo, ctrl, clave);
        }

        // ─── Pedidos ──────────────────────────────────────────────────────────
        private void CargarPedidos()
        {
            string busqueda = TxtBuscar.Text.Trim().ToLower();

            var facturadoPorPedido = CalcularFacturadoPorPedido();

            _pedidos.Clear();
            int linea = 1;
            int uf = Sql.DocumentosPObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.DocumentosPObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.DocumentosPObj.ObtenerItem("sucursal", id)?.ToString() != AppState.SucursalActiva) continue;

                string codigo      = Sql.DocumentosPObj.ObtenerItem("codigo",     id)?.ToString() ?? "";
                string referencia  = Sql.DocumentosPObj.ObtenerItem("referencia", id)?.ToString() ?? "";
                string terceroId   = Sql.DocumentosPObj.ObtenerItem("tercero",    id)?.ToString() ?? "";
                string terceroDesc = Sql.TercerosObj.ObtenerItem("descripcion", terceroId)?.ToString() ?? "";

                if (!string.IsNullOrEmpty(busqueda) &&
                    !codigo.ToLower().Contains(busqueda) &&
                    !terceroDesc.ToLower().Contains(busqueda) &&
                    !referencia.ToLower().Contains(busqueda))
                    continue;

                var fechaObj = Sql.DocumentosPObj.ObtenerItem("fecha", id);
                DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;

                _pedidos.Add(new ValidarPedidoFila
                {
                    Linea       = linea++,
                    DocumentoP  = id,
                    Codigo      = codigo,
                    FechaStr    = fecha != default ? $"{fecha:d} {fecha:HH:mm:ss}" : "",
                    Movimiento  = Sql.DocumentosPObj.ObtenerItem("movimiento", id)?.ToString() ?? "",
                    TerceroDesc = terceroDesc,
                    Referencia  = referencia,
                    Importe     = CalcularImportePedido(id),
                    Facturado   = facturadoPorPedido.TryGetValue(id, out double fac) ? fac : 0
                });
            }

            GridPedidos.ItemsSource = null;
            GridPedidos.ItemsSource = _pedidos;
            OcultarDetalle();
        }

        /// <summary>
        /// Importe ya facturado de cada pedido: suma de las líneas de las facturas
        /// cuyo documentosF.relacion apunta a ese pedido. Se arma de una sola
        /// pasada (documentosF → relacion, después facturas → documentoF).
        /// </summary>
        private static Dictionary<string, double> CalcularFacturadoPorPedido()
        {
            var relacionPorFactura = new Dictionary<string, string>();
            int ufD = Sql.DocumentosFObj.ContarFilas;
            for (int i = 1; i <= ufD; i++)
            {
                var idObj = Sql.DocumentosFObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                string relacion = Sql.DocumentosFObj.ObtenerItem("relacion", id)?.ToString() ?? "";
                if (relacion != "") relacionPorFactura[id] = relacion;
            }

            var total = new Dictionary<string, double>();
            int ufF = Sql.FacturasObj.ContarFilas;
            for (int i = 1; i <= ufF; i++)
            {
                var idObj = Sql.FacturasObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                string docF = Sql.FacturasObj.ObtenerItem("documentoF", id)?.ToString() ?? "";
                if (docF == "" || !relacionPorFactura.TryGetValue(docF, out string? pedido)) continue;

                double importe = Convert.ToDouble(Sql.FacturasObj.ObtenerItem("importe", id) ?? 0);
                total[pedido] = (total.TryGetValue(pedido, out double acum) ? acum : 0) + importe;
            }
            return total;
        }

        private static double CalcularImportePedido(string documentoP)
        {
            double importe = 0;
            int uf = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() != documentoP) continue;
                importe += Convert.ToDouble(Sql.PedidosObj.ObtenerItem("importe", id) ?? 0);
            }
            return importe;
        }

        // ─── Líneas del pedido seleccionado ───────────────────────────────────
        private void GridPedidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridPedidos.SelectedItem is ValidarPedidoFila fila)
                MostrarDetalle(fila);
            else
                OcultarDetalle();
        }

        private void MostrarDetalle(ValidarPedidoFila pedido)
        {
            LblDetalleHeader.Text = $"Artículos del pedido {pedido.Codigo}";

            _items.Clear();
            int linea = 1;
            int uf = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;
                if (Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() != pedido.DocumentoP) continue;

                string artId = Sql.PedidosObj.ObtenerItem("articulo", id)?.ToString() ?? "";

                _items.Add(new ValidarItemFila
                {
                    Seleccionado = true,   // por defecto se factura todo el pedido
                    Linea        = linea++,
                    ArticuloId   = artId,
                    Codigo       = Sql.ArticulosObj.ObtenerItem("codigo", artId)?.ToString() ?? artId,
                    Descripcion  = ObtenerDescripcionArticulo(artId),
                    CategoriaId  = Sql.ArticulosObj.ObtenerItem("categoria", artId)?.ToString() ?? "",
                    Cantidad     = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad", id) ?? 0),
                    Importe      = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("importe",  id) ?? 0)
                });
            }

            GridItems.ItemsSource = null;
            GridItems.ItemsSource = _items;
            ActualizarTotalSeleccionado();
        }

        private void OcultarDetalle()
        {
            LblDetalleHeader.Text = "Artículos del pedido";
            _items.Clear();
            GridItems.ItemsSource = null;
            ActualizarTotalSeleccionado();
        }

        private static string ObtenerDescripcionArticulo(string artId)
        {
            string desc    = Sql.ArticulosObj.ObtenerItem("descripcion", artId)?.ToString() ?? "";
            string famId   = Sql.ArticulosObj.ObtenerItem("familia",     artId)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";
            string modelo  = Sql.ArticulosObj.ObtenerItem("modelo",      artId)?.ToString() ?? "";
            return FuncionesComunes.UnirVariables(desc, famDesc, modelo);
        }

        // ─── Selección de líneas ──────────────────────────────────────────────
        private void ChkItem_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not CheckBox chk || chk.DataContext is not ValidarItemFila fila) return;

            fila.Seleccionado = !fila.Seleccionado;
            GridItems.SelectedItem = fila;
            GridItems.Items.Refresh();
            ActualizarTotalSeleccionado();
        }

        private void BtnMarcarTodos_Click(object sender, RoutedEventArgs e)
            => MarcarTodos(true);

        private void BtnDesmarcarTodos_Click(object sender, RoutedEventArgs e)
            => MarcarTodos(false);

        private void MarcarTodos(bool valor)
        {
            foreach (var item in _items) item.Seleccionado = valor;
            GridItems.Items.Refresh();
            ActualizarTotalSeleccionado();
        }

        private void ActualizarTotalSeleccionado()
        {
            var marcados = _items.Where(i => i.Seleccionado).ToList();
            LblTotalSeleccionado.Text  = marcados.Sum(i => i.Importe).ToString("N2");
            LblItemsSeleccionados.Text = _items.Count == 0
                                         ? ""
                                         : $"({marcados.Count} de {_items.Count} líneas)";
        }

        // ─── Buscador / actualizar ────────────────────────────────────────────
        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_iniciado) return;
            CargarPedidos();
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e)
        {
            if (!FuncionesComunes.VerificarConexionParaActualizar(Window.GetWindow(this))) return;
            Sql.DocumentosPObj.Actualizar();
            Sql.PedidosObj.Actualizar();
            Sql.DocumentosFObj.Actualizar();
            Sql.FacturasObj.Actualizar();
            CargarPedidos();
        }

        // ─── Validar / cancelar ───────────────────────────────────────────────
        private void BtnValidar_Click(object sender, RoutedEventArgs e)
        {
            if (GridPedidos.SelectedItem is not ValidarPedidoFila pedido)
            {
                MessageBox.Show("Elegí un pedido de la lista.", "Consola",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var marcados = _items.Where(i => i.Seleccionado).ToList();
            if (marcados.Count == 0)
            {
                MessageBox.Show("Tildá al menos una línea para facturar.", "Consola",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Una línea de factura por categoría: se suman los importes de todas
            // las líneas tildadas que comparten categoría (dos ítems de 200 de la
            // misma categoría entran como una sola línea de 400).
            PedidoValidado  = pedido.DocumentoP;
            LineasValidadas = marcados
                .GroupBy(i => i.CategoriaId)
                .Select(g => new FacturaLineaValidada
                {
                    CategoriaId = g.Key,
                    Concepto    = DescripcionCategoria(g.Key),
                    Importe     = g.Sum(i => i.Importe)
                })
                .ToList();

            Cerrando?.Invoke();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            PedidoValidado  = null;
            LineasValidadas = null;
            Cerrando?.Invoke();
        }

        private static string DescripcionCategoria(string categoriaId)
            => string.IsNullOrEmpty(categoriaId)
               ? "Sin categoría"
               : Sql.CategoriasObj.ObtenerItem("descripcion", categoriaId)?.ToString() ?? "Sin categoría";
    }

    // ─── Modelos ──────────────────────────────────────────────────────────────
    public class ValidarPedidoFila
    {
        public int    Linea       { get; set; }
        public string DocumentoP  { get; set; } = "";
        public string Codigo      { get; set; } = "";
        public string FechaStr    { get; set; } = "";
        public string Movimiento  { get; set; } = "";
        public string TerceroDesc { get; set; } = "";
        public string Referencia  { get; set; } = "";
        public double Importe     { get; set; }
        public double Facturado   { get; set; }
    }

    public class ValidarItemFila
    {
        public bool   Seleccionado { get; set; }
        public int    Linea        { get; set; }
        public string ArticuloId   { get; set; } = "";
        public string Codigo       { get; set; } = "";
        public string Descripcion  { get; set; } = "";
        public string CategoriaId  { get; set; } = "";
        // Se resuelve en vivo contra la caché de Categorias (igual que en FacturasDetalle).
        public string CategoriaDescripcion =>
            string.IsNullOrEmpty(CategoriaId)
                ? "Sin categoría"
                : SqlData.Instance.CategoriasObj.ObtenerItem("descripcion", CategoriaId)?.ToString() ?? "Sin categoría";
        public double Cantidad     { get; set; }
        public double Importe      { get; set; }
    }

    /// <summary>
    /// Línea de factura resultante de validar un pedido: una por categoría, con
    /// el importe ya sumado.
    /// </summary>
    public class FacturaLineaValidada
    {
        public string CategoriaId { get; set; } = "";
        public string Concepto    { get; set; } = "";
        public double Importe     { get; set; }
    }
}
