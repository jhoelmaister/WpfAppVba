namespace TuProyecto.Data
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
        public DataConsulta UsuariosObj     { get; } = new();
        public DataConsulta StocksObj       { get; } = new();
        public DataConsulta ArticulosObj    { get; } = new();
        public DataConsulta FamiliasObj     { get; } = new();
        public DataConsulta ProductosObj    { get; } = new();
        public DataConsulta CategoriasObj   { get; } = new();
        public DataConsulta IndustriasObj   { get; } = new();
        public DataConsulta TercerosObj     { get; } = new();
        public DataConsulta SucursalesObj   { get; } = new();
        public DataConsulta PreciosObj      { get; } = new();
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

        private SqlData() { } // constructor privado → usar Instance
    }
}
