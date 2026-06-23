using System;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WpfAppVba.Data
{
    /// <summary>
    /// Recalcula el importe de los pedidos de tipo "automatico" según la lista de
    /// precios vigente a la fecha del documento (documentosP.fecha): para cada
    /// pedido se toma, por artículo, el precio de la lista (documentosL/precios)
    /// con estado distinto de "pendiente" y fecha más reciente que no supere la
    /// fecha del documento — mismo criterio que ObtenerPrecioArticulo en
    /// PedidosDetalle. Los pedidos de tipo "manual" no se tocan. Si un pedido no
    /// tiene ninguna lista de precios aplicable (ninguna lista con fecha anterior
    /// o igual a la del documento), su importe queda sin modificar.
    /// Trabaja directamente sobre SQL Server, en una única transacción.
    /// </summary>
    public static class PedidosPrecioActualizador
    {
        public static string ActualizarImportesAutomaticos()
        {
            var conn = DatabaseConnection.ObtenerConexion();

            using var tx = conn.BeginTransaction();
            try
            {
                int sinPrecio    = ContarSinPrecio(conn, tx);
                int actualizados = ActualizarImportes(conn, tx);

                tx.Commit();

                var resumen = new StringBuilder();
                resumen.AppendLine($"Pedidos actualizados: {actualizados}");
                resumen.AppendLine($"Pedidos sin lista de precios aplicable (sin cambios): {sinPrecio}");
                return resumen.ToString().TrimEnd();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ─── UPDATE: importe = precio vigente × cantidad ─────────────────────
        private static int ActualizarImportes(SqlConnection conn, SqlTransaction tx)
        {
            string sql =
                "UPDATE p " +
                "SET p.importe = ap.precio * p.cantidad " +
                "FROM pedidos AS p " +
                "INNER JOIN documentosP AS dp ON dp.id = p.documentoP " +
                "CROSS APPLY (" +
                "    SELECT TOP 1 pr.precio " +
                "    FROM precios AS pr " +
                "    INNER JOIN documentosL AS dl ON dl.id = pr.documentoL " +
                "    WHERE pr.articulo = p.articulo " +
                "      AND dl.estado <> 'pendiente' " +
                "      AND dl.fecha <= dp.fecha " +
                "    ORDER BY dl.fecha DESC" +
                ") AS ap " +
                "WHERE p.tipo = 'automatico';";
            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            return cmd.ExecuteNonQuery();
        }

        // ─── Pedidos automáticos sin ninguna lista de precios aplicable ──────
        private static int ContarSinPrecio(SqlConnection conn, SqlTransaction tx)
        {
            string sql =
                "SELECT COUNT(*) " +
                "FROM pedidos AS p " +
                "INNER JOIN documentosP AS dp ON dp.id = p.documentoP " +
                "WHERE p.tipo = 'automatico' " +
                "  AND NOT EXISTS (" +
                "      SELECT 1 FROM precios AS pr " +
                "      INNER JOIN documentosL AS dl ON dl.id = pr.documentoL " +
                "      WHERE pr.articulo = p.articulo " +
                "        AND dl.estado <> 'pendiente' " +
                "        AND dl.fecha <= dp.fecha" +
                "  );";
            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
