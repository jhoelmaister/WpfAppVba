using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using WpfAppVba.Data;

namespace VisorEmpresa
{
    // ─── Modelos de resultados ────────────────────────────────────────────────

    /// <summary>
    /// Una fila agregada de movimiento de pedidos: unidades por
    /// sucursal + movimiento (venta/compra) + mes + categoría.
    /// </summary>
    public class MovimientoFila
    {
        public string SucursalId { get; set; } = "";
        public string Sucursal   { get; set; } = "";
        public string Movimiento { get; set; } = "";   // "venta" / "compra" (normalizado)
        public int    Mes        { get; set; }
        public string Categoria  { get; set; } = "";
        public double Cantidad   { get; set; }
    }

    /// <summary>Datos del usuario autenticado que necesita el visor.</summary>
    public class UsuarioVisor
    {
        public string Id      { get; set; } = "";
        public string Tipo    { get; set; } = "";
        public string Empresa { get; set; } = "";
        public string TemaC   { get; set; } = "";
    }

    /// <summary>
    /// Consultas SQL agregadas a nivel de EMPRESA (todas las sucursales, sin el
    /// filtro por sucursal activa de la app principal), parametrizadas y de solo
    /// lectura. Mismo patrón que PreciosDetalle.CalcularStockEmpresa: agregación
    /// directa en SQL, sin pasar por las cachés de SqlData.
    /// </summary>
    public static class ConsultasEmpresa
    {
        // Empresa vacía: GUID nulo válido para que la comparación contra la columna
        // uniqueidentifier no falle y devuelva 0 filas (mismo criterio que AppLoader
        // con la sucursal activa).
        private const string GuidNulo = "00000000-0000-0000-0000-000000000000";

        private static string EmpresaSegura(string empresa) =>
            string.IsNullOrEmpty(empresa) ? GuidNulo : empresa;

        // ─── Login (solo lectura) ─────────────────────────────────────────────
        /// <summary>
        /// Valida credenciales con una consulta puntual, sin descargar la tabla de
        /// usuarios y SIN escribir en la base. A diferencia de AppLoader.ValidarLogin,
        /// una contraseña antigua en texto plano que coincide se acepta pero NO se
        /// re-hashea: la migración queda a cargo de la app principal.
        /// Devuelve null si la cuenta no existe o la contraseña no coincide.
        /// </summary>
        public static UsuarioVisor? ValidarLogin(string cuenta, string contrasena)
        {
            var conn = DatabaseConnection.ObtenerConexion();

            string id = "", llave = "", tipo = "", empresa = "", temaC = "";
            using (var cmd = new SqlCommand(
                "SELECT id, llave, tipo, empresa, temaC FROM usuarios " +
                "WHERE cuenta = @cuenta AND estadof = 'normal'", conn))
            {
                cmd.Parameters.AddWithValue("@cuenta", cuenta);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;
                id      = Texto(reader["id"]);
                llave   = Texto(reader["llave"]);
                tipo    = Texto(reader["tipo"]);
                empresa = Texto(reader["empresa"]);
                temaC   = Texto(reader["temaC"]);
            }

            if (string.IsNullOrEmpty(id)) return null;

            bool valida = PasswordHasher.Verificar(contrasena, llave)
                          || (!PasswordHasher.EsHash(llave) && llave == contrasena);
            if (!valida) return null;

            return new UsuarioVisor { Id = id, Tipo = tipo, Empresa = empresa, TemaC = temaC };
        }

        // ─── Catálogos mínimos ────────────────────────────────────────────────

        /// <summary>Empresas activas (para el selector del encabezado).</summary>
        public static List<(string Id, string Descripcion)> CargarEmpresas()
        {
            var lista = new List<(string, string)>();
            var tabla = EjecutarConsulta(
                "SELECT id, descripcion FROM empresas " +
                "WHERE estadof = 'normal' ORDER BY secuencia ASC");
            foreach (DataRow fila in tabla.Rows)
                lista.Add((Texto(fila["id"]), Texto(fila["descripcion"])));
            return lista;
        }

        /// <summary>
        /// Años con documentos de pedidos en la empresa (descendente). Siempre
        /// incluye el año actual aunque todavía no tenga documentos.
        /// </summary>
        public static List<int> CargarAnios(string empresa)
        {
            var anios = new List<int>();
            var tabla = EjecutarConsulta(
                "SELECT DISTINCT YEAR(vg.fecha) AS anio " +
                "FROM documentosP vg " +
                "INNER JOIN sucursales s ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp " +
                "ORDER BY anio DESC",
                ("@emp", EmpresaSegura(empresa)));
            foreach (DataRow fila in tabla.Rows)
            {
                int anio = Entero(fila["anio"]);
                if (anio > 0) anios.Add(anio);
            }

            int actual = DateTime.Now.Year;
            if (!anios.Contains(actual))
            {
                anios.Add(actual);
                anios.Sort((a, b) => b.CompareTo(a));   // mantener orden descendente
            }
            return anios;
        }

        // ─── Movimientos agregados del año ────────────────────────────────────

