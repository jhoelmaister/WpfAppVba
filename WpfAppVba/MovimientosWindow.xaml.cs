using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfAppVba.Data;

namespace WpfAppVba
{
    public partial class MovimientosWindow : Window
    {
        private static SqlData Sql => SqlData.Instance;
        private string _identificadorArticulo = "";   // ID numérico del artículo activo

        // ─── Constructor: puede recibir código de artículo pre-cargado ─────────
        public MovimientosWindow(string codigoArticulo = "")
        {
            InitializeComponent();
            WindowHelper.AjustarAlEcran(this);
            Loaded += (_, _) =>
            {
                if (!string.IsNullOrEmpty(codigoArticulo))
                {
                    TxtCodigo.Text = codigoArticulo;
                    BuscarArticulo();
                }
            };
        }

        // ─── Buscar artículo por código ───────────────────────────────────────
        private void BuscarArticulo()
        {
            string codigo = TxtCodigo.Text.Trim();
            if (string.IsNullOrEmpty(codigo)) return;

            long id = Sql.ArticulosObj.BuscarIdentificador("codigo", codigo);
            if (id == 0)
            {
                LblDescripcion.Text = "Artículo no encontrado.";
                _identificadorArticulo = "";
                Grid1.ItemsSource = null;
                return;
            }

            _identificadorArticulo = id.ToString();

            string desc    = Sql.ArticulosObj.ObtenerItem("descripcion", _identificadorArticulo)?.ToString() ?? "";
            string modelo  = Sql.ArticulosObj.ObtenerItem("modelo",      _identificadorArticulo)?.ToString() ?? "";
            string famId   = Sql.ArticulosObj.ObtenerItem("familia",      _identificadorArticulo)?.ToString() ?? "";
            string famDesc = Sql.FamiliasObj.ObtenerItem("descripcion",  famId)?.ToString() ?? "";

            LblDescripcion.Text = FuncionesComunes.UnirVariables(desc, famDesc, modelo);
            CargarMovimientos();
        }

        // ─── Carga y muestra todos los movimientos del artículo ───────────────
        private void CargarMovimientos()
        {
            if (string.IsNullOrEmpty(_identificadorArticulo)) return;

            var datos = new List<MovimientoDato>();

            // 1. Apertura activa (snapshot de inventario inicial)
            foreach (var item in AppState.AperturaActiva)
            {
                if (item == null) continue;
                if (item.ArticuloId != _identificadorArticulo) continue;

                datos.Add(new MovimientoDato
                {
                    Fecha      = item.Fecha,
                    Documento  = "0",
                    Movimiento = "apertura",
                    Estado     = "-",
                    Cantidad   = item.Cantidad,
                    Unitario   = "-",
                    SubTotal   = "-",
                    Forma      = "-",
                    Contable   = "-"
                });
            }

            // 2. Pedidos — solo los entregados
            int uf = Sql.PedidosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.PedidosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.PedidosObj.ObtenerItem("articulo", id)?.ToString() != _identificadorArticulo) continue;

                string docP  = Sql.PedidosObj.ObtenerItem("documentoP", id)?.ToString() ?? "";
                string estado = Sql.DocumentosPObj.ObtenerItem("estado", docP)?.ToString() ?? "";
                if (estado != "entregado") continue;

                double cantidad = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad", id) ?? 0);
                double importe  = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("importe",  id) ?? 0);
                double unitario = cantidad != 0 ? importe / cantidad : 0;
                string movDoc   = Sql.DocumentosPObj.ObtenerItem("movimiento", docP)?.ToString() ?? "";
                var   fechaObj  = Sql.DocumentosPObj.ObtenerItem("fecha", docP);
                DateTime fecha  = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;

