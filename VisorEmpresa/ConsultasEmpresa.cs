using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using SistemaGestion.Data;

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
    /// SistemaGestion.StockCalculator.ContarStock/ContarStock2.</summary>
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
            // Parte SQL con reintentos ante fallos transitorios (timeout de handshake,
            // conexión rota) — igual que DataConsulta; acá antes no estaba conectado.
            var (id, llave, tipo, empresa) = SqlRetry.Ejecutar(() =>
            {
                using var conn = new SqlConnection(CadenaConexion());
                conn.Open();

                using var cmd = new SqlCommand(
                    "SELECT id, llave, tipo, empresa FROM usuarios " +
                    "WHERE cuenta = @cuenta AND estadof = 'normal'", conn);
                cmd.Parameters.AddWithValue("@cuenta", cuenta);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) return ("", "", "", "");
                return (Texto(reader["id"]), Texto(reader["llave"]), Texto(reader["tipo"]), Texto(reader["empresa"]));
            });

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

        // ─── Caché en memoria de pedidos/traspasos/correcciones/aperturas, a
        //     nivel de TODA LA EMPRESA (todas las sucursales, sin límite de
        //     fecha) ────────────────────────────────────────────────────────
        //
        // Alimenta ObtenerStockEmpresa/CargarMovimientos/CargarResumenPedidos/
        // CargarTraspasosInternos, que antes de esto consultaban SQL en vivo en
        // CADA llamada (con latencia alta esa demora se sentía al abrir cada
        // pestaña). Se carga una sola vez al loguear (el pre-calentado de
        // ObtenerStockEmpresa en LoginVisorWindow ya la dispara) y al cambiar de
        // empresa desde la top bar (ConsolaVisor); de ahí en más esos 4 métodos
        // son cálculo puro en memoria.
        //
        // Independiente de Sql.DocumentosPObj/PedidosObj/DocumentosTObj/
        // TraspasosObj/DocumentosCObj/CorreccionesObj (usados por
        // ConectarCachePedidos/Traspasos/Correcciones, año+sucursal-scoped, para
        // las pantallas de navegación de documentos): esas no se tocan acá.
        private sealed class PedidoCache
        {
            public string DocumentoId = "", Sucursal = "", Articulo = "", Movimiento = "", Estado = "";
            public double Cantidad;
            public DateTime Fecha;
        }

        private sealed class TraspasoCache
        {
            public string DocumentoId = "", Origen = "", Destino = "", Articulo = "";
            public double Cantidad;
            public DateTime Fecha;
        }

        private sealed class CorreccionCache
        {
            public string Sucursal = "", Articulo = "", Movimiento = "";
            public double Cantidad;
            public DateTime Fecha;
        }

        private static string? _empresaCacheDashboard;
        private static Dictionary<string, DateTime> _aperturaFechaCache    = new();
        private static Dictionary<string, double>   _aperturaCantidadCache = new(); // clave "sucursal|articulo"
        private static List<PedidoCache>            _pedidosCache          = new();
        private static List<TraspasoCache>          _traspasosCache        = new();
        private static List<CorreccionCache>        _correccionesCache     = new();

        /// <summary>
        /// Puebla la caché de pedidos/traspasos/correcciones/aperturas de TODA la
        /// empresa (sin filtro de fecha ni sucursal). Los 4 métodos que la usan
        /// también la disparan solos si detectan que la empresa cacheada no
        /// coincide, así que son correctos igual aunque se llamen directo sin
        /// pre-calentar antes.
        /// </summary>
        public static void ConectarCacheDashboardEmpresa(string empresa)
        {
            string emp = EmpresaSegura(empresa);
            _empresaCacheDashboard = empresa;

            // ── Fecha de apertura por sucursal ───────────────────────────────
            _aperturaFechaCache = new Dictionary<string, DateTime>();
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
                _aperturaFechaCache[sucId] = fecha;
            }

            // ── Cantidades de apertura (inventario del documentoI más reciente
            //    de cada sucursal) ─────────────────────────────────────────────
            _aperturaCantidadCache = new Dictionary<string, double>();
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
            {
                string clave = Texto(fila["sucursal"]) + "|" + Texto(fila["articulo"]);
                _aperturaCantidadCache[clave] = _aperturaCantidadCache.GetValueOrDefault(clave) + Numero(fila["cantidad"]);
            }

            // ── Pedidos (todas las sucursales, sin límite de fecha) ──────────
            _pedidosCache = new List<PedidoCache>();
            var tPedidos = EjecutarConsulta(
                "SELECT vg.id AS documentoId, vg.sucursal, vd.articulo, vd.cantidad, " +
                "       vg.movimiento, vg.estado, vg.fecha " +
                "FROM pedidos AS vd " +
                "INNER JOIN documentosP AS vg ON vd.documentoP = vg.id " +
                "INNER JOIN sucursales AS s ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp",
                ("@emp", emp));
            foreach (DataRow fila in tPedidos.Rows)
                _pedidosCache.Add(new PedidoCache
                {
                    DocumentoId = Texto(fila["documentoId"]),
                    Sucursal    = Texto(fila["sucursal"]),
                    Articulo    = Texto(fila["articulo"]),
                    Cantidad    = Numero(fila["cantidad"]),
                    Movimiento  = Texto(fila["movimiento"]).Trim().ToLowerInvariant(),
                    Estado      = Texto(fila["estado"]),
                    Fecha       = Convert.ToDateTime(fila["fecha"])
                });

            // ── Traspasos (origen o destino en la empresa, sin límite de fecha) ─
            _traspasosCache = new List<TraspasoCache>();
            var tTraspasos = EjecutarConsulta(
                "SELECT vg.id AS documentoId, vg.origen, vg.destino, vd.articulo, vd.cantidad, vg.fecha " +
                "FROM traspasos AS vd " +
                "INNER JOIN documentosT AS vg ON vd.documentoT = vg.id " +
                "WHERE vg.estadof = 'normal' " +
                "AND (EXISTS (SELECT 1 FROM sucursales so WHERE so.id = vg.origen  AND so.estadof = 'normal' AND so.empresa = @emp) " +
                "  OR EXISTS (SELECT 1 FROM sucursales sd WHERE sd.id = vg.destino AND sd.estadof = 'normal' AND sd.empresa = @emp))",
                ("@emp", emp));
            foreach (DataRow fila in tTraspasos.Rows)
                _traspasosCache.Add(new TraspasoCache
                {
                    DocumentoId = Texto(fila["documentoId"]),
                    Origen      = Texto(fila["origen"]),
                    Destino     = Texto(fila["destino"]),
                    Articulo    = Texto(fila["articulo"]),
                    Cantidad    = Numero(fila["cantidad"]),
                    Fecha       = Convert.ToDateTime(fila["fecha"])
                });

            // ── Correcciones (todas las sucursales, sin límite de fecha) ─────
            _correccionesCache = new List<CorreccionCache>();
            var tCorrecciones = EjecutarConsulta(
                "SELECT vg.sucursal, vd.articulo, vd.cantidad, vg.movimiento, vg.fecha " +
                "FROM correcciones AS vd " +
                "INNER JOIN documentosC AS vg ON vd.documentoC = vg.id " +
                "INNER JOIN sucursales AS s ON s.id = vg.sucursal " +
                "WHERE vg.estadof = 'normal' AND s.estadof = 'normal' AND s.empresa = @emp",
                ("@emp", emp));
            foreach (DataRow fila in tCorrecciones.Rows)
                _correccionesCache.Add(new CorreccionCache
                {
                    Sucursal   = Texto(fila["sucursal"]),
                    Articulo   = Texto(fila["articulo"]),
                    Cantidad   = Numero(fila["cantidad"]),
                    Movimiento = Texto(fila["movimiento"]).ToLowerInvariant(),
                    Fecha      = Convert.ToDateTime(fila["fecha"])
                });

            // La empresa cambió: cualquier stock ya calculado quedó obsoleto.
            _stockEmpresaCache.Clear();
        }

        // ─── Movimientos agregados del año ────────────────────────────────────

        /// <summary>
        /// Unidades de pedidos de TODA la empresa en el año, agrupadas por
        /// sucursal + movimiento + mes + categoría. Alimenta los KPIs, el gráfico
        /// mensual, el desglose por categoría y la comparativa por sucursal.
        /// Cálculo en memoria sobre la caché de <see cref="ConectarCacheDashboardEmpresa"/>.
        /// </summary>
        public static List<MovimientoFila> CargarMovimientos(string empresa, int anio, string sucursalId = "")
        {
            if (_empresaCacheDashboard != empresa) ConectarCacheDashboardEmpresa(empresa);

            var (desde, hasta) = RangoAnio(anio);
            var grupos = new Dictionary<(string SucursalId, string Movimiento, int Mes, string Categoria), double>();
            var sucursalDescripcion = new Dictionary<string, string>();

            // Mismo corte por apertura que las 4 vistas de documentos: sin esto,
            // el dashboard sumaba pedidos anteriores a la última apertura de cada
            // sucursal.
            foreach (var p in _pedidosCache)
            {
                if (p.Fecha < desde || p.Fecha > hasta) continue;
                if (!_aperturaFechaCache.TryGetValue(p.Sucursal, out var apertura) || p.Fecha < apertura) continue;
                if (!string.IsNullOrEmpty(sucursalId) && p.Sucursal != sucursalId) continue;

                if (!sucursalDescripcion.ContainsKey(p.Sucursal))
                    sucursalDescripcion[p.Sucursal] = Texto(Sql.SucursalesObj.ObtenerItem("descripcion", p.Sucursal));

                string categoriaId = Texto(Sql.ArticulosObj.ObtenerItem("categoria", p.Articulo));
                string categoria = string.IsNullOrEmpty(categoriaId)
                    ? ""
                    : Texto(Sql.CategoriasObj.ObtenerItem("descripcion", categoriaId));
                if (string.IsNullOrEmpty(categoria)) categoria = "Sin categoría";

                var clave = (p.Sucursal, p.Movimiento, p.Fecha.Month, categoria);
                grupos[clave] = grupos.GetValueOrDefault(clave) + p.Cantidad;
            }

            var lista = new List<MovimientoFila>();
            foreach (var (clave, cantidad) in grupos)
                lista.Add(new MovimientoFila
                {
                    SucursalId = clave.SucursalId,
                    Sucursal   = sucursalDescripcion.GetValueOrDefault(clave.SucursalId, ""),
                    Movimiento = clave.Movimiento,
                    Mes        = clave.Mes,
                    Categoria  = clave.Categoria,
                    Cantidad   = cantidad
                });
            return lista;
        }

        /// <summary>
        /// Contadores por movimiento (venta/compra): documentos distintos y
        /// artículos distintos del año en toda la empresa. Cálculo en memoria
        /// sobre la caché de <see cref="ConectarCacheDashboardEmpresa"/>.
        /// </summary>
        public static Dictionary<string, (int Documentos, int Articulos)> CargarResumenPedidos(
            string empresa, int anio, string sucursalId = "")
        {
            if (_empresaCacheDashboard != empresa) ConectarCacheDashboardEmpresa(empresa);

            var (desde, hasta) = RangoAnio(anio);
            var documentosPorMov = new Dictionary<string, HashSet<string>>();
            var articulosPorMov  = new Dictionary<string, HashSet<string>>();

            foreach (var p in _pedidosCache)
            {
                if (p.Fecha < desde || p.Fecha > hasta) continue;
                if (!_aperturaFechaCache.TryGetValue(p.Sucursal, out var apertura) || p.Fecha < apertura) continue;
                if (!string.IsNullOrEmpty(sucursalId) && p.Sucursal != sucursalId) continue;
                if (p.Movimiento == "") continue;

                if (!documentosPorMov.TryGetValue(p.Movimiento, out var docs))
                    documentosPorMov[p.Movimiento] = docs = new HashSet<string>();
                docs.Add(p.DocumentoId);

                if (!articulosPorMov.TryGetValue(p.Movimiento, out var arts))
                    articulosPorMov[p.Movimiento] = arts = new HashSet<string>();
                arts.Add(p.Articulo);
            }

            var resumen = new Dictionary<string, (int, int)>();
            foreach (var mov in documentosPorMov.Keys)
                resumen[mov] = (documentosPorMov[mov].Count, articulosPorMov.GetValueOrDefault(mov)?.Count ?? 0);
            return resumen;
        }

        /// <summary>
        /// Traspasos internos del año: unidades y documentos donde el origen o el
        /// destino pertenece a la empresa. A nivel de empresa las "entradas" y
        /// "salidas" se cancelan entre sí, por eso se muestran como un único
        /// total de movimiento interno (se excluyen auto-traspasos origen=destino).
        /// Cálculo en memoria sobre la caché de <see cref="ConectarCacheDashboardEmpresa"/>.
        /// </summary>
        public static (double Unidades, int Documentos) CargarTraspasosInternos(
            string empresa, int anio, string sucursalId = "")
        {
            if (_empresaCacheDashboard != empresa) ConectarCacheDashboardEmpresa(empresa);

            var (desde, hasta) = RangoAnio(anio);
            double unidades = 0;
            var documentos = new HashSet<string>();

            // Mismo criterio por-lado que antes: un traspaso cuenta si AL MENOS un
            // lado (origen o destino) pertenece a la empresa (o, con una sucursal
            // puntual elegida, si esa sucursal es el origen o el destino) y está
            // posterior a la apertura de ESA sucursal. _aperturaFechaCache solo
            // contiene sucursales de la empresa activa, así que TryGetValue ya
            // hace las veces de "pertenece a la empresa".
            foreach (var t in _traspasosCache)
            {
                if (t.Fecha < desde || t.Fecha > hasta) continue;
                if (t.Origen == t.Destino) continue;

                bool aperturaOrigenOk  = _aperturaFechaCache.TryGetValue(t.Origen,  out var apOrigen)  && t.Fecha >= apOrigen;
                bool aperturaDestinoOk = _aperturaFechaCache.TryGetValue(t.Destino, out var apDestino) && t.Fecha >= apDestino;

                bool cumple = string.IsNullOrEmpty(sucursalId)
                    ? (aperturaOrigenOk || aperturaDestinoOk)
                    : ((t.Origen == sucursalId && aperturaOrigenOk) || (t.Destino == sucursalId && aperturaDestinoOk));
                if (!cumple) continue;

                unidades += t.Cantidad;
                documentos.Add(t.DocumentoId);
            }

            return (unidades, documentos.Count);
        }

        // ─── Stock/Disponible de TODA la empresa (para PreciosDetalle y para el
        //     panel "Stock" de Pedidos/Traspasos/Correcciones Detalle) ──────────
        //
        // Mismo criterio que SistemaGestion.StockCalculator.ContarStock/ContarStock2 (5
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
        // día) y se reutiliza entre aperturas de documentos. Se invalida con
        // forzarRecarga=true (botón "Actualizar" de PreciosDetalle).
        private static readonly Dictionary<(string Empresa, DateTime Hasta), StockEmpresaResultado> _stockEmpresaCache = new();

        /// <param name="hasta">Fecha límite de la acumulación (default: ahora).</param>
        public static StockEmpresaResultado ObtenerStockEmpresa(string empresa, DateTime? hasta = null, bool forzarRecarga = false)
        {
            DateTime fechaHasta = hasta ?? DateTime.Now;
            var clave = (empresa, fechaHasta.Date);

            if (!forzarRecarga && _stockEmpresaCache.TryGetValue(clave, out var enCache))
                return enCache;

            if (forzarRecarga || _empresaCacheDashboard != empresa)
                ConectarCacheDashboardEmpresa(empresa);

            DateTime ahora = fechaHasta;
            var porSucursal = new Dictionary<string, StockAcumuladoInfo>();

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

            // ── Apertura (cantidades de inventario del documentoI más reciente
            //    de cada sucursal) ────────────────────────────────────────────
            foreach (var (claveSucArt, cantidad) in _aperturaCantidadCache)
            {
                int sep = claveSucArt.IndexOf('|');
                Obtener(claveSucArt[..sep], claveSucArt[(sep + 1)..]).Apertura += cantidad;
            }

            // ── Pedidos (ventas/compras) ─────────────────────────────────────
            foreach (var p in _pedidosCache)
            {
                if (p.Fecha > ahora) continue;
                if (!_aperturaFechaCache.TryGetValue(p.Sucursal, out var apertura) || p.Fecha < apertura) continue;

                var acumulado = Obtener(p.Sucursal, p.Articulo);
                if (p.Movimiento == "venta")
                {
                    acumulado.VentasTodas += p.Cantidad;
                    if (p.Estado == "entregado") acumulado.VentasEnt += p.Cantidad;
                }
                if (p.Movimiento == "compra")
                {
                    acumulado.ComprasTodas += p.Cantidad;
                    if (p.Estado == "entregado") acumulado.ComprasEnt += p.Cantidad;
                }
            }

            // ── Traspasos (entradas/salidas; cada lado se gatea con la apertura
            //    propia de SU sucursal) ────────────────────────────────────────
            foreach (var t in _traspasosCache)
            {
                if (t.Fecha > ahora) continue;
                if (t.Origen == t.Destino) continue;

                if (_aperturaFechaCache.TryGetValue(t.Origen, out var apOrigen) && t.Fecha >= apOrigen)
                    Obtener(t.Origen, t.Articulo).Salidas += t.Cantidad;

                if (_aperturaFechaCache.TryGetValue(t.Destino, out var apDestino) && t.Fecha >= apDestino)
                    Obtener(t.Destino, t.Articulo).Entradas += t.Cantidad;
            }

            // ── Correcciones (ingreso suma, egreso resta) ────────────────────
            foreach (var c in _correccionesCache)
            {
                if (c.Fecha > ahora) continue;
                if (!_aperturaFechaCache.TryGetValue(c.Sucursal, out var apertura) || c.Fecha < apertura) continue;

                var acumulado = Obtener(c.Sucursal, c.Articulo);
                if (c.Movimiento == "ingreso") acumulado.Ingresos   += c.Cantidad;
                if (c.Movimiento == "egreso")  acumulado.Descuentos += c.Cantidad;
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
        public static void ConectarCacheTraspasos(string empresa, int anio, string origenId = "", string destinoId = "")
        {
            var (desde, hasta) = RangoAnio(anio);
            string aper = FechaLiteral(desde);
            string cier = FechaLiteral(hasta);
            string emp  = EmpresaSegura(empresa);
            bool porOrigen  = !string.IsNullOrEmpty(origenId);
            bool porDestino = !string.IsNullOrEmpty(destinoId);

            string condLado;
            if (porOrigen && porDestino)
                condLado = $"AND vg.origen = '{origenId}' AND vg.destino = '{destinoId}' " +
                           "AND vg.fecha >= COALESCE(apo.fecha, so.fecha) AND vg.fecha >= COALESCE(apd.fecha, sd.fecha) ";
            else if (porOrigen)
                condLado = $"AND vg.origen = '{origenId}' AND vg.fecha >= COALESCE(apo.fecha, so.fecha) ";
            else if (porDestino)
                condLado = $"AND vg.destino = '{destinoId}' AND vg.fecha >= COALESCE(apd.fecha, sd.fecha) ";
            else
                condLado = $"AND ( (so.empresa = '{emp}' AND vg.fecha >= COALESCE(apo.fecha, so.fecha)) " +
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

        // Reintentos ante fallos transitorios (timeout de handshake, conexión rota,
        // deadlock) — igual que DataConsulta.ObtenerDatos. Cubre implícitamente casi
        // todos los métodos de esta clase (CargarSucursalesEmpresa, ObtenerStockEmpresa,
        // CargarResumenPedidos, etc.), todos pasan por acá.
        private static DataTable EjecutarConsulta(
            string sql, params (string Nombre, object Valor)[] parametros) => SqlRetry.Ejecutar(() =>
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
        });

        private static string Texto(object? v) =>
            v is null or DBNull ? "" : v.ToString() ?? "";

        private static double Numero(object? v) =>
            v is null or DBNull ? 0 : Convert.ToDouble(v);

        private static int Entero(object? v) =>
            v is null or DBNull ? 0 : Convert.ToInt32(v);
    }
}
