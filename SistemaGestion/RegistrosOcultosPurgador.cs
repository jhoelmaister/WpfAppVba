using System;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SistemaGestion.Data
{
    /// <summary>
    /// Borra FÍSICAMENTE (DELETE real, no vía <c>estadof</c>) las filas con
    /// <c>estadof</c> en ('ocultado', 'eliminado') de la EMPRESA ACTIVA
    /// (<see cref="AppState.EmpresaActiva"/>), en las 26 tablas de la base, y
    /// luego renumera 1..N (sin huecos) la columna <c>indice</c> de las tablas
    /// que la tienen, agrupada por su documento/familia dueño.
    ///   • Líneas de documentos (correcciones, entregas, facturas, inventarios,
    ///     pedidos, precios, transaccionesF/P, traspasos): filtradas a la
    ///     empresa activa vía su documento cabecera → sucursal (o emitido) →
    ///     sucursales.empresa (documentosL/precios vía documentosL.empresa,
    ///     columna directa).
    ///   • Cabeceras (documentosC/F/I/L/P/T): mismo filtro sucursal → empresa.
    ///   • Maestras con columna 'empresa' directa (categorias, industrias,
    ///     productos, regiones, sucursales, terceros, usuarios, appsheets):
    ///     filtradas directo.
    ///   • familias: vía su producto → productos.empresa. articulos: vía su
    ///     categoria → categorias.empresa.
    ///   • empresas: solo la fila cuyo id = empresa activa (caso límite: una
    ///     empresa activa normalmente no puede estar oculta).
    /// Es la única operación de la app que hace DELETE físico — el resto usa
    /// borrado lógico (ver DataConsulta.ExportarItemsInterno). Irreversible:
    /// no hay forma de recuperar las filas borradas.
    /// </summary>
    public static class RegistrosOcultosPurgador
    {
        private const string EstadosOcultos = "('ocultado', 'eliminado')";

        /// <summary>
        /// Purga la empresa activa en una única transacción. Devuelve un
        /// resumen (tabla → filas afectadas).
        /// </summary>
        public static string PurgarTodos()
        {
            string empresaId = AppState.EmpresaActiva;
            if (string.IsNullOrEmpty(empresaId))
                throw new InvalidOperationException(
                    "No hay una empresa activa: inicia sesión antes de purgar registros ocultos.");

            var conn = DatabaseConnection.ObtenerConexion();
            var resumen = new StringBuilder();

            using var tx = conn.BeginTransaction();
            try
            {
                // ── 1) Líneas de documentos (antes que sus cabeceras) ───────────
                resumen.AppendLine($"correcciones: {Ejecutar(conn, tx,
                    "DELETE c FROM correcciones AS c " +
                    "INNER JOIN documentosC AS d ON d.id = c.documentoC " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND c.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"entregas: {Ejecutar(conn, tx,
                    "DELETE e FROM entregas AS e " +
                    "INNER JOIN documentosP AS d ON d.id = e.documentoP " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND e.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"facturas: {Ejecutar(conn, tx,
                    "DELETE f FROM facturas AS f " +
                    "INNER JOIN documentosF AS d ON d.id = f.documentoF " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND f.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"inventarios: {Ejecutar(conn, tx,
                    "DELETE i FROM inventarios AS i " +
                    "INNER JOIN documentosI AS d ON d.id = i.documentoI " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND i.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"pedidos: {Ejecutar(conn, tx,
                    "DELETE p FROM pedidos AS p " +
                    "INNER JOIN documentosP AS d ON d.id = p.documentoP " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND p.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"precios: {Ejecutar(conn, tx,
                    "DELETE p FROM precios AS p " +
                    "INNER JOIN documentosL AS d ON d.id = p.documentoL " +
                    $"WHERE d.empresa = @emp AND p.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"transaccionesF: {Ejecutar(conn, tx,
                    "DELETE t FROM transaccionesF AS t " +
                    "INNER JOIN documentosF AS d ON d.id = t.documentoF " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND t.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"transaccionesP: {Ejecutar(conn, tx,
                    "DELETE t FROM transaccionesP AS t " +
                    "INNER JOIN documentosP AS d ON d.id = t.documentoP " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND t.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"traspasos: {Ejecutar(conn, tx,
                    "DELETE t FROM traspasos AS t " +
                    "INNER JOIN documentosT AS d ON d.id = t.documentoT " +
                    "INNER JOIN sucursales AS s ON s.id = d.emitido " +
                    $"WHERE s.empresa = @emp AND t.estadof IN {EstadosOcultos};", empresaId)}");

                // ── 2) Cabeceras de documentos ───────────────────────────────────
                resumen.AppendLine($"documentosC: {Ejecutar(conn, tx,
                    "DELETE d FROM documentosC AS d " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND d.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"documentosF: {Ejecutar(conn, tx,
                    "DELETE d FROM documentosF AS d " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND d.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"documentosI: {Ejecutar(conn, tx,
                    "DELETE d FROM documentosI AS d " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND d.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"documentosL: {Ejecutar(conn, tx,
                    $"DELETE FROM documentosL WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"documentosP: {Ejecutar(conn, tx,
                    "DELETE d FROM documentosP AS d " +
                    "INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    $"WHERE s.empresa = @emp AND d.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"documentosT: {Ejecutar(conn, tx,
                    "DELETE d FROM documentosT AS d " +
                    "INNER JOIN sucursales AS s ON s.id = d.emitido " +
                    $"WHERE s.empresa = @emp AND d.estadof IN {EstadosOcultos};", empresaId)}");

                // ── 3) Maestras de la empresa ─────────────────────────────────────
                resumen.AppendLine($"articulos: {Ejecutar(conn, tx,
                    "DELETE a FROM articulos AS a " +
                    "INNER JOIN categorias AS c ON c.id = a.categoria " +
                    $"WHERE c.empresa = @emp AND a.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"categorias: {Ejecutar(conn, tx,
                    $"DELETE FROM categorias WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"familias: {Ejecutar(conn, tx,
                    "DELETE f FROM familias AS f " +
                    "INNER JOIN productos AS p ON p.id = f.producto " +
                    $"WHERE p.empresa = @emp AND f.estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"industrias: {Ejecutar(conn, tx,
                    $"DELETE FROM industrias WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"productos: {Ejecutar(conn, tx,
                    $"DELETE FROM productos WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"regiones: {Ejecutar(conn, tx,
                    $"DELETE FROM regiones WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"sucursales: {Ejecutar(conn, tx,
                    $"DELETE FROM sucursales WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"terceros: {Ejecutar(conn, tx,
                    $"DELETE FROM terceros WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"usuarios: {Ejecutar(conn, tx,
                    $"DELETE FROM usuarios WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"appsheets: {Ejecutar(conn, tx,
                    $"DELETE FROM appsheets WHERE empresa = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                resumen.AppendLine($"empresas: {Ejecutar(conn, tx,
                    $"DELETE FROM empresas WHERE id = @emp AND estadof IN {EstadosOcultos};", empresaId)}");

                // ── 4) Renumerar 'indice' 1..N sin huecos (tablas que lo tienen) ──
                resumen.AppendLine($"articulos.indice: {Ejecutar(conn, tx,
                    ";WITH cte AS (" +
                    "  SELECT a.indice AS indice, " +
                    "         ROW_NUMBER() OVER (PARTITION BY a.familia ORDER BY a.indice, a.id) AS rn " +
                    "  FROM articulos AS a " +
                    "  INNER JOIN categorias AS c ON c.id = a.categoria " +
                    "  WHERE c.empresa = @emp" +
                    ") UPDATE cte SET indice = rn;", empresaId)}");

                resumen.AppendLine($"correcciones.indice: {Ejecutar(conn, tx,
                    ";WITH cte AS (" +
                    "  SELECT t.indice AS indice, " +
                    "         ROW_NUMBER() OVER (PARTITION BY t.documentoC ORDER BY t.indice, t.id) AS rn " +
                    "  FROM correcciones AS t " +
                    "  INNER JOIN documentosC AS d ON d.id = t.documentoC " +
                    "  INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    "  WHERE s.empresa = @emp" +
                    ") UPDATE cte SET indice = rn;", empresaId)}");

                resumen.AppendLine($"entregas.indice: {Ejecutar(conn, tx,
                    ";WITH cte AS (" +
                    "  SELECT t.indice AS indice, " +
                    "         ROW_NUMBER() OVER (PARTITION BY t.documentoP ORDER BY t.indice, t.id) AS rn " +
                    "  FROM entregas AS t " +
                    "  INNER JOIN documentosP AS d ON d.id = t.documentoP " +
                    "  INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    "  WHERE s.empresa = @emp" +
                    ") UPDATE cte SET indice = rn;", empresaId)}");

                resumen.AppendLine($"facturas.indice: {Ejecutar(conn, tx,
                    ";WITH cte AS (" +
                    "  SELECT t.indice AS indice, " +
                    "         ROW_NUMBER() OVER (PARTITION BY t.documentoF ORDER BY t.indice, t.id) AS rn " +
                    "  FROM facturas AS t " +
                    "  INNER JOIN documentosF AS d ON d.id = t.documentoF " +
                    "  INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    "  WHERE s.empresa = @emp" +
                    ") UPDATE cte SET indice = rn;", empresaId)}");

                resumen.AppendLine($"pedidos.indice: {Ejecutar(conn, tx,
                    ";WITH cte AS (" +
                    "  SELECT t.indice AS indice, " +
                    "         ROW_NUMBER() OVER (PARTITION BY t.documentoP ORDER BY t.indice, t.id) AS rn " +
                    "  FROM pedidos AS t " +
                    "  INNER JOIN documentosP AS d ON d.id = t.documentoP " +
                    "  INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    "  WHERE s.empresa = @emp" +
                    ") UPDATE cte SET indice = rn;", empresaId)}");

                resumen.AppendLine($"transaccionesF.indice: {Ejecutar(conn, tx,
                    ";WITH cte AS (" +
                    "  SELECT t.indice AS indice, " +
                    "         ROW_NUMBER() OVER (PARTITION BY t.documentoF ORDER BY t.indice, t.id) AS rn " +
                    "  FROM transaccionesF AS t " +
                    "  INNER JOIN documentosF AS d ON d.id = t.documentoF " +
                    "  INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    "  WHERE s.empresa = @emp" +
                    ") UPDATE cte SET indice = rn;", empresaId)}");

                resumen.AppendLine($"transaccionesP.indice: {Ejecutar(conn, tx,
                    ";WITH cte AS (" +
                    "  SELECT t.indice AS indice, " +
                    "         ROW_NUMBER() OVER (PARTITION BY t.documentoP ORDER BY t.indice, t.id) AS rn " +
                    "  FROM transaccionesP AS t " +
                    "  INNER JOIN documentosP AS d ON d.id = t.documentoP " +
                    "  INNER JOIN sucursales AS s ON s.id = d.sucursal " +
                    "  WHERE s.empresa = @emp" +
                    ") UPDATE cte SET indice = rn;", empresaId)}");

                resumen.AppendLine($"traspasos.indice: {Ejecutar(conn, tx,
                    ";WITH cte AS (" +
                    "  SELECT t.indice AS indice, " +
                    "         ROW_NUMBER() OVER (PARTITION BY t.documentoT ORDER BY t.indice, t.id) AS rn " +
                    "  FROM traspasos AS t " +
                    "  INNER JOIN documentosT AS d ON d.id = t.documentoT " +
                    "  INNER JOIN sucursales AS s ON s.id = d.emitido " +
                    "  WHERE s.empresa = @emp" +
                    ") UPDATE cte SET indice = rn;", empresaId)}");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return resumen.ToString().TrimEnd();
        }

        private static int Ejecutar(SqlConnection conn, SqlTransaction tx, string sql, string empresaId)
        {
            using var cmd = new SqlCommand(sql, conn, tx) { CommandTimeout = 300 };
            cmd.Parameters.AddWithValue("@emp", empresaId);
            return cmd.ExecuteNonQuery();
        }
    }
}
