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

    /// <summary>
    /// Datos del usuario autenticado que necesita el visor. Sin TemaC a propósito:
    /// el tema del visor es independiente del de la app principal (ver TemaVisor).
    /// </summary>
    public class UsuarioVisor
    {
        public string Id      { get; set; } = "";
        public string Tipo    { get; set; } = "";
        public string Empresa { get; set; } = "";
    }

    /// <summary>Acumulador de movimientos de UNA sucursal + UN artículo, usado por
    /// <see cref="ConsultasEmpresa.ObtenerStockEmpresa"/>. Mismas fórmulas que
    /// WpfAppVba.StockCalculator.ContarStock/ContarStock2.</summary>
    public class StockAcumuladoInfo
    {
        public double Apertura, Entradas, Salidas, VentasTodas, ComprasTodas, VentasEnt, ComprasEnt, Ingresos, Descuentos;

        // Equivalente a StockCalculator.ContarStock (solo movimientos "entregado").
        public double Stock      => (Apertura + Entradas + ComprasEnt   + Ingresos) - (Salidas + VentasEnt   + Descuentos);
        // Equivalente a StockCalculator.ContarStock2 (todos los movimientos).
        public double Disponible => (Apertura + Entradas + ComprasTodas + Ingresos) - (Salidas + VentasTodas + Descuentos);
    }

    /// <summary>Resultado de <see cref="ConsultasEmpresa.ObtenerStockEmpresa"/>.</summary>
    public class StockEmpresaResultado
    {
        /// <summary>Totales por artículo (suma de todas las sucursales).</summary>
        public Dictionary<string, (double Disponible, double Stock)> Totales { get; init; } = new();
        /// <summary>Detalle por sucursal, clave "sucursal|articulo".</summary>
        public Dictionary<string, StockAcumuladoInfo> PorSucursal { get; init; } = new();
    }

    /// <summary>
    /// Consultas SQL agregadas a nivel de EMPRESA (todas las sucursales, sin el
    /// filtro por sucursal activa de la app principal), parametrizadas y de solo
    /// lectura.
    /// </summary>
    public static class ConsultasEmpresa
    {
        private static SqlData Sql => SqlData.Instance;

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
            using var conn = new SqlConnection(CadenaConexion());
            conn.Open();

            string id = "", llave = "", tipo = "", empresa = "";
            using (var cmd = new SqlCommand(
                "SELECT id, llave, tipo, empresa FROM usuarios " +
                "WHERE cuenta = @cuenta AND estadof = 'normal'", conn))
            {
                cmd.Parameters.AddWithValue("@cuenta", cuenta);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return null;
                id      = Texto(reader["id"]);
                llave   = Texto(reader["llave"]);
                tipo    = Texto(reader["tipo"]);
                empresa = Texto(reader["empresa"]);
            }

            if (string.IsNullOrEmpty(id)) return null;

            bool valida = PasswordHasher.Verificar(contrasena, llave)
                          || (!PasswordHasher.EsHash(llave) && llave == contrasena);
            if (!valida) return null;

            return new UsuarioVisor { Id = id, Tipo = tipo, Empresa = empresa };
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
        public static List<MovimientoFila> CargarMovimientos(string empresa, int anio, string sucursalId = "")
        {
            var (desde, hasta) = RangoAnio(anio);
            var lista = new List<MovimientoFila>();
            string filtroSuc = string.IsNullOrEmpty(sucursalId) ? "" : " AND s.id = @suc";

            // Mismo corte por apertura que las 4 vistas de documentos (ver
            // SubconsultaApertura más abajo): sin esto, el dashboard sumaba pedidos
            // anteriores a la última apertura de cada sucursal.
            var tabla = EjecutarConsulta(
                "SELECT s.id AS sucursalId, s.descripcion AS sucursal, vg.movimiento, " +
                "       MONTH(vg.fecha) AS mes, " +
                "       ISNULL(c.descripcion, N'Sin categoría') AS categoria, " +
                "       SUM(vd.cantidad) AS cantidad " +
                "FROM pedidos vd " +
                "INNER JOIN documentosP vg ON vd.documentoP = vg.id " +
                "INNER JOIN sucursales s   ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = @emp " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "LEFT JOIN articulos a     ON a.id = vd.articulo " +
                "LEFT JOIN categorias c    ON c.id = a.categoria " +
                "WHERE vg.estadof = 'normal' " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha)" +
                filtroSuc +
                " GROUP BY s.id, s.descripcion, vg.movimiento, MONTH(vg.fecha), " +
                "         ISNULL(c.descripcion, N'Sin categoría')",
                ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta), ("@suc", sucursalId));

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
            string empresa, int anio, string sucursalId = "")
        {
            var (desde, hasta) = RangoAnio(anio);
            var resumen = new Dictionary<string, (int, int)>();
            string filtroSuc = string.IsNullOrEmpty(sucursalId) ? "" : " AND vg.sucursal = @suc";

            var tabla = EjecutarConsulta(
                "SELECT vg.movimiento, " +
                "       COUNT(DISTINCT vg.id) AS documentos, " +
                "       COUNT(DISTINCT vd.articulo) AS articulos " +
                "FROM pedidos vd " +
                "INNER JOIN documentosP vg ON vd.documentoP = vg.id " +
                "INNER JOIN sucursales s   ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = @emp " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "WHERE vg.estadof = 'normal' " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha)" +
                filtroSuc +
                " GROUP BY vg.movimiento",
                ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta), ("@suc", sucursalId));

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
            string empresa, int anio, string sucursalId = "")
        {
            var (desde, hasta) = RangoAnio(anio);

            // Mismo criterio por-lado que CargarDocsTraspasos: un traspaso cuenta si
            // AL MENOS un lado (origen o destino) pertenece a la empresa (o, con una
            // sucursal puntual elegida, si esa sucursal es el origen o el destino) y
            // está posterior a la apertura de ESA sucursal.
            string condicionLado = string.IsNullOrEmpty(sucursalId)
                ? "( (so.empresa = @emp AND vg.fecha >= COALESCE(apo.fecha, so.fecha)) " +
                  "   OR (sd.empresa = @emp AND vg.fecha >= COALESCE(apd.fecha, sd.fecha)) )"
                : "( (vg.origen = @suc AND vg.fecha >= COALESCE(apo.fecha, so.fecha)) " +
                  "   OR (vg.destino = @suc AND vg.fecha >= COALESCE(apd.fecha, sd.fecha)) )";

            var tabla = EjecutarConsulta(
                "SELECT ISNULL(SUM(vd.cantidad), 0) AS cantidad, " +
                "       COUNT(DISTINCT vg.id) AS documentos " +
                "FROM traspasos vd " +
                "INNER JOIN documentosT vg ON vd.documentoT = vg.id " +
                "LEFT JOIN sucursales so ON so.id = vg.origen  AND so.estadof = 'normal' " +
                "LEFT JOIN sucursales sd ON sd.id = vg.destino AND sd.estadof = 'normal' " +
                "LEFT JOIN " + SubconsultaApertura + " apo ON apo.sucursal = vg.origen " +
                "LEFT JOIN " + SubconsultaApertura + " apd ON apd.sucursal = vg.destino " +
                "WHERE vg.estadof = 'normal' " +
                "AND vg.fecha >= @desde AND vg.fecha <= @hasta " +
                "AND vg.origen <> vg.destino " +
                "AND " + condicionLado,
                ("@emp", EmpresaSegura(empresa)), ("@desde", desde), ("@hasta", hasta), ("@suc", sucursalId));

            if (tabla.Rows.Count == 0) return (0, 0);
            var r = tabla.Rows[0];
            return (Numero(r["cantidad"]), Entero(r["documentos"]));
        }

        // ─── Stock/Disponible de TODA la empresa (para PreciosDetalle y para el
        //     panel "Stock" de Pedidos/Traspasos/Correcciones Detalle) ──────────
        //
        // Mismo criterio que WpfAppVba.StockCalculator.ContarStock/ContarStock2 (5
        // consultas SQL agregadas a nivel de empresa en vez de una sola sucursal):
        // la apertura de cada sucursal es la de su documentoI más reciente (o su
        // fecha de creación si no tiene ninguno), y se acumula todo movimiento
        // posterior hasta la fecha "hasta" pedida (por defecto, ahora mismo).
        //
        // IMPORTANTE: StockCalculator.ContarStock usa AppState.SucursalActiva/
        // AperturaActiva/DataFechaFinal — ninguno de los tres se puebla nunca en
        // VisorEmpresa (AppState.ActualizarBase, que los llena, es exclusivo del
        // login de la app principal), así que llamarlo directamente desde el
        // visor da 0 o valores incompletos siempre. Este método es el reemplazo
        // real, no solo una optimización de caché.
        //
        // El resultado queda en caché en memoria (por empresa + fecha "hasta", al
        // día) y se reutiliza entre aperturas de documentos: evita recalcular con
        // 5 consultas SQL cada vez. Se invalida con forzarRecarga=true (botón
        // "Actualizar" de PreciosDetalle).
        private static readonly Dictionary<(string Empresa, DateTime Hasta), StockEmpresaResultado> _stockEmpresaCache = new();

        /// <param name="hasta">Fecha límite de la acumulación (default: ahora).</param>
        public static StockEmpresaResultado ObtenerStockEmpresa(string empresa, DateTime? hasta = null, bool forzarRecarga = false)
        {
            DateTime fechaHasta = hasta ?? DateTime.Now;
            var clave = (empresa, fechaHasta.Date);

            if (!forzarRecarga && _stockEmpresaCache.TryGetValue(clave, out var enCache))
                return enCache;

            string emp = EmpresaSegura(empresa);
            DateTime ahora = fechaHasta;

            var aperturaFecha = new Dictionary<string, DateTime>();
            var porSucursal   = new Dictionary<string, StockAcumuladoInfo>();

            StockAcumuladoInfo Obtener(string sucursal, string articulo)
            {
                string claveSucArt = sucursal + "|" + articulo;
                if (!porSucursal.TryGetValue(claveSucArt, out var acumulado))
                {
                    acumulado = new StockAcumuladoInfo();
                    porSucursal[claveSucArt] = acumulado;
                }
                return acumulado;
            }

            // ── Fecha de apertura por sucursal ──────────────────────────────
            var tSucursales = EjecutarConsulta(
                "SELECT s.id AS sucursal, s.fecha AS fechaCreacion, " +
                "(SELECT MAX(di.fecha) FROM documentosI di " +
                " WHERE di.estadof = 'normal' AND di.sucursal = s.id) AS fechaApertura " +
                "FROM sucursales s WHERE s.estadof = 'normal' AND s.empresa = @emp",
                ("@emp", emp));

            foreach (DataRow fila in tSucursales.Rows)
            {
                string sucId = Texto(fila["sucursal"]);
                DateTime fecha = fila["fechaApertura"] is DBNull
                    ? (fila["fechaCreacion"] is DBNull ? DateTime.Today : Convert.ToDateTime(fila["fechaCreacion"]))
                    : Convert.ToDateTime(fila["fechaApertura"]);
                aperturaFecha[sucId] = fecha;
            }

            // ── Cantidades de apertura (inventario del documentoI más reciente
            //    de cada sucursal) ───────────────────────────────────────────
            var tApertura = EjecutarConsulta(
                "WITH UltimaApertura AS (" +
                "  SELECT di.id AS documentoI, di.sucursal, " +
                "         ROW_NUMBER() OVER (PARTITION BY di.sucursal ORDER BY di.fecha DESC) AS rn " +
                "  FROM documentosI di " +
                "  INNER JOIN sucursales s ON s.id = di.sucursal " +
                "  WHERE di.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp" +
                ") " +
                "SELECT ua.sucursal, inv.articulo, inv.cantidad " +
                "FROM UltimaApertura ua " +
                "INNER JOIN inventarios inv ON inv.documentoI = ua.documentoI " +
                "WHERE ua.rn = 1",
                ("@emp", emp));

            foreach (DataRow fila in tApertura.Rows)
                Obtener(Texto(fila["sucursal"]), Texto(fila["articulo"])).Apertura += Numero(fila["cantidad"]);

            // ── Pedidos (ventas/compras) ─────────────────────────────────────
            var tPedidos = EjecutarConsulta(
                "SELECT vg.sucursal, vd.articulo, vd.cantidad, vg.movimiento, vg.estado, vg.fecha " +
                "FROM pedidos AS vd " +
                "INNER JOIN documentosP AS vg ON vd.documentoP = vg.id " +
                "INNER JOIN sucursales AS s ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp " +
                "AND vg.fecha <= @ahora",
                ("@emp", emp), ("@ahora", ahora));

            foreach (DataRow fila in tPedidos.Rows)
            {
                string sucId = Texto(fila["sucursal"]);
                if (!aperturaFecha.TryGetValue(sucId, out var apertura) || Convert.ToDateTime(fila["fecha"]) < apertura)
                    continue;

                string movimiento = Texto(fila["movimiento"]).ToLower();
                string estado     = Texto(fila["estado"]);
                double cantidad   = Numero(fila["cantidad"]);
                var acumulado     = Obtener(sucId, Texto(fila["articulo"]));

                if (movimiento == "venta")
                {
                    acumulado.VentasTodas += cantidad;
                    if (estado == "entregado") acumulado.VentasEnt += cantidad;
                }
                if (movimiento == "compra")
                {
                    acumulado.ComprasTodas += cantidad;
                    if (estado == "entregado") acumulado.ComprasEnt += cantidad;
                }
            }

            // ── Traspasos (entradas/salidas; cada lado se gatea con la apertura
            //    propia de SU sucursal) ────────────────────────────────────────
            var tTraspasos = EjecutarConsulta(
                "SELECT vg.origen, vg.destino, vd.articulo, vd.cantidad, vg.fecha " +
                "FROM traspasos AS vd " +
                "INNER JOIN documentosT AS vg ON vd.documentoT = vg.id " +
                "WHERE vg.estadof = 'normal' AND vg.fecha <= @ahora " +
                "AND (EXISTS (SELECT 1 FROM sucursales so WHERE so.id = vg.origen  AND so.estadof = 'normal' AND so.empresa = @emp) " +
                "  OR EXISTS (SELECT 1 FROM sucursales sd WHERE sd.id = vg.destino AND sd.estadof = 'normal' AND sd.empresa = @emp))",
                ("@emp", emp), ("@ahora", ahora));

            foreach (DataRow fila in tTraspasos.Rows)
            {
                string origen   = Texto(fila["origen"]);
                string destino  = Texto(fila["destino"]);
                string articulo = Texto(fila["articulo"]);
                double cantidad = Numero(fila["cantidad"]);
                DateTime fecha  = Convert.ToDateTime(fila["fecha"]);

                if (origen == destino) continue;

                if (aperturaFecha.TryGetValue(origen, out var apOrigen) && fecha >= apOrigen)
                    Obtener(origen, articulo).Salidas += cantidad;

                if (aperturaFecha.TryGetValue(destino, out var apDestino) && fecha >= apDestino)
                    Obtener(destino, articulo).Entradas += cantidad;
            }

            // ── Correcciones (ingreso suma, egreso resta) ────────────────────
            var tCorrecciones = EjecutarConsulta(
                "SELECT vg.sucursal, vd.articulo, vd.cantidad, vg.movimiento, vg.fecha " +
                "FROM correcciones AS vd " +
                "INNER JOIN documentosC AS vg ON vd.documentoC = vg.id " +
                "INNER JOIN sucursales AS s ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp " +
                "AND vg.fecha <= @ahora",
                ("@emp", emp), ("@ahora", ahora));

            foreach (DataRow fila in tCorrecciones.Rows)
            {
                string sucId = Texto(fila["sucursal"]);
                if (!aperturaFecha.TryGetValue(sucId, out var apertura) || Convert.ToDateTime(fila["fecha"]) < apertura)
                    continue;

                string movimiento = Texto(fila["movimiento"]).ToLower();
                double cantidad   = Numero(fila["cantidad"]);
                var acumulado     = Obtener(sucId, Texto(fila["articulo"]));

                if (movimiento == "ingreso") acumulado.Ingresos   += cantidad;
                if (movimiento == "egreso")  acumulado.Descuentos += cantidad;
            }

            // ── Totales por artículo (suma de todas las sucursales) ──────────
            var totales = new Dictionary<string, (double Disponible, double Stock)>();
            foreach (var (claveSucArt, acumulado) in porSucursal)
            {
                string articulo = claveSucArt.Substring(claveSucArt.IndexOf('|') + 1);
                totales.TryGetValue(articulo, out var actual);
                totales[articulo] = (actual.Disponible + acumulado.Disponible, actual.Stock + acumulado.Stock);
            }

            var resultado = new StockEmpresaResultado { Totales = totales, PorSucursal = porSucursal };
            _stockEmpresaCache[clave] = resultado;
            return resultado;
        }

        /// <summary>
        /// Stock "al cierre del período activo" (equivalente a
        /// StockCalculator.ContarStock(codigo, AppState.DataFechaFinal) de la app
        /// principal): fin del año elegido en la top bar del visor, o "ahora" si
        /// ese año todavía no terminó — mismo criterio que
        /// AppState.ActualizarBase para el período activo.
        /// </summary>
        public static StockEmpresaResultado ObtenerStockEmpresaAlCierre(string empresa, int anio, bool forzarRecarga = false)
        {
            DateTime finDeAño = new DateTime(anio, 12, 31, 23, 59, 59);
            DateTime hasta = finDeAño < DateTime.Now ? finDeAño : DateTime.Now;
            return ObtenerStockEmpresa(empresa, hasta, forzarRecarga);
        }

        // ─── Cachés SqlData para Pedidos/Traspasos/Correcciones/FacturasGeneral ──
        //
        // Estos 4 controles (VisorEmpresa.PedidosGeneral, etc.) son duplicados
        // fieles de los de la app principal: mismo código de UI (Tree1 de meses,
        // filtros, grilla, detalle) que lee de Sql.DocumentosXObj/Sql.XObj
        // (DataConsulta), igual que AppLoader.ConectarDocumentos. En vez de
        // reescribir esa lógica de lectura, se puebla la MISMA caché con SQL
        // scoped a UNA sucursal (sucursalId) o a TODA la empresa (sucursalId
        // vacío), siempre con el corte por apertura de CADA sucursal
        // (SubconsultaApertura) — mismo criterio que las antiguas CargarDocsXxx.

        private const string SubconsultaApertura =
            "(SELECT sucursal, MAX(fecha) AS fecha FROM documentosI " +
            "WHERE estadof = 'normal' GROUP BY sucursal)";

        private static string FechaLiteral(DateTime dt) => dt.ToString("yyyyMMdd HH:mm:ss");

        /// <summary>Puebla Sql.DocumentosPObj + Sql.PedidosObj (una sucursal o toda la empresa).</summary>
        public static void ConectarCachePedidos(string empresa, int anio, string sucursalId)
        {
            var (desde, hasta) = RangoAnio(anio);
            string aper = FechaLiteral(desde);
            string cier = FechaLiteral(hasta);
            string emp  = EmpresaSegura(empresa);
            string filtroSuc = string.IsNullOrEmpty(sucursalId) ? "" : $" AND vg.sucursal = '{sucursalId}'";

            Sql.DocumentosPObj.Conectar("documentosP",
                "SELECT vg.* FROM documentosP AS vg " +
                $"INNER JOIN sucursales AS s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = '{emp}' " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha)" +
                filtroSuc +
                " ORDER BY vg.fecha ASC");

            Sql.PedidosObj.Conectar("pedidos",
                "SELECT vd.* FROM pedidos AS vd " +
                "INNER JOIN documentosP AS vg ON vd.documentoP = vg.id " +
                $"INNER JOIN sucursales AS s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = '{emp}' " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha)" +
                filtroSuc +
                " ORDER BY vd.documentoP ASC, vd.indice ASC");
        }

        /// <summary>
        /// Puebla Sql.DocumentosTObj + Sql.TraspasosObj. Cada lado (origen/destino)
        /// se corta con la apertura de SU propia sucursal, igual con una sucursal
        /// puntual o con toda la empresa.
        /// </summary>
        public static void ConectarCacheTraspasos(string empresa, int anio, string sucursalId)
        {
            var (desde, hasta) = RangoAnio(anio);
            string aper = FechaLiteral(desde);
            string cier = FechaLiteral(hasta);
            string emp  = EmpresaSegura(empresa);
            bool porSucursal = !string.IsNullOrEmpty(sucursalId);

            string condLado = porSucursal
                ? $"AND ( (vg.origen = '{sucursalId}' AND vg.fecha >= COALESCE(apo.fecha, so.fecha)) " +
                  $"   OR (vg.destino = '{sucursalId}' AND vg.fecha >= COALESCE(apd.fecha, sd.fecha)) ) "
                : $"AND ( (so.empresa = '{emp}' AND vg.fecha >= COALESCE(apo.fecha, so.fecha)) " +
                  $"   OR (sd.empresa = '{emp}' AND vg.fecha >= COALESCE(apd.fecha, sd.fecha)) ) ";

            Sql.DocumentosTObj.Conectar("documentosT",
                "SELECT vg.* FROM documentosT AS vg " +
                "LEFT JOIN sucursales AS so ON so.id = vg.origen  AND so.estadof = 'normal' " +
                "LEFT JOIN sucursales AS sd ON sd.id = vg.destino AND sd.estadof = 'normal' " +
                "LEFT JOIN " + SubconsultaApertura + " apo ON apo.sucursal = vg.origen " +
                "LEFT JOIN " + SubconsultaApertura + " apd ON apd.sucursal = vg.destino " +
                "WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                condLado +
                "ORDER BY vg.fecha ASC");

            Sql.TraspasosObj.Conectar("traspasos",
                "SELECT vd.* FROM traspasos AS vd " +
                "INNER JOIN documentosT AS vg ON vd.documentoT = vg.id " +
                "LEFT JOIN sucursales AS so ON so.id = vg.origen  AND so.estadof = 'normal' " +
                "LEFT JOIN sucursales AS sd ON sd.id = vg.destino AND sd.estadof = 'normal' " +
                "LEFT JOIN " + SubconsultaApertura + " apo ON apo.sucursal = vg.origen " +
                "LEFT JOIN " + SubconsultaApertura + " apd ON apd.sucursal = vg.destino " +
                "WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                condLado +
                "ORDER BY vd.documentoT ASC, vd.indice ASC");
        }

        /// <summary>Puebla Sql.DocumentosCObj + Sql.CorreccionesObj (una sucursal o toda la empresa).</summary>
        public static void ConectarCacheCorrecciones(string empresa, int anio, string sucursalId)
        {
            var (desde, hasta) = RangoAnio(anio);
            string aper = FechaLiteral(desde);
            string cier = FechaLiteral(hasta);
            string emp  = EmpresaSegura(empresa);
            string filtroSuc = string.IsNullOrEmpty(sucursalId) ? "" : $" AND vg.sucursal = '{sucursalId}'";

            Sql.DocumentosCObj.Conectar("documentosC",
                "SELECT vg.* FROM documentosC AS vg " +
                $"INNER JOIN sucursales AS s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = '{emp}' " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha)" +
                filtroSuc +
                " ORDER BY vg.fecha ASC");

            Sql.CorreccionesObj.Conectar("correcciones",
                "SELECT vd.* FROM correcciones AS vd " +
                "INNER JOIN documentosC AS vg ON vd.documentoC = vg.id " +
                $"INNER JOIN sucursales AS s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = '{emp}' " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha)" +
                filtroSuc +
                " ORDER BY vd.documentoC ASC, vd.indice ASC");
        }

        /// <summary>
        /// Puebla Sql.DocumentosFObj + Sql.FacturasObj (una sucursal o toda la
        /// empresa). Al usar SELECT vg.*/vd.* (no columnas nombradas) no hace falta
        /// verificar existencia de columnas: se adapta sola al esquema real, igual
        /// que AppLoader.ConectarDocumentos.
        /// </summary>
        public static void ConectarCacheFacturas(string empresa, int anio, string sucursalId)
        {
            var (desde, hasta) = RangoAnio(anio);
            string aper = FechaLiteral(desde);
            string cier = FechaLiteral(hasta);
            string emp  = EmpresaSegura(empresa);
            string filtroSuc = string.IsNullOrEmpty(sucursalId) ? "" : $" AND vg.sucursal = '{sucursalId}'";

            Sql.DocumentosFObj.Conectar("documentosF",
                "SELECT vg.* FROM documentosF AS vg " +
                $"INNER JOIN sucursales AS s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = '{emp}' " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha)" +
                filtroSuc +
                " ORDER BY vg.fecha ASC");

            Sql.FacturasObj.Conectar("facturas",
                "SELECT vd.* FROM facturas AS vd " +
                "INNER JOIN documentosF AS vg ON vd.documentoF = vg.id " +
                $"INNER JOIN sucursales AS s ON s.id = vg.sucursal AND s.estadof = 'normal' AND s.empresa = '{emp}' " +
                "LEFT JOIN " + SubconsultaApertura + " ap ON ap.sucursal = vg.sucursal " +
                "WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                "AND vg.fecha >= COALESCE(ap.fecha, s.fecha)" +
                filtroSuc +
                " ORDER BY vd.documentoF ASC, vd.indice ASC");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static (DateTime Desde, DateTime Hasta) RangoAnio(int anio) =>
            (new DateTime(anio, 1, 1, 0, 0, 0), new DateTime(anio, 12, 31, 23, 59, 59));

        // Cadena de conexión PROPIA del visor: mismas credenciales que
        // DatabaseConnection (leídas de la configuración cifrada compartida), pero
        // cada consulta abre su propia conexión del pool. Las vistas consultan en
        // segundo plano (Task.Run) y pueden solaparse entre sí o con los módulos
        // de edición: dos consultas simultáneas sobre la MISMA SqlConnection dan
        // "Ya hay un DataReader abierto asociado a Connection". La conexión global
        // queda solo para las cachés (AppLoader), que la usan secuencialmente.
        private static string CadenaConexion()
        {
            var cfg = ConexionConfig.Cargar()
                      ?? throw new InvalidOperationException("Sin configuración de conexión.");
            var (servidor, baseDatos, usuario, contrasena) = cfg;
            return $"Server={servidor};Database={baseDatos};User Id={usuario};Password={contrasena};" +
                   "Application Name=edber-visor;Connect Timeout=10;Command Timeout=10;" +
                   "TrustServerCertificate=True;" +
                   "Connect Retry Count=3;Connect Retry Interval=10;Pooling=true;";
        }

        private static DataTable EjecutarConsulta(
            string sql, params (string Nombre, object Valor)[] parametros)
        {
            var tabla = new DataTable();
            using var conn = new SqlConnection(CadenaConexion());
            conn.Open();
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
