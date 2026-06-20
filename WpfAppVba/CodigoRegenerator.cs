using System;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WpfAppVba.Data
{
    /// <summary>
    /// Regenera la columna <c>codigo</c> de las tablas del sistema:
    ///   • Maestras  → numeración secuencial 1..N, ordenada por 'secuencia'.
    ///   • documentosI/P/C → signo de la sucursal + correlativo por sucursal,
    ///     ordenado por fecha dentro de cada sucursal.
    ///   • documentosT (traspasos) → signo de la empresa + correlativo por empresa
    ///     (la empresa se obtiene por la cascada emitido → sucursales → empresas),
    ///     ordenado por fecha dentro de cada empresa.
    ///   • documentosL (listas de precios) → signo de la empresa + correlativo por
    ///     empresa (columna 'empresa' directa, igual criterio que documentosT),
    ///     ordenado por fecha dentro de cada empresa.
    /// Trabaja directamente sobre SQL Server y reescribe TODAS las filas.
    /// </summary>
    public static class CodigoRegenerator
    {
        // Tablas maestras → código entero secuencial, ordenadas por 'secuencia'.
        private static readonly string[] Maestras =
        {
            "usuarios", "familias", "productos", "Categorias", "industrias",
            "terceros", "sucursales", "regiones", "empresas"
        };

        // Documentos con columna 'sucursal' → signo de la sucursal + correlativo
        // por sucursal. (documentosT se trata aparte: signo/correlativo por empresa.)
        private static readonly (string tabla, string colSucursal)[] Documentos =
        {
            ("documentosI", "sucursal"),
            ("documentosP", "sucursal"),
            ("documentosC", "sucursal")
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

                foreach (var (tabla, colSucursal) in Documentos)
                    resumen.AppendLine($"{tabla}: {RenumerarDocumentoPorSucursal(conn, tx, tabla, colSucursal)}");

                resumen.AppendLine($"documentosT: {RenumerarTraspasosPorEmpresa(conn, tx)}");

                resumen.AppendLine($"documentosL: {RenumerarDocumentosLPorEmpresa(conn, tx)}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return resumen.ToString().TrimEnd();
        }

        // ─── Maestras: codigo = 1..N (orden por secuencia) ───────────────────
        private static int RenumerarMaestra(SqlConnection conn, SqlTransaction tx, string tabla)
        {
            string sql =
                $";WITH cte AS (" +
                $"  SELECT codigo, ROW_NUMBER() OVER (ORDER BY secuencia, id) AS rn " +
                $"  FROM {tabla}" +
                $") UPDATE cte SET codigo = CAST(rn AS NVARCHAR(50));";
            return Ejecutar(conn, tx, sql);
        }

        // ─── Documentos: codigo = signo + correlativo por sucursal ───────────
        // <paramref name="colSucursal"/> es la columna del documento que apunta
        // al id de la sucursal (p. ej. 'sucursal' o 'emitido').
        private static int RenumerarDocumentoPorSucursal(SqlConnection conn, SqlTransaction tx,
                                                         string tabla, string colSucursal)
        {
            string sql =
                $";WITH cte AS (" +
                $"  SELECT d.codigo AS codigo, ISNULL(UPPER(s.signo), '') AS signo, " +
                $"         ROW_NUMBER() OVER (PARTITION BY d.{colSucursal} ORDER BY d.fecha, d.id) AS rn " +
                $"  FROM {tabla} AS d " +
                $"  LEFT JOIN sucursales AS s ON s.id = d.{colSucursal} " +
                $") UPDATE cte SET codigo = signo + CAST(rn AS NVARCHAR(50));";
            return Ejecutar(conn, tx, sql);
        }

        // ─── Traspasos: codigo = signo empresa + correlativo por empresa ─────
        // La empresa se obtiene por la cascada: documentosT.emitido (sucursal) →
        // sucursales.empresa → empresas.signo. Se numera por empresa.
        private static int RenumerarTraspasosPorEmpresa(SqlConnection conn, SqlTransaction tx)
        {
            string sql =
                ";WITH cte AS (" +
                "  SELECT d.codigo AS codigo, ISNULL(UPPER(e.signo), '') AS signo, " +
                "         ROW_NUMBER() OVER (PARTITION BY e.id ORDER BY d.fecha, d.id) AS rn " +
                "  FROM documentosT AS d " +
                "  LEFT JOIN sucursales AS s ON s.id = d.emitido " +
                "  LEFT JOIN empresas   AS e ON e.id = s.empresa " +
                ") UPDATE cte SET codigo = signo + CAST(rn AS NVARCHAR(50));";
            return Ejecutar(conn, tx, sql);
        }

        // ─── DocumentosL: codigo = signo empresa + correlativo por empresa ───
        private static int RenumerarDocumentosLPorEmpresa(SqlConnection conn, SqlTransaction tx)
        {
            string sql =
                ";WITH cte AS (" +
                "  SELECT d.codigo AS codigo, ISNULL(UPPER(e.signo), '') AS signo, " +
                "         ROW_NUMBER() OVER (PARTITION BY d.empresa ORDER BY d.fecha, d.id) AS rn " +
                "  FROM documentosL AS d " +
                "  LEFT JOIN empresas AS e ON e.id = d.empresa " +
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
