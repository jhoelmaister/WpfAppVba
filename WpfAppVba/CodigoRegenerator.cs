using System;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WpfAppVba.Data
{
    /// <summary>
    /// Regenera la columna <c>codigo</c> de las tablas del sistema:
    ///   • Maestras  → numeración secuencial 1..N.
    ///   • documentosT/I/P/C → signo de la sucursal + correlativo por sucursal.
    ///   • precios   → signo de la región + correlativo por región.
    /// Trabaja directamente sobre SQL Server y reescribe TODAS las filas.
    /// </summary>
    public static class CodigoRegenerator
    {
        // Tablas maestras → código entero secuencial.
        private static readonly string[] Maestras =
        {
            "usuarios", "familias", "productos", "Categorias", "industrias",
            "terceros", "sucursales", "regiones"
        };

        // Documentos → código = signo de la sucursal + correlativo por sucursal.
        private static readonly string[] Documentos =
        {
            "documentosT", "documentosI", "documentosP", "documentosC"
        };

        /// <summary>
        /// Regenera todos los códigos en una única transacción.
        /// Devuelve un resumen (tabla → filas afectadas).
        /// </summary>
        public static string RegenerarTodos()
        {
            var conn = DatabaseConnection.ObtenerConexion();
            var resumen = new StringBuilder();

            using var tx = conn.BeginTransaction();
            try
            {
                foreach (var t in Maestras)
                    resumen.AppendLine($"{t}: {RenumerarMaestra(conn, tx, t)}");

                foreach (var t in Documentos)
                    resumen.AppendLine($"{t}: {RenumerarDocumentoPorSucursal(conn, tx, t)}");

                resumen.AppendLine($"precios: {RenumerarPreciosPorRegion(conn, tx)}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return resumen.ToString().TrimEnd();
        }

        // ─── Maestras: codigo = 1..N (orden por id) ──────────────────────────
        private static int RenumerarMaestra(SqlConnection conn, SqlTransaction tx, string tabla)
        {
            string sql =
                $";WITH cte AS (" +
                $"  SELECT codigo, ROW_NUMBER() OVER (ORDER BY id) AS rn " +
                $"  FROM {tabla}" +
                $") UPDATE cte SET codigo = CAST(rn AS NVARCHAR(50));";
            return Ejecutar(conn, tx, sql);
        }

        // ─── Documentos: codigo = signo + correlativo por sucursal ───────────
        private static int RenumerarDocumentoPorSucursal(SqlConnection conn, SqlTransaction tx, string tabla)
        {
            string sql =
                $";WITH cte AS (" +
                $"  SELECT d.codigo AS codigo, ISNULL(s.signo, '') AS signo, " +
                $"         ROW_NUMBER() OVER (PARTITION BY d.sucursal ORDER BY d.fecha, d.id) AS rn " +
                $"  FROM {tabla} AS d " +
                $"  LEFT JOIN sucursales AS s ON s.id = d.sucursal " +
                $") UPDATE cte SET codigo = signo + CAST(rn AS NVARCHAR(50));";
            return Ejecutar(conn, tx, sql);
        }

        // ─── Precios: codigo = signo región + correlativo por región ─────────
        private static int RenumerarPreciosPorRegion(SqlConnection conn, SqlTransaction tx)
        {
            string sql =
                ";WITH cte AS (" +
                "  SELECT p.codigo AS codigo, ISNULL(r.signo, '') AS signo, " +
                "         ROW_NUMBER() OVER (PARTITION BY p.region ORDER BY p.fecha, p.id) AS rn " +
                "  FROM precios AS p " +
                "  LEFT JOIN regiones AS r ON r.id = p.region " +
                ") UPDATE cte SET codigo = signo + CAST(rn AS NVARCHAR(50));";
            return Ejecutar(conn, tx, sql);
        }

        private static int Ejecutar(SqlConnection conn, SqlTransaction tx, string sql)
        {
            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            return cmd.ExecuteNonQuery();
        }
    }
}
