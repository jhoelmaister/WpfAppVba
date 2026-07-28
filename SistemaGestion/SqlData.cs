namespace SistemaGestion.Data
{
    /// <summary>
    /// Equivalente a Conectar_Data.cls
    /// Contenedor singleton de todos los objetos DataConsulta.
    /// Usar como: SqlData.Instance.ArticulosObj.ObtenerItem(...)
    /// </summary>
    public class SqlData
    {
        // ─── Singleton ────────────────────────────────────────────────────────
        private static SqlData? _instance;
        public static SqlData Instance => _instance ??= new SqlData();

        // ─── Tablas de catálogo / maestros ───────────────────────────────────
        public DataConsulta EmpresasObj     { get; } = new();
        public DataConsulta UsuariosObj     { get; } = new();
        public DataConsulta ArticulosObj    { get; } = new();
        public DataConsulta FamiliasObj     { get; } = new();
        public DataConsulta ProductosObj    { get; } = new();
        public DataConsulta CategoriasObj   { get; } = new();
        public DataConsulta IndustriasObj   { get; } = new();
        public DataConsulta TercerosObj     { get; } = new();
        public DataConsulta SucursalesObj   { get; } = new();
        public DataConsulta RegionesObj     { get; } = new();

        // ─── Documentos e inventario ─────────────────────────────────────────
        public DataConsulta DocumentosIObj  { get; } = new();
        public DataConsulta InventariosObj  { get; } = new();

        // ─── Documentos de venta / pedidos ───────────────────────────────────
        public DataConsulta DocumentosPObj  { get; } = new();
        public DataConsulta PedidosObj      { get; } = new();
        public DataConsulta TrasaccionesObj { get; } = new();
        public DataConsulta EntregasObj     { get; } = new();
        public DataConsulta PagosObj        { get; } = new();

        // ─── Traspasos / retornos ────────────────────────────────────────────
        public DataConsulta DocumentosTObj  { get; } = new();
        public DataConsulta TraspasosObj    { get; } = new();

        // ─── Correcciones de stock ───────────────────────────────────────────
        public DataConsulta DocumentosCObj  { get; } = new();
        public DataConsulta CorreccionesObj { get; } = new();

        // ─── Listas de precios ────────────────────────────────────────────────
        public DataConsulta DocumentosLObj  { get; } = new();
        public DataConsulta PreciosObj      { get; } = new();

        // ─── Facturas ─────────────────────────────────────────────────────────
        public DataConsulta DocumentosFObj   { get; } = new();
        public DataConsulta FacturasObj      { get; } = new();
        public DataConsulta TransaccionesFObj{ get; } = new();

        private SqlData() { } // constructor privado → usar Instance
    }
}
