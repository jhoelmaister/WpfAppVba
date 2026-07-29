namespace SistemaGestion.Data
{
    /// <summary>
    /// Única fuente de verdad de los "cubos" de movimiento con los que se agrupan
    /// los correlativos de <c>codigo</c>. Cada tipo de documento expone dos formas
    /// del mismo criterio, que DEBEN coincidir siempre:
    ///   • <c>Caso*</c> → expresión SQL <c>CASE</c>, para agrupar/filtrar en el
    ///     servidor (la usan <see cref="CodigoRegenerator"/> al renumerar y
    ///     <see cref="CodigoDocumento"/> al buscar el siguiente número).
    ///   • <c>Normalizar*</c> → el mismo criterio en C#, para saber a qué cubo
    ///     pertenece el documento que se está creando.
    /// Normaliza además el vocabulario viejo al nuevo, para no partir en dos
    /// correlativos distintos documentos que son del mismo tipo real:
    ///   • documentosP: venta / compra.
    ///   • documentosC: "ingreso" viejo cuenta como repuesta; el resto, retirado.
    ///   • documentosF: criterio de mercadería — una "venta" vieja sale (egreso) y
    ///     una "compra" vieja entra (ingreso).
    /// Si se cambia un criterio acá, cambian a la vez la creación y la
    /// regeneración: por eso no debe duplicarse en ningún otro lado.
    /// </summary>
    public static class MovimientoSql
    {
        // ─── documentosP: venta / compra ──────────────────────────────────────
        public static string CasoPedidos(string alias) =>
            $"CASE WHEN LOWER({alias}.movimiento) = 'compra' THEN 'compra' ELSE 'venta' END";

        public static string NormalizarPedido(string? movimiento) =>
            (movimiento ?? "").Trim().ToLower() == "compra" ? "compra" : "venta";

        // ─── documentosC: repuesta / retirado ─────────────────────────────────
        public static string CasoCorrecciones(string alias) =>
            $"CASE WHEN LOWER({alias}.movimiento) IN ('repuesta', 'ingreso') " +
            $"THEN 'repuesta' ELSE 'retirado' END";

        public static string NormalizarCorreccion(string? movimiento) =>
            (movimiento ?? "").Trim().ToLower() is "repuesta" or "ingreso" ? "repuesta" : "retirado";

        // ─── documentosF: ingreso / egreso ────────────────────────────────────
        public static string CasoFacturas(string alias) =>
            $"CASE WHEN LOWER({alias}.movimiento) IN ('egreso', 'venta') " +
            $"THEN 'egreso' ELSE 'ingreso' END";

        public static string NormalizarFactura(string? movimiento) =>
            (movimiento ?? "").Trim().ToLower() is "egreso" or "venta" ? "egreso" : "ingreso";
    }
}
