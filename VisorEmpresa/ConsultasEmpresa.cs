using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
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
    /// Documento (encabezado) para las vistas de solo-visualización del visor.
    /// Modelo único para los 4 módulos: cada vista usa las columnas que le aplican
    /// (Sucursal para Pedidos/Correcciones/Facturas, Origen/Destino para Traspasos,
    /// Tercero/EstadoC/Importe para Pedidos/Facturas, Motivo para Correcciones...).
    /// </summary>
    public class DocVisorFila
    {
        public int      Num        { get; set; }
        public string   Id         { get; set; } = "";
        public string   Codigo     { get; set; } = "";
        public DateTime Fecha      { get; set; }
        public string   Sucursal   { get; set; } = "";
        public string   Origen     { get; set; } = "";
        public string   Destino    { get; set; } = "";
        public string   Movimiento { get; set; } = "";
        public string   Tercero    { get; set; } = "";
        public string   Referencia { get; set; } = "";
        public string   Motivo     { get; set; } = "";
        public string   Estado     { get; set; } = "";
        public string   EstadoC    { get; set; } = "";
        public double   Cantidad   { get; set; }
        public double   Importe    { get; set; }

        public string FechaTexto    => Fecha == default ? "" : Fecha.ToString("dd/MM/yyyy");
        public string CantidadTexto => Cantidad.ToString("N0", CultureInfo.CurrentCulture);
        public string ImporteTexto  => Importe.ToString("#,##0.##", CultureInfo.CurrentCulture);
    }

    /// <summary>Línea de un documento para la grilla inferior de las vistas.</summary>
    public class LineaVisorFila
    {
        public int    Lin         { get; set; }
        public string Codigo      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Categoria   { get; set; } = "";
        public double Cantidad    { get; set; }
        public double Importe     { get; set; }

        public string CantidadTexto => Cantidad.ToString("N0", CultureInfo.CurrentCulture);
        public string ImporteTexto  => Importe.ToString("#,##0.##", CultureInfo.CurrentCulture);
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
        /// Años con documentos (pedidos, traspasos, correcciones o facturas) en la
        /// empresa, descendente. Siempre incluye el año actual aunque esté vacío.
        /// </summary>
        public static List<int> CargarAnios(string empresa)
        {
            var anios = new List<int>();
            var tabla = EjecutarConsulta(
                "SELECT DISTINCT YEAR(vg.fecha) AS anio FROM documentosP vg " +
                "INNER JOIN sucursales s ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp " +
                "UNION " +
                "SELECT DISTINCT YEAR(vg.fecha) FROM documentosC vg " +
                "INNER JOIN sucursales s ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp " +
                "UNION " +
                "SELECT DISTINCT YEAR(vg.fecha) FROM documentosF vg " +
                "INNER JOIN sucursales s ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp " +
                "UNION " +
                "SELECT DISTINCT YEAR(vg.fecha) FROM documentosT vg " +
                "WHERE vg.estadof = 'normal' " +
                "AND (EXISTS (SELECT 1 FROM sucursales so WHERE so.id = vg.origen  AND so.estadof = 'normal' AND so.empresa = @emp) " +
                "  OR EXISTS (SELECT 1 FROM sucursales sd WHERE sd.id = vg.destino AND sd.estadof = 'normal' AND sd.empresa = @emp)) " +
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

        /// <summary>Sucursales activas de la empresa (para el combo de filtro).</summary>
        public static List<(string Id, string Descripcion)> CargarSucursalesEmpresa(string empresa)
        {
            var lista = new List<(string, string)>();
            var tabla = EjecutarConsulta(
                "SELECT id, descripcion FROM sucursales " +
                "WHERE estadof = 'normal' AND empresa = @emp ORDER BY descripcion ASC",
                ("@emp", EmpresaSegura(empresa)));
            foreach (DataRow fila in tabla.Rows)
                lista.Add((Texto(fila["id"]), Texto(fila["descripcion"])));
            return lista;
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

        // ─── Vistas de documentos (encabezados + líneas) ──────────────────────
        //
        // Reglas comunes de las 4 vistas:
        //  - Sin filtro por sucursal activa: entran TODAS las sucursales de la
        //    empresa, con filtro opcional por UNA sucursal (combo del panel).
        //  - Corte por apertura ("problema2"): cada sucursal solo aporta documentos
        //    con fecha >= MAX(documentosI.fecha) de ESA sucursal (su último
        //    inventario/apertura); si no tiene ninguno, desde la fecha de creación
        //    de la sucursal. Mismo criterio que AppState.ActualizarBase y que
        //    PreciosDetalle.CalcularStockEmpresa en la app principal (se usa >=,
        //    que es como carga la app principal el período desde la apertura).

        private const string SubconsultaApertura =
            "(SELECT sucursal, MAX(fecha) AS fecha FROM documentosI " +
            "WHERE estadof = 'normal' GROUP BY sucursal)";

        /// <summary>Encabezados de pedidos de toda la empresa (o de una sucursal).</summary>
        public static List<DocVisorFila> CargarDocsPedidos(string empresa, int anio, string sucursalId)
        {
            var (desde, hasta) = RangoAnio(anio);
            bool porSucursal = !string.IsNullOrEmpty(sucursalId);

            string sql =
                "SELECT vg.id, vg.codigo, vg.fecha, s.descripcion AS sucursal, " +
                "       ISNULL(vg.movimiento, '') AS movimiento, ISNULL(t.descripcion, '') AS tercero, " +
                "       ISNULL(vg.estado, '') AS estado, ISNULL(vg.estadoC, '') AS estadoC, " +
                "       ISNULL(SUM(vd.cantidad), 0) AS cantidad, ISNULL(SUM(vd.importe), 0) AS importe " +
                "FROM documentosP vg " +
                "INNER JOIN sucursales s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = @emp " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "LEFT JOIN terceros t ON t.id = vg.tercero " +
                "LEFT JOIN pedidos vd ON vd.documentoP = vg.id " +
                "WHERE vg.estadof = 'normal' " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha) " +
                (porSucursal ? "AND vg.sucursal = @suc " : "") +
                "GROUP BY vg.id, vg.codigo, vg.fecha, s.descripcion, vg.movimiento, t.descripcion, vg.estado, vg.estadoC " +
                "ORDER BY vg.fecha DESC";

            var pars = new List<(string, object)> { ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta) };
            if (porSucursal) pars.Add(("@suc", sucursalId));

            var lista = new List<DocVisorFila>();
            var tabla = EjecutarConsulta(sql, pars.ToArray());
            foreach (DataRow fila in tabla.Rows)
            {
                lista.Add(new DocVisorFila
                {
                    Num        = lista.Count + 1,
                    Id         = Texto(fila["id"]),
                    Codigo     = Texto(fila["codigo"]),
                    Fecha      = FechaSql(fila["fecha"]),
                    Sucursal   = Texto(fila["sucursal"]),
                    Movimiento = Texto(fila["movimiento"]),
                    Tercero    = Texto(fila["tercero"]),
                    Estado     = Texto(fila["estado"]),
                    EstadoC    = Texto(fila["estadoC"]),
                    Cantidad   = Numero(fila["cantidad"]),
                    Importe    = Numero(fila["importe"])
                });
            }
            return lista;
        }

        /// <summary>
        /// Encabezados de traspasos donde el origen o el destino pertenece a la
        /// empresa; cada lado se corta con la apertura de SU propia sucursal.
        /// </summary>
        public static List<DocVisorFila> CargarDocsTraspasos(string empresa, int anio, string sucursalId)
        {
            var (desde, hasta) = RangoAnio(anio);
            bool porSucursal = !string.IsNullOrEmpty(sucursalId);

            string condLado = porSucursal
                ? "AND ( (vg.origen = @suc AND vg.fecha >= COALESCE(apo.fecha, so.fecha)) " +
                  "   OR (vg.destino = @suc AND vg.fecha >= COALESCE(apd.fecha, sd.fecha)) ) "
                : "AND ( (so.empresa = @emp AND vg.fecha >= COALESCE(apo.fecha, so.fecha)) " +
                  "   OR (sd.empresa = @emp AND vg.fecha >= COALESCE(apd.fecha, sd.fecha)) ) ";

            string sql =
                "SELECT vg.id, vg.codigo, vg.fecha, " +
                "       ISNULL(so.descripcion, '') AS origen, ISNULL(sd.descripcion, '') AS destino, " +
                "       ISNULL(vg.estado, '') AS estado, " +
                "       ISNULL(SUM(vd.cantidad), 0) AS cantidad " +
                "FROM documentosT vg " +
                "LEFT JOIN sucursales so ON so.id = vg.origen  AND so.estadof = 'normal' " +
                "LEFT JOIN sucursales sd ON sd.id = vg.destino AND sd.estadof = 'normal' " +
                "LEFT JOIN " + SubconsultaApertura + " apo ON apo.sucursal = vg.origen " +
                "LEFT JOIN " + SubconsultaApertura + " apd ON apd.sucursal = vg.destino " +
                "LEFT JOIN traspasos vd ON vd.documentoT = vg.id " +
                "WHERE vg.estadof = 'normal' " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                condLado +
                "GROUP BY vg.id, vg.codigo, vg.fecha, so.descripcion, sd.descripcion, vg.estado " +
                "ORDER BY vg.fecha DESC";

            var pars = new List<(string, object)> { ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta) };
            if (porSucursal) pars.Add(("@suc", sucursalId));

            var lista = new List<DocVisorFila>();
            var tabla = EjecutarConsulta(sql, pars.ToArray());
            foreach (DataRow fila in tabla.Rows)
            {
                lista.Add(new DocVisorFila
                {
                    Num      = lista.Count + 1,
                    Id       = Texto(fila["id"]),
                    Codigo   = Texto(fila["codigo"]),
                    Fecha    = FechaSql(fila["fecha"]),
                    Origen   = Texto(fila["origen"]),
                    Destino  = Texto(fila["destino"]),
                    Estado   = Texto(fila["estado"]),
                    Cantidad = Numero(fila["cantidad"])
                });
            }
            return lista;
        }

        /// <summary>Encabezados de correcciones de toda la empresa (o de una sucursal).</summary>
        public static List<DocVisorFila> CargarDocsCorrecciones(string empresa, int anio, string sucursalId)
        {
            var (desde, hasta) = RangoAnio(anio);
            bool porSucursal = !string.IsNullOrEmpty(sucursalId);

            string sql =
                "SELECT vg.id, vg.codigo, vg.fecha, s.descripcion AS sucursal, " +
                "       ISNULL(vg.movimiento, '') AS movimiento, ISNULL(vg.motivo, '') AS motivo, " +
                "       ISNULL(SUM(vd.cantidad), 0) AS cantidad " +
                "FROM documentosC vg " +
                "INNER JOIN sucursales s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = @emp " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "LEFT JOIN correcciones vd ON vd.documentoC = vg.id " +
                "WHERE vg.estadof = 'normal' " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha) " +
                (porSucursal ? "AND vg.sucursal = @suc " : "") +
                "GROUP BY vg.id, vg.codigo, vg.fecha, s.descripcion, vg.movimiento, vg.motivo " +
                "ORDER BY vg.fecha DESC";

            var pars = new List<(string, object)> { ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta) };
            if (porSucursal) pars.Add(("@suc", sucursalId));

            var lista = new List<DocVisorFila>();
            var tabla = EjecutarConsulta(sql, pars.ToArray());
            foreach (DataRow fila in tabla.Rows)
            {
                lista.Add(new DocVisorFila
                {
                    Num        = lista.Count + 1,
                    Id         = Texto(fila["id"]),
                    Codigo     = Texto(fila["codigo"]),
                    Fecha      = FechaSql(fila["fecha"]),
                    Sucursal   = Texto(fila["sucursal"]),
                    Movimiento = Texto(fila["movimiento"]),
                    Motivo     = Texto(fila["motivo"]),
                    Cantidad   = Numero(fila["cantidad"])
                });
            }
            return lista;
        }

        /// <summary>Encabezados de facturas de toda la empresa (o de una sucursal).</summary>
        public static List<DocVisorFila> CargarDocsFacturas(string empresa, int anio, string sucursalId)
        {
            var (desde, hasta) = RangoAnio(anio);
            bool porSucursal = !string.IsNullOrEmpty(sucursalId);

            string sql =
                "SELECT vg.id, vg.codigo, vg.fecha, s.descripcion AS sucursal, " +
                "       ISNULL(vg.movimiento, '') AS movimiento, ISNULL(t.descripcion, '') AS tercero, " +
                "       ISNULL(vg.referencia, '') AS referencia, " +
                "       ISNULL(vg.estado, '') AS estado, ISNULL(vg.estadoC, '') AS estadoC, " +
                "       ISNULL(SUM(vd.importe), 0) AS importe " +
                "FROM documentosF vg " +
                "INNER JOIN sucursales s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = @emp " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "LEFT JOIN terceros t ON t.id = vg.tercero " +
                "LEFT JOIN facturas vd ON vd.documentoF = vg.id " +
                "WHERE vg.estadof = 'normal' " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha) " +
                (porSucursal ? "AND vg.sucursal = @suc " : "") +
                "GROUP BY vg.id, vg.codigo, vg.fecha, s.descripcion, vg.movimiento, t.descripcion, vg.referencia, vg.estado, vg.estadoC " +
                "ORDER BY vg.fecha DESC";

            var pars = new List<(string, object)> { ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta) };
            if (porSucursal) pars.Add(("@suc", sucursalId));

            var lista = new List<DocVisorFila>();
            var tabla = EjecutarConsulta(sql, pars.ToArray());
            foreach (DataRow fila in tabla.Rows)
            {
                lista.Add(new DocVisorFila
                {
                    Num        = lista.Count + 1,
                    Id         = Texto(fila["id"]),
                    Codigo     = Texto(fila["codigo"]),
                    Fecha      = FechaSql(fila["fecha"]),
                    Sucursal   = Texto(fila["sucursal"]),
                    Movimiento = Texto(fila["movimiento"]),
                    Tercero    = Texto(fila["tercero"]),
                    Referencia = Texto(fila["referencia"]),
                    Estado     = Texto(fila["estado"]),
                    EstadoC    = Texto(fila["estadoC"]),
                    Importe    = Numero(fila["importe"])
                });
            }
            return lista;
        }

        // ─── Líneas del documento seleccionado ────────────────────────────────
        // Igual que AppLoader: las líneas se filtran solo por el encabezado (sin
        // filtro propio de estadof), para mostrar exactamente lo mismo que la app
        // principal.

        public static List<LineaVisorFila> CargarLineasPedido(string documentoId) =>
            MapearLineas(EjecutarConsulta(
                "SELECT vd.indice, ISNULL(a.codigo, '') AS codigo, ISNULL(a.descripcion, '') AS descripcion, " +
                "       ISNULL(vd.cantidad, 0) AS cantidad, ISNULL(vd.importe, 0) AS importe " +
                "FROM pedidos vd LEFT JOIN articulos a ON a.id = vd.articulo " +
                "WHERE vd.documentoP = @doc ORDER BY vd.indice ASC",
                ("@doc", documentoId)), conImporte: true);

        public static List<LineaVisorFila> CargarLineasTraspaso(string documentoId) =>
            MapearLineas(EjecutarConsulta(
                "SELECT vd.indice, ISNULL(a.codigo, '') AS codigo, ISNULL(a.descripcion, '') AS descripcion, " +
                "       ISNULL(vd.cantidad, 0) AS cantidad " +
                "FROM traspasos vd LEFT JOIN articulos a ON a.id = vd.articulo " +
                "WHERE vd.documentoT = @doc ORDER BY vd.indice ASC",
                ("@doc", documentoId)), conImporte: false);

        public static List<LineaVisorFila> CargarLineasCorreccion(string documentoId) =>
            MapearLineas(EjecutarConsulta(
                "SELECT vd.indice, ISNULL(a.codigo, '') AS codigo, ISNULL(a.descripcion, '') AS descripcion, " +
                "       ISNULL(vd.cantidad, 0) AS cantidad " +
                "FROM correcciones vd LEFT JOIN articulos a ON a.id = vd.articulo " +
                "WHERE vd.documentoC = @doc ORDER BY vd.indice ASC",
                ("@doc", documentoId)), conImporte: false);

        public static List<LineaVisorFila> CargarLineasFactura(string documentoId)
        {
            var lista = new List<LineaVisorFila>();
            var tabla = EjecutarConsulta(
                "SELECT vd.indice, ISNULL(vd.concepto, '') AS descripcion, " +
                "       ISNULL(c.descripcion, '') AS categoria, ISNULL(vd.importe, 0) AS importe " +
                "FROM facturas vd LEFT JOIN categorias c ON c.id = vd.categoria " +
                "WHERE vd.documentoF = @doc ORDER BY vd.indice ASC",
                ("@doc", documentoId));
            foreach (DataRow fila in tabla.Rows)
            {
                lista.Add(new LineaVisorFila
                {
                    Lin         = Entero(fila["indice"]),
                    Descripcion = Texto(fila["descripcion"]),
                    Categoria   = Texto(fila["categoria"]),
                    Importe     = Numero(fila["importe"])
                });
            }
            return lista;
        }

        private static List<LineaVisorFila> MapearLineas(DataTable tabla, bool conImporte)
        {
            var lista = new List<LineaVisorFila>();
            foreach (DataRow fila in tabla.Rows)
            {
                lista.Add(new LineaVisorFila
                {
                    Lin         = Entero(fila["indice"]),
                    Codigo      = Texto(fila["codigo"]),
                    Descripcion = Texto(fila["descripcion"]),
                    Cantidad    = Numero(fila["cantidad"]),
                    Importe     = conImporte ? Numero(fila["importe"]) : 0
                });
            }
            return lista;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static (DateTime Desde, DateTime Hasta) RangoAnio(int anio) =>
            (new DateTime(anio, 1, 1, 0, 0, 0), new DateTime(anio, 12, 31, 23, 59, 59));

        private static DateTime FechaSql(object? v)
        {
            if (v is DateTime dt) return dt;
            return DateTime.TryParse(v?.ToString(), out var f) ? f : default;
        }

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