        /// <summary>
        /// Unidades de pedidos de TODA la empresa en el año, agrupadas por
        /// sucursal + movimiento + mes + categoría. Una sola consulta alimenta los
        /// KPIs, el gráfico mensual, el desglose por categoría y la comparativa
        /// por sucursal.
        /// </summary>
        public static List<MovimientoFila> CargarMovimientos(string empresa, int anio)
        {
            var (desde, hasta) = RangoAnio(anio);
            var lista = new List<MovimientoFila>();

            var tabla = EjecutarConsulta(
                "SELECT s.id AS sucursalId, s.descripcion AS sucursal, vg.movimiento, " +
                "       MONTH(vg.fecha) AS mes, " +
                "       ISNULL(c.descripcion, N'Sin categoría') AS categoria, " +
                "       SUM(vd.cantidad) AS cantidad " +
                "FROM pedidos vd " +
                "INNER JOIN documentosP vg ON vd.documentoP = vg.id " +
                "INNER JOIN sucursales s   ON s.id = vg.sucursal " +
                "LEFT JOIN articulos a     ON a.id = vd.articulo " +
                "LEFT JOIN categorias c    ON c.id = a.categoria " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "GROUP BY s.id, s.descripcion, vg.movimiento, MONTH(vg.fecha), " +
                "         ISNULL(c.descripcion, N'Sin categoría')",
                ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta));

            foreach (DataRow fila in tabla.Rows)
            {
                lista.Add(new MovimientoFila
                {
                    SucursalId = Texto(fila["sucursalId"]),
                    Sucursal   = Texto(fila["sucursal"]),
                    Movimiento = Texto(fila["movimiento"]).Trim().ToLowerInvariant(),
                    Mes        = Entero(fila["mes"]),
                    Categoria  = Texto(fila["categoria"]),
                    Cantidad   = Numero(fila["cantidad"])
                });
            }
            return lista;
        }

        /// <summary>
        /// Contadores por movimiento (venta/compra): documentos distintos y
        /// artículos distintos del año en toda la empresa.
        /// </summary>
        public static Dictionary<string, (int Documentos, int Articulos)> CargarResumenPedidos(
            string empresa, int anio)
        {
            var (desde, hasta) = RangoAnio(anio);
            var resumen = new Dictionary<string, (int, int)>();

            var tabla = EjecutarConsulta(
                "SELECT vg.movimiento, " +
                "       COUNT(DISTINCT vg.id) AS documentos, " +
                "       COUNT(DISTINCT vd.articulo) AS articulos " +
                "FROM pedidos vd " +
                "INNER JOIN documentosP vg ON vd.documentoP = vg.id " +
                "INNER JOIN sucursales s   ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "GROUP BY vg.movimiento",
                ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta));

            foreach (DataRow fila in tabla.Rows)
            {
                string mov = Texto(fila["movimiento"]).Trim().ToLowerInvariant();
                if (mov == "") continue;
                resumen[mov] = (Entero(fila["documentos"]), Entero(fila["articulos"]));
            }
            return resumen;
        }

        /// <summary>
        /// Traspasos internos del año: unidades y documentos donde el origen o el
        /// destino pertenece a la empresa. A nivel de empresa las "entradas" y
        /// "salidas" se cancelan entre sí, por eso se muestran como un único
        /// total de movimiento interno (se excluyen auto-traspasos origen=destino).
        /// </summary>
        public static (double Unidades, int Documentos) CargarTraspasosInternos(
            string empresa, int anio)
        {
            var (desde, hasta) = RangoAnio(anio);

            var tabla = EjecutarConsulta(
                "SELECT ISNULL(SUM(vd.cantidad), 0) AS cantidad, " +
                "       COUNT(DISTINCT vg.id) AS documentos " +
                "FROM traspasos vd " +
                "INNER JOIN documentosT vg ON vd.documentoT = vg.id " +
                "WHERE vg.estadof = 'normal' " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "AND vg.origen <> vg.destino " +
                "AND (EXISTS (SELECT 1 FROM sucursales so WHERE so.id = vg.origen  AND so.estadof = 'normal' AND so.empresa = @emp) " +
                "  OR EXISTS (SELECT 1 FROM sucursales sd WHERE sd.id = vg.destino AND sd.estadof = 'normal' AND sd.empresa = @emp))",
                ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta));

            if (tabla.Rows.Count == 0) return (0, 0);
            var r = tabla.Rows[0];
            return (Numero(r["cantidad"]), Entero(r["documentos"]));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static (DateTime Desde, DateTime Hasta) RangoAnio(int anio) =>
            (new DateTime(anio, 1, 1, 0, 0, 0), new DateTime(anio, 12, 31, 23, 59, 59));

        private static DataTable EjecutarConsulta(
            string sql, params (string Nombre, object Valor)[] parametros)
        {
            var tabla = new DataTable();
            var conn  = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(sql, conn);
            foreach (var (nombre, valor) in parametros)
                cmd.Parameters.AddWithValue(nombre, valor);
            using var adaptador = new SqlDataAdapter(cmd);
            adaptador.Fill(tabla);
            return tabla;
        }

        private static string Texto(object? v) =>
            v is null or DBNull ? "" : v.ToString() ?? "";

        private static double Numero(object? v) =>
            v is null or DBNull ? 0 : Convert.ToDouble(v);

        private static int Entero(object? v) =>
            v is null or DBNull ? 0 : Convert.ToInt32(v);
    }
}