                datos.Add(new MovimientoDato
                {
                    Fecha      = fecha,
                    Documento  = docP,
                    Movimiento = movDoc,
                    Estado     = estado,
                    Cantidad   = cantidad,
                    Unitario   = unitario.ToString("N2"),
                    SubTotal   = importe.ToString("N2"),
                    Forma      = Sql.PedidosObj.ObtenerItem("forma",     id)?.ToString() ?? "-",
                    Contable   = Sql.PedidosObj.ObtenerItem("contable",  id)?.ToString() ?? "-"
                });
            }

            // 3. Traspasos — todos (independiente del estado)
            uf = Sql.TraspasosObj.ContarFilas;
            for (int i = 1; i <= uf; i++)
            {
                var idObj = Sql.TraspasosObj.Mover(i);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                if (Sql.TraspasosObj.ObtenerItem("articulo", id)?.ToString() != _identificadorArticulo) continue;

                string docT   = Sql.TraspasosObj.ObtenerItem("documentoT", id)?.ToString() ?? "";
                string estado = Sql.DocumentosTObj.ObtenerItem("estado",   docT)?.ToString() ?? "";
                string origen  = Sql.DocumentosTObj.ObtenerItem("origen",  docT)?.ToString() ?? "";
                string destino = Sql.DocumentosTObj.ObtenerItem("destino", docT)?.ToString() ?? "";
                string emitido = Sql.DocumentosTObj.ObtenerItem("emitido", docT)?.ToString() ?? "";

                // "pendiente revisar" si no fue emitida por esta sucursal
                if (emitido != AppState.SucursalActiva.ToString() && estado == "pendiente")
                    estado = "pendiente revisar";

                // Determinar dirección respecto a la sucursal activa
                string movimiento = (origen == AppState.SucursalActiva.ToString() &&
                                     destino != AppState.SucursalActiva.ToString())
                                    ? "salida" : "entrada";

                var fechaObj   = Sql.DocumentosTObj.ObtenerItem("fecha", docT);
                DateTime fecha = fechaObj != null ? Convert.ToDateTime(fechaObj) : default;
                double cantidad = Convert.ToDouble(Sql.TraspasosObj.ObtenerItem("cantidad", id) ?? 0);

                datos.Add(new MovimientoDato
                {
                    Fecha      = fecha,
                    Documento  = docT,
                    Movimiento = movimiento,
                    Estado     = estado,
                    Cantidad   = cantidad,
                    Unitario   = "-",
                    SubTotal   = "-",
                    Forma      = "-",
                    Contable   = "-"
                });
            }

            // ── Ordenar por fecha y luego por documento ───────────────────────
            datos = datos
                .OrderBy(d => d.Fecha)
                .ThenBy(d => d.Documento)
                .ToList();

            // ── Construir filas con stock acumulado ───────────────────────────
            var lista = new List<MovimientoFila>();
            int linea = 1;
            double stock = 0;
            double totalCompras = 0, totalVentas = 0, totalEntradas = 0, totalSalidas = 0;

            foreach (var d in datos)
            {
                switch (d.Movimiento)
                {
                    case "apertura": stock += d.Cantidad; break;
                    case "compra":   stock += d.Cantidad; totalCompras  += d.Cantidad; break;
                    case "venta":    stock -= d.Cantidad; totalVentas   += d.Cantidad; break;
                    case "entrada":  stock += d.Cantidad; totalEntradas += d.Cantidad; break;
                    case "salida":   stock -= d.Cantidad; totalSalidas  += d.Cantidad; break;
                }

                lista.Add(new MovimientoFila
                {
                    Linea      = linea++,
                    FechaStr   = d.Fecha != default ? $"{d.Fecha:d} {d.Fecha:HH:mm:ss}" : "-",
                    Movimiento = string.IsNullOrEmpty(d.Documento) || d.Documento == "0"
                                 ? d.Movimiento
                                 : $"{d.Documento.PadLeft(6, '0')}-{d.Movimiento}",
                    Estado     = d.Estado,
                    Forma      = d.Forma,
                    Contable   = d.Contable,
                    Cantidad   = d.Cantidad,
                    Unitario   = d.Unitario,
                    SubTotal   = d.SubTotal,
                    Stock      = stock
                });
            }

            Grid1.ItemsSource = lista;

            TxtTotalCompras.Text  = totalCompras.ToString("N0");
            TxtTotalVentas.Text   = totalVentas.ToString("N0");
            TxtTotalEntradas.Text = totalEntradas.ToString("N0");
            TxtTotalSalidas.Text  = totalSalidas.ToString("N0");
        }

        // ─── Eventos ─────────────────────────────────────────────────────────
        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
            => BuscarArticulo();

        private void TxtCodigo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { BuscarArticulo(); e.Handled = true; }
        }

        private void BtnProcesar_Click(object sender, RoutedEventArgs e)
            => CargarMovimientos();
    }

    // ─── Dato interno (antes de ordenar) ─────────────────────────────────────
    internal class MovimientoDato
    {
        public DateTime Fecha      { get; set; }
        public string   Documento  { get; set; } = "";
        public string   Movimiento { get; set; } = "";
        public string   Estado     { get; set; } = "";
        public double   Cantidad   { get; set; }
        public string   Unitario   { get; set; } = "";
        public string   SubTotal   { get; set; } = "";
        public string   Forma      { get; set; } = "";
        public string   Contable   { get; set; } = "";
    }

    // ─── Fila del DataGrid ────────────────────────────────────────────────────
    public class MovimientoFila
    {
        public int    Linea      { get; set; }
        public string FechaStr   { get; set; } = "";
        public string Movimiento { get; set; } = "";
        public string Estado     { get; set; } = "";
        public string Forma      { get; set; } = "";
        public string Contable   { get; set; } = "";
        public double Cantidad   { get; set; }
        public string Unitario   { get; set; } = "";
        public string SubTotal   { get; set; } = "";
        public double Stock      { get; set; }
    }
}
