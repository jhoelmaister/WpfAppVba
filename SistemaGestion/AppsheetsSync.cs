using System;
using Microsoft.Data.SqlClient;

namespace SistemaGestion.Data
{
    /// <summary>
    /// Sincroniza la tabla <c>appsheets</c> con los artículos de la empresa activa.
    /// Trabaja directamente sobre SQL Server (la tabla appsheets no forma parte del
    /// caché de <see cref="SqlData"/>). Procesa TODAS las sucursales (estadof='normal')
    /// de la empresa activa.
    ///
    /// Reglas (por cada sucursal de la empresa):
    ///   • INSERT: una fila por cada artículo activo (estadof='normal', empresa activa)
    ///     que aún NO tenga una fila 'normal' en appsheets para esa sucursal. Los que ya
    ///     existen NO se tocan.
    ///   • UPDATE: marca estadof='eliminado' las filas 'normal' de appsheets cuyo artículo
    ///     ya no exista como artículo activo (artículo eliminado).
    /// </summary>
    public static class AppsheetsSync
    {
        /// <summary>
        /// Sincroniza todas las sucursales (estadof='normal') de la empresa activa.
        /// Devuelve un resumen.
        /// </summary>
        public static string SincronizarTodasLasSucursales()
        {
            string emp = AppState.EmpresaActiva;
            string usu = AppState.UsuarioActivo;

            if (string.IsNullOrEmpty(emp))
                throw new InvalidOperationException("No hay una empresa activa.");

            var conn = DatabaseConnection.ObtenerConexion();
            using var tx = conn.BeginTransaction();
            try
            {
                int insertados = InsertarFaltantes(conn, tx, emp, usu);
                int eliminados = MarcarEliminados(conn, tx, emp);
                tx.Commit();
                return Resumen(insertados, eliminados);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static string Resumen(int insertados, int eliminados) =>
            "Sincronización completada (todas las sucursales de la empresa).\n\n" +
            $"Artículos agregados: {insertados}\n" +
            $"Marcados como eliminados: {eliminados}";

        // ─── INSERT: artículos activos sin fila 'normal' en appsheets ─────────
        // Cubre todas las sucursales 'normal' de la empresa (CROSS JOIN).
        private static int InsertarFaltantes(SqlConnection conn, SqlTransaction tx,
                                             string emp, string usu)
        {
            string sql =
                "INSERT INTO appsheets (id, articulo, sucursal, empresa, usuario, emision, estadof) " +
                "SELECT NEWID(), a.id, s.id, @emp, @usu, GETDATE(), 'normal' " +
                "FROM articulos AS a " +
                "CROSS JOIN sucursales AS s " +
                "WHERE a.estadof = 'normal' " +
                "AND a.familia IN (SELECT f.id FROM familias AS f " +
                "                  INNER JOIN productos AS p ON f.producto = p.id " +
                "                  WHERE f.estadof = 'normal' AND p.estadof = 'normal' AND p.empresa = @emp) " +
                "AND s.estadof = 'normal' AND s.empresa = @emp " +
                "  AND NOT EXISTS (" +
                "      SELECT 1 FROM appsheets AS ap " +
                "      WHERE ap.articulo = a.id AND ap.sucursal = s.id AND ap.estadof = 'normal'" +
                "  );";

            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            cmd.Parameters.AddWithValue("@emp", new Guid(emp));
            cmd.Parameters.AddWithValue("@usu",
                Guid.TryParse(usu, out var gUsu) ? gUsu : (object)DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        // ─── UPDATE: filas 'normal' cuyo artículo ya no está activo ───────────
        private static int MarcarEliminados(SqlConnection conn, SqlTransaction tx, string emp)
        {
            string sql =
                "UPDATE ap SET ap.estadof = 'eliminado' " +
                "FROM appsheets AS ap " +
                "WHERE ap.empresa = @emp AND ap.estadof = 'normal' " +
                "AND ap.sucursal IN (SELECT id FROM sucursales WHERE empresa = @emp AND estadof = 'normal') " +
                "  AND NOT EXISTS (" +
                "      SELECT 1 FROM articulos AS a " +
                "      WHERE a.id = ap.articulo AND a.estadof = 'normal' " +
                "      AND a.familia IN (SELECT f.id FROM familias AS f " +
                "                        INNER JOIN productos AS p ON f.producto = p.id " +
                "                        WHERE f.estadof = 'normal' AND p.estadof = 'normal' AND p.empresa = @emp)" +
                "  );";

            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            cmd.Parameters.AddWithValue("@emp", new Guid(emp));
            return cmd.ExecuteNonQuery();
        }
    }
}
