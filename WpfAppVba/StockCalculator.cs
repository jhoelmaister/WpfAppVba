using System;
using System.Windows;

namespace TuProyecto.Data
{
    /// <summary>
    /// Equivalente a Funciones_Dedicadas.bas
    /// Contiene el cálculo de stock y helpers de UI (activar/desactivar controles).
    /// </summary>
    public static class StockCalculator
    {
        private static SqlData Sql => SqlData.Instance;

        // ─── contarStock ──────────────────────────────────────────────────────

        /// <summary>
        /// Equivalente a contarStock(codigo, fechaFinal).
        /// Calcula el stock de un artículo hasta una fecha dada,
        /// considerando SOLO movimientos con estado "entregado".
        ///
        /// Stock = apertura + entradas + compras - salidas - ventas
        /// </summary>
        public static double ContarStock(string codigo, DateTime fechaFinal)
        {
            double aperturas = 0, entradas = 0, ventas = 0, salidas = 0, compras = 0;
            DateTime fechaInicio = AppState.DataFechaInicio;

            // ── Apertura ─────────────────────────────────────────────────────
            foreach (var item in AppState.AperturaActiva)
            {
                if (item == null) continue;
                if (item.ArticuloId == codigo)
                    aperturas += item.Cantidad;
            }

            // ── Ventas y compras (pedidos con estado "entregado") ────────────
            int ufPedidos = Sql.PedidosObj.ContarFilas;
            for (int ciclo = 1; ciclo <= ufPedidos; ciclo++)
            {
                var idObj = Sql.PedidosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                var docPIdObj = Sql.PedidosObj.ObtenerItem("documentoP", id);
                if (docPIdObj == null) continue;
                string documentoP = docPIdObj.ToString()!;

                var fechaObj = Sql.DocumentosPObj.ObtenerItem("fecha", documentoP);
                if (fechaObj == null) continue;
                DateTime fecha = Convert.ToDateTime(fechaObj);

                if (fecha < fechaInicio || fecha > fechaFinal) continue;

                var articuloObj = Sql.PedidosObj.ObtenerItem("articulo", id);
                if (articuloObj?.ToString() != codigo) continue;

                string movimiento = Sql.DocumentosPObj.ObtenerItem("movimiento", documentoP)?.ToString()?.ToLower() ?? "";
                string estado     = Sql.DocumentosPObj.ObtenerItem("estado",     documentoP)?.ToString() ?? "";

                if (estado != "entregado") continue;

                double cantidad = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad", id) ?? 0);

                if (movimiento == "venta")  ventas  += cantidad;
                if (movimiento == "compra") compras += cantidad;
            }

            // ── Traspasos (entradas y salidas) ───────────────────────────────
            long sucursal = AppState.SucursalActiva;
            int ufTrasp = Sql.TraspasosObj.ContarFilas;

            for (int ciclo = 1; ciclo <= ufTrasp; ciclo++)
            {
                var idObj = Sql.TraspasosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                var docTIdObj = Sql.TraspasosObj.ObtenerItem("documentoT", id);
                if (docTIdObj == null) continue;
                string documentoT = docTIdObj.ToString()!;

                var fechaObj = Sql.DocumentosTObj.ObtenerItem("fecha", documentoT);
                if (fechaObj == null) continue;
                DateTime fecha = Convert.ToDateTime(fechaObj);

                if (fecha < fechaInicio || fecha > fechaFinal) continue;

                var articuloObj = Sql.TraspasosObj.ObtenerItem("articulo", id);
                if (articuloObj?.ToString() != codigo) continue;

                long origen  = Convert.ToInt64(Sql.DocumentosTObj.ObtenerItem("origen",  documentoT) ?? 0L);
                long destino = Convert.ToInt64(Sql.DocumentosTObj.ObtenerItem("destino", documentoT) ?? 0L);
                double cantidad = Convert.ToDouble(Sql.TraspasosObj.ObtenerItem("cantidad", id) ?? 0);

                if (origen == sucursal && destino != sucursal) salidas  += cantidad;
                if (destino == sucursal && origen != sucursal) entradas += cantidad;
            }

            return (aperturas + entradas + compras) - (salidas + ventas);
        }

        // ─── contarStock2 ─────────────────────────────────────────────────────

