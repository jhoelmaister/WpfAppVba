using System;
using Microsoft.Data.SqlClient;

namespace WpfAppVba.Data
{
    /// <summary>
    /// Sincroniza la tabla <c>appsheets</c> con los artículos de la empresa activa.
    /// Trabaja directamente sobre SQL Server (la tabla appsheets no forma parte del
    /// caché de <see cref="SqlData"/>).
    ///
    /// Alcance:
    ///   • Sucursal activa: solo procesa <see cref="AppState.SucursalActiva"/>.
    ///   • Todas las sucursales: procesa todas las sucursales (estadof='normal') de
    ///     la empresa activa.
    ///
    /// Reglas (por cada sucursal procesada):
    ///   • INSERT: una fila por cada artículo activo (estadof='normal', empresa
    ///     activa) que aún NO tenga una fila 'normal' en appsheets para esa
    ///     sucursal. Rellena articulo, sucursal, empresa, usuario, emision y
    ///     estadof='normal'. Los que ya existen NO se tocan.
    ///   • UPDATE: marca estadof='eliminado' las filas 'normal' de appsheets cuyo
    ///     artículo ya no exista como artículo activo (artículo eliminado).
    /// </summary>
    public static class AppsheetsSync
    {
        /// <summary>
        /// Sincroniza solo la sucursal activa. Devuelve un resumen.
        /// </summary>
        public static string SincronizarSucursalActiva()
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
                int insertados = InsertarFaltantes(conn, tx, emp, usu, suc);
                int eliminados = MarcarEliminados(conn, tx, emp, suc);
                tx.Commit();
                return Resumen("sucursal activa", insertados, eliminados);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

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
                int insertados = InsertarFaltantes(conn, tx, emp, usu, null);
                int eliminados = MarcarEliminados(conn, tx, emp, null);
                tx.Commit();
                return Resumen("todas las sucursales de la empresa", insertados, eliminados);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static string Resumen(string alcance, int insertados, int eliminados) =>
            $"Sincronización completada ({alcance}).\n\n" +
            $"Artículos agregados: {insertados}\n" +
            $"Marcados como eliminados: {eliminados}";

        // ─── INSERT: artículos activos sin fila 'normal' en appsheets ─────────
        // Si <paramref name="suc"/> es null → todas las sucursales 'normal' de la
        // empresa (CROSS JOIN). Si trae valor → solo esa sucursal.
        private static int InsertarFaltantes(SqlConnection conn, SqlTransaction tx,
                                             string emp, string usu, string? suc)
        {
            string sucExpr   = suc == null ? "s.id" : "@suc";
            string fromSuc   = suc == null
                ? "CROSS JOIN sucursales AS s "
                : "";
            string whereSuc  = suc == null
                ? "AND s.estadof = 'normal' AND s.empresa = @emp "
                : "";
            string existsSuc = suc == null ? "s.id" : "@suc";

            string sql =
                "INSERT INTO appsheets (id, articulo, sucursal, empresa, usuario, emision, estadof) " +
                $"SELECT NEWID(), a.id, {sucExpr}, @emp, @usu, GETDATE(), 'normal' " +
                "FROM articulos AS a " +
                fromSuc +
                "WHERE a.estadof = 'normal' AND a.empresa = @emp " +
                whereSuc +
                "  AND NOT EXISTS (" +
                "      SELECT 1 FROM appsheets AS ap " +
                $"      WHERE ap.articulo = a.id AND ap.sucursal = {existsSuc} AND ap.estadof = 'normal'" +
                "  );";

            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            cmd.Parameters.AddWithValue("@emp", new Guid(emp));
            cmd.Parameters.AddWithValue("@usu",
                Guid.TryParse(usu, out var gUsu) ? gUsu : (object)DBNull.Value);
            if (suc != null) cmd.Parameters.AddWithValue("@suc", new Guid(suc));
            return cmd.ExecuteNonQuery();
        }

        // ─── UPDATE: filas 'normal' cuyo artículo ya no está activo ───────────
        // Si <paramref name="suc"/> es null → todas las sucursales 'normal' de la
        // empresa. Si trae valor → solo esa sucursal.
        private static int MarcarEliminados(SqlConnection conn, SqlTransaction tx,
                                            string emp, string? suc)
        {
            string whereSuc = suc == null
                ? "AND ap.sucursal IN (SELECT id FROM sucursales WHERE empresa = @emp AND estadof = 'normal') "
                : "AND ap.sucursal = @suc ";

            string sql =
                "UPDATE ap SET ap.estadof = 'eliminado' " +
                "FROM appsheets AS ap " +
                "WHERE ap.empresa = @emp AND ap.estadof = 'normal' " +
                whereSuc +
                "  AND NOT EXISTS (" +
                "      SELECT 1 FROM articulos AS a " +
                "      WHERE a.id = ap.articulo AND a.estadof = 'normal' AND a.empresa = @emp" +
                "  );";

            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            cmd.Parameters.AddWithValue("@emp", new Guid(emp));
            if (suc != null) cmd.Parameters.AddWithValue("@suc", new Guid(suc));
            return cmd.ExecuteNonQuery();
        }
    }
}
