using System;

namespace TuProyecto.Data
{
    /// <summary>
    /// Equivalente a Módulo4.bas
    /// Contiene los métodos de carga inicial de datos agrupados por contexto.
    /// Llamar desde App.xaml.cs o desde un ViewModel de inicio.
    /// </summary>
    public static class AppLoader
    {
        // Variable global de sucursal activa (equivalente a sucursalActiva en VBA)
        //public static string SucursalActiva { get; set; } = "";

        // ─── Referencia corta al contenedor ──────────────────────────────────
        private static SqlData Sql => SqlData.Instance;

        // ─── ConectarProductos ────────────────────────────────────────────────
        /// <summary>
        /// Carga todos los catálogos/maestros al iniciar la aplicación.
        /// Equivalente a conectarProductos() en Módulo4.bas
        /// </summary>
        public static void ConectarProductos()
        {
            var inicio = DateTime.Now;

            Sql.UsuariosObj.Conectar("usuarios",
                "SELECT * FROM usuarios ORDER BY id ASC");

            Sql.StocksObj.Conectar("stocks",
                "SELECT * FROM stocks ORDER BY id ASC");

            Sql.ArticulosObj.Conectar("articulos",
                "SELECT * FROM articulos WHERE estadof = 'normal' ORDER BY familia ASC, indice ASC");

            Sql.FamiliasObj.Conectar("familias",
                "SELECT * FROM familias ORDER BY id ASC");

            Sql.ProductosObj.Conectar("productos",
                "SELECT * FROM productos ORDER BY id ASC");

            Sql.CategoriasObj.Conectar("Categorias",
                "SELECT * FROM Categorias ORDER BY id ASC");

            Sql.IndustriasObj.Conectar("industrias",
                "SELECT * FROM industrias ORDER BY id ASC");

            Sql.TercerosObj.Conectar("terceros",
                "SELECT * FROM terceros ORDER BY id ASC");

            Sql.SucursalesObj.Conectar("sucursales",
                "SELECT * FROM sucursales ORDER BY id ASC");

            Sql.PreciosObj.Conectar("precios",
                "SELECT * FROM precios ORDER BY fecha ASC");

            Sql.RegionesObj.Conectar("regiones",
                "SELECT * FROM regiones ORDER BY id ASC");

            var tiempo = DateTime.Now - inicio;
            System.Diagnostics.Debug.WriteLine($"ConectarProductos: {tiempo.TotalSeconds:F2}s");
        }

        // ─── ConectarBases ────────────────────────────────────────────────────
        /// <summary>
        /// Carga documentos de inventario filtrados por sucursal activa.
        /// Equivalente a conectarBases() en Módulo4.bas
        /// </summary>
        public static void ConectarBases()
        {
            long suc = AppState.SucursalActiva;

            Sql.DocumentosIObj.Conectar("documentosI",
                $"SELECT * FROM documentosI " +
                $"WHERE estadof = 'normal' AND sucursal = '{suc}' " +
                $"ORDER BY fecha ASC");

            Sql.InventariosObj.Conectar("inventarios",
                $"SELECT ctb2.* FROM inventarios AS ctb2 " +
                $"INNER JOIN documentosI AS ctb ON ctb2.documentoI = ctb.id " +
                $"WHERE ctb.estadof = 'normal' AND ctb.sucursal = '{suc}' " +
                $"ORDER BY documentoI ASC, indice ASC");
        }

        // ─── ConectarDocumentos ───────────────────────────────────────────────
        /// <summary>
        /// Carga documentos de venta/traslado en un rango de fechas.
        /// Equivalente a conectarDocumentos(apertura, cierre) en Módulo4.bas
        /// </summary>
        public static void ConectarDocumentos(DateTime apertura, DateTime cierre)
        {
            long suc = AppState.SucursalActiva;
            string aper = apertura.ToString("yyyyMMdd HH:mm:ss");
            string cier = cierre.ToString("yyyyMMdd HH:mm:ss");

            // ── DocumentosP ──────────────────────────────────────────────────
            Sql.DocumentosPObj.Conectar("documentosP",
                $"SELECT * FROM documentosP " +
                $"WHERE estadof = 'normal' " +
                $"AND fecha >= '{aper}' AND fecha <= '{cier}' " +
                $"AND sucursal = '{suc}' " +
                $"ORDER BY fecha ASC");

            // ── Pedidos ──────────────────────────────────────────────────────
            Sql.PedidosObj.Conectar("pedidos",
                $"SELECT vd.* FROM pedidos AS vd " +
                $"INNER JOIN documentosP AS vg ON vd.documentoP = vg.id " +
                $"WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                $"AND vg.sucursal = '{suc}' " +
                $"ORDER BY vd.documentoP ASC, vd.indice ASC");

            // ── Transacciones ────────────────────────────────────────────────
            Sql.TrasaccionesObj.Conectar("transacciones",
                $"SELECT vd.* FROM transacciones AS vd " +
                $"INNER JOIN documentosP AS vg ON vd.documentoP = vg.id " +
                $"WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                $"AND vg.sucursal = '{suc}' " +
                $"ORDER BY vd.fecha ASC");

            // ── Entregas ─────────────────────────────────────────────────────
            Sql.EntregasObj.Conectar("entregas",
                $"SELECT vd.* FROM entregas AS vd " +
                $"INNER JOIN documentosP AS vg ON vd.documentoP = vg.id " +
                $"WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                $"AND vg.sucursal = '{suc}' " +
                $"ORDER BY vd.documentoP ASC, vd.indice ASC");

            // ── DocumentosT (traspasos) ───────────────────────────────────────
            Sql.DocumentosTObj.Conectar("documentosT",
                $"SELECT * FROM documentosT " +
                $"WHERE estadof = 'normal' " +
                $"AND fecha >= '{aper}' AND fecha <= '{cier}' " +
                $"AND (destino = '{suc}' OR origen = '{suc}') " +
                $"ORDER BY fecha ASC");

            // ── Traspasos ─────────────────────────────────────────────────────
            Sql.TraspasosObj.Conectar("traspasos",
                $"SELECT vd.* FROM traspasos AS vd " +
                $"INNER JOIN documentosT AS vg ON vd.documentoT = vg.id " +
                $"WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                $"AND (vg.origen = '{suc}' OR vg.destino = '{suc}') " +
                $"ORDER BY vd.documentoT ASC, vd.indice ASC");
        }
    }
}
