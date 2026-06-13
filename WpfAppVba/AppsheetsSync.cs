using System;
using Microsoft.Data.SqlClient;

namespace WpfAppVba.Data
{
    /// <summary>
    /// Sincroniza la tabla <c>appsheets</c> con los artículos de la empresa activa,
    /// para la sucursal activa. Trabaja directamente sobre SQL Server (la tabla
    /// appsheets no forma parte del caché de <see cref="SqlData"/>).
    ///
    /// Reglas (todo filtrado por sucursal + empresa activa):
    ///   • INSERT: una fila por cada artículo activo (estadof='normal', empresa
    ///     activa) que aún NO tenga una fila 'normal' en appsheets para la
    ///     sucursal activa. Rellena articulo, indice (de articulos.indice),
    ///     sucursal, empresa, usuario, emision y estadof='normal'. Los que ya
    ///     existen NO se tocan.
    ///   • UPDATE: marca estadof='eliminado' las filas 'normal' de appsheets
    ///     (sucursal activa) cuyo artículo ya no exista como artículo activo
    ///     (artículo eliminado).
    /// </summary>
    public static class AppsheetsSync
    {
        /// <summary>
        /// Ejecuta la sincronización en una única transacción.
        /// Devuelve un resumen (insertados / marcados como eliminados).
        /// </summary>
        public static string Sincronizar()
        {
            string suc = AppState.SucursalActiva;
            string emp = AppState.EmpresaActiva;
            string usu = AppState.UsuarioActivo;

            if (string.IsNullOrEmpty(suc))
                throw new InvalidOperationException("No hay una sucursal activa.");
            if (string.IsNullOrEmpty(emp))
                throw new InvalidOperationException("No hay una empresa activa.");

            var conn = DatabaseConnection.ObtenerConexion();
            using var tx = conn.BeginTransaction();
            try
            {
                int insertados = InsertarFaltantes(conn, tx, suc, emp, usu);
                int eliminados = MarcarEliminados(conn, tx, suc, emp);
                tx.Commit();
                return $"Sincronización completada.\n\n" +
                       $"Artículos agregados: {insertados}\n" +
                       $"Marcados como eliminados: {eliminados}";
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ─── INSERT: artículos activos sin fila 'normal' en appsheets ─────────
        private static int InsertarFaltantes(SqlConnection conn, SqlTransaction tx,
                                             string suc, string emp, string usu)
        {
            const string sql =
                "INSERT INTO appsheets (id, indice, articulo, sucursal, empresa, usuario, emision, estadof) " +
                "SELECT NEWID(), a.indice, a.id, @suc, @emp, @usu, GETDATE(), 'normal' " +
                "FROM articulos AS a " +
                "WHERE a.estadof = 'normal' AND a.empresa = @emp " +
                "  AND NOT EXISTS (" +
                "      SELECT 1 FROM appsheets AS ap " +
                "      WHERE ap.articulo = a.id AND ap.sucursal = @suc AND ap.estadof = 'normal'" +
                "  );";

            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            cmd.Parameters.AddWithValue("@suc", new Guid(suc));
            cmd.Parameters.AddWithValue("@emp", new Guid(emp));
            // usuario puede no ser un GUID válido en estados degenerados → NULL.
            cmd.Parameters.AddWithValue("@usu",
                Guid.TryParse(usu, out var gUsu) ? gUsu : (object)DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        // ─── UPDATE: filas 'normal' cuyo artículo ya no está activo ───────────
        private static int MarcarEliminados(SqlConnection conn, SqlTransaction tx,
                                            string suc, string emp)
        {
            const string sql =
                "UPDATE ap SET ap.estadof = 'eliminado' " +
                "FROM appsheets AS ap " +
                "WHERE ap.sucursal = @suc AND ap.empresa = @emp AND ap.estadof = 'normal' " +
                "  AND NOT EXISTS (" +
                "      SELECT 1 FROM articulos AS a " +
                "      WHERE a.id = ap.articulo AND a.estadof = 'normal' AND a.empresa = @emp" +
                "  );";

            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            cmd.Parameters.AddWithValue("@suc", new Guid(suc));
            cmd.Parameters.AddWithValue("@emp", new Guid(emp));
            return cmd.ExecuteNonQuery();
        }
    }
}