        /// <summary>
        /// Equivalente a contarStock2(codigo, fechaFinal).
        /// Igual que ContarStock pero SIN filtrar por estado "entregado"
        /// (cuenta todos los movimientos independientemente del estado).
        /// </summary>
        public static double ContarStock2(string codigo, DateTime fechaFinal)
        {
            double aperturas = 0, entradas = 0, ventas = 0, salidas = 0, compras = 0;
            DateTime fechaInicio = AppState.DataFechaInicio;

            // ── Apertura ─────────────────────────────────────────────────────
            foreach (var item in AppState.AperturaActiva)
            {
                if (item == null) continue;
                if (item.ArticuloId == codigo)
                    aperturas += item.Cantidad;
            }

            // ── Ventas y compras (todos los movimientos, sin filtro de estado) ─
            int ufPedidos = Sql.PedidosObj.ContarFilas;
            for (int ciclo = 1; ciclo <= ufPedidos; ciclo++)
            {
                var idObj = Sql.PedidosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                var docPIdObj = Sql.PedidosObj.ObtenerItem("documentoP", id);
                if (docPIdObj == null) continue;
                string documentoP = docPIdObj.ToString()!;

                var fechaObj = Sql.DocumentosPObj.ObtenerItem("fecha", documentoP);
                if (fechaObj == null) continue;
                DateTime fecha = Convert.ToDateTime(fechaObj);

                if (fecha < fechaInicio || fecha > fechaFinal) continue;

                var articuloObj = Sql.PedidosObj.ObtenerItem("articulo", id);
                if (articuloObj?.ToString() != codigo) continue;

                string movimiento = Sql.DocumentosPObj.ObtenerItem("movimiento", documentoP)?.ToString()?.ToLower() ?? "";
                // ← Sin filtro de estado (diferencia clave con ContarStock)
                double cantidad = Convert.ToDouble(Sql.PedidosObj.ObtenerItem("cantidad", id) ?? 0);

                if (movimiento == "venta")  ventas  += cantidad;
                if (movimiento == "compra") compras += cantidad;
            }

            // ── Traspasos ────────────────────────────────────────────────────
            long sucursal = AppState.SucursalActiva;
            int ufTrasp = Sql.TraspasosObj.ContarFilas;

            for (int ciclo = 1; ciclo <= ufTrasp; ciclo++)
            {
                var idObj = Sql.TraspasosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                var docTIdObj = Sql.TraspasosObj.ObtenerItem("documentoT", id);
                if (docTIdObj == null) continue;
                string documentoT = docTIdObj.ToString()!;

                var fechaObj = Sql.DocumentosTObj.ObtenerItem("fecha", documentoT);
                if (fechaObj == null) continue;
                DateTime fecha = Convert.ToDateTime(fechaObj);

                if (fecha < fechaInicio || fecha > fechaFinal) continue;

                var articuloObj = Sql.TraspasosObj.ObtenerItem("articulo", id);
                if (articuloObj?.ToString() != codigo) continue;

                long origen  = Convert.ToInt64(Sql.DocumentosTObj.ObtenerItem("origen",  documentoT) ?? 0L);
                long destino = Convert.ToInt64(Sql.DocumentosTObj.ObtenerItem("destino", documentoT) ?? 0L);
                double cantidad = Convert.ToDouble(Sql.TraspasosObj.ObtenerItem("cantidad", id) ?? 0);

                if (origen == sucursal && destino != sucursal) salidas  += cantidad;
                if (destino == sucursal && origen != sucursal) entradas += cantidad;
            }

            return (aperturas + entradas + compras) - (salidas + ventas);
        }
    }

    // ─── Helpers de UI ────────────────────────────────────────────────────────

    /// <summary>
    /// Equivalente a DesactivarControlesEnFormulario / ActivarControlesEnFormulario.
    /// En WPF se usa IsEnabled en lugar del .Enabled de VBA.
    /// </summary>
    public static class UiHelpers
    {
        /// <summary>
        /// Equivalente a DesactivarControlesEnFormulario(frm).
        /// Desactiva todos los controles directos de un Panel/Grid/StackPanel, etc.
        /// </summary>
        public static void DesactivarControles(System.Windows.Controls.Panel panel)
        {
            foreach (UIElement elemento in panel.Children)
                elemento.IsEnabled = false;
        }

        /// <summary>
        /// Equivalente a ActivarControlesEnFormulario(frm).
        /// </summary>
        public static void ActivarControles(System.Windows.Controls.Panel panel)
        {
            foreach (UIElement elemento in panel.Children)
                elemento.IsEnabled = true;
        }

        /// <summary>
        /// Versión más potente: recorre TODOS los controles del árbol visual (recursivo).
        /// Útil cuando los controles están anidados en varios paneles.
        /// </summary>
        public static void EstablecerControles(System.Windows.DependencyObject padre, bool habilitado)
        {
            int hijos = System.Windows.Media.VisualTreeHelper.GetChildrenCount(padre);
            for (int i = 0; i < hijos; i++)
            {
                var hijo = System.Windows.Media.VisualTreeHelper.GetChild(padre, i);
                if (hijo is UIElement ui)
                    ui.IsEnabled = habilitado;
                EstablecerControles(hijo, habilitado); // recursión
            }
        }
    }
}
