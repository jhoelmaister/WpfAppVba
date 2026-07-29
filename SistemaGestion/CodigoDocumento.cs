using System;
using Microsoft.Data.SqlClient;

namespace SistemaGestion.Data
{
    /// <summary>
    /// Genera el <c>codigo</c> de un documento NUEVO, y lo revisa de nuevo al
    /// guardar. Sin signo: el código es el correlativo pelado (MAX + 1) dentro de
    /// su ámbito, y el ámbito es exactamente el mismo con el que
    /// <see cref="CodigoRegenerator"/> renumera — si los dos no coincidieran, al
    /// regenerar se moverían los códigos de lugar:
    ///   • documentosP/C/F → (empresa activa, movimiento normalizado): cada
    ///     venta/compra, repuesta/retirado e ingreso/egreso lleva su propio
    ///     correlativo 1..N. Ver <see cref="MovimientoSql"/>.
    ///   • documentosI/T/L → (empresa activa): un único correlativo 1..N, sin
    ///     partir por movimiento. Antes iban por sucursal (inventarios) o con
    ///     signo de empresa (traspasos, precios).
    /// La empresa se resuelve con la misma cascada que el regenerador:
    /// <c>sucursal</c> → <c>sucursales.empresa</c> (P/C/F/I), <c>emitido</c> →
    /// <c>sucursales.empresa</c> (T), o la columna <c>empresa</c> directa (L).
    /// </summary>
    public static class CodigoDocumento
    {
        // Ámbito = tabla + cómo se llega a la empresa + (opcional) cubo de
        // movimiento por el que se parte el correlativo.
        private sealed record Ambito(string Tabla, string Join, string Filtro, string? CasoMovimiento);

        private static readonly Ambito Pedidos = new(
            "documentosP",
            "INNER JOIN sucursales AS s ON s.id = d.sucursal",
            "s.empresa = @emp",
            MovimientoSql.CasoPedidos("d"));

        private static readonly Ambito Correcciones = new(
            "documentosC",
            "INNER JOIN sucursales AS s ON s.id = d.sucursal",
            "s.empresa = @emp",
            MovimientoSql.CasoCorrecciones("d"));

        private static readonly Ambito Facturas = new(
            "documentosF",
            "INNER JOIN sucursales AS s ON s.id = d.sucursal",
            "s.empresa = @emp",
            MovimientoSql.CasoFacturas("d"));

        private static readonly Ambito Inventarios = new(
            "documentosI",
            "INNER JOIN sucursales AS s ON s.id = d.sucursal",
            "s.empresa = @emp",
            null);

        private static readonly Ambito Traspasos = new(
            "documentosT",
            "INNER JOIN sucursales AS s ON s.id = d.emitido",
            "s.empresa = @emp",
            null);

        // documentosL.empresa es nvarchar (guarda el id como texto): compara directo.
        private static readonly Ambito Precios = new(
            "documentosL",
            "",
            "d.empresa = @emp",
            null);

        // ─── Código para un documento nuevo ───────────────────────────────────

        public static string SiguientePedido(string empresaId, string? movimiento) =>
            Siguiente(Pedidos, empresaId, MovimientoSql.NormalizarPedido(movimiento));

        public static string SiguienteCorreccion(string empresaId, string? movimiento) =>
            Siguiente(Correcciones, empresaId, MovimientoSql.NormalizarCorreccion(movimiento));

        public static string SiguienteFactura(string empresaId, string? movimiento) =>
            Siguiente(Facturas, empresaId, MovimientoSql.NormalizarFactura(movimiento));

        public static string SiguienteInventario(string empresaId) =>
            Siguiente(Inventarios, empresaId, null);

        public static string SiguienteTraspaso(string empresaId) =>
            Siguiente(Traspasos, empresaId, null);

        public static string SiguientePrecio(string empresaId) =>
            Siguiente(Precios, empresaId, null);

        // ─── Revisión al guardar ──────────────────────────────────────────────
        // El código se calcula al abrir el formulario, así que entre ese momento y
        // el guardado otro usuario (u otra sucursal de la misma empresa) pudo
        // haberse quedado con el número. Si sigue libre devuelve el mismo código;
        // si ya está tomado devuelve el siguiente libre, y el llamador avisa al
        // usuario del cambio.

        public static string VerificarPedido(string codigo, string empresaId, string? movimiento) =>
            Verificar(Pedidos, codigo, empresaId, MovimientoSql.NormalizarPedido(movimiento));

        public static string VerificarCorreccion(string codigo, string empresaId, string? movimiento) =>
            Verificar(Correcciones, codigo, empresaId, MovimientoSql.NormalizarCorreccion(movimiento));

        public static string VerificarFactura(string codigo, string empresaId, string? movimiento) =>
            Verificar(Facturas, codigo, empresaId, MovimientoSql.NormalizarFactura(movimiento));

        public static string VerificarInventario(string codigo, string empresaId) =>
            Verificar(Inventarios, codigo, empresaId, null);

        public static string VerificarTraspaso(string codigo, string empresaId) =>
            Verificar(Traspasos, codigo, empresaId, null);

        public static string VerificarPrecio(string codigo, string empresaId) =>
            Verificar(Precios, codigo, empresaId, null);

        // ─── Núcleo ───────────────────────────────────────────────────────────

        private static string Siguiente(Ambito a, string empresaId, string? movimiento)
        {
            if (string.IsNullOrEmpty(empresaId)) return "1";
            return (MaximoNumero(a, empresaId, movimiento) + 1).ToString();
        }

        private static string Verificar(Ambito a, string codigo, string empresaId, string? movimiento)
        {
            if (string.IsNullOrEmpty(empresaId) || string.IsNullOrEmpty(codigo)) return codigo;
            if (!Existe(a, codigo, empresaId, movimiento)) return codigo;
            return (MaximoNumero(a, empresaId, movimiento) + 1).ToString();
        }

        // MAX del correlativo del ámbito. No filtra por estadof: un número ya usado
        // no se reutiliza aunque su fila se haya ocultado o eliminado (mismo
        // criterio que tenían los SiguienteNumeroDoc* que este archivo reemplaza).
        private static int MaximoNumero(Ambito a, string empresaId, string? movimiento) => SqlRetry.Ejecutar(() =>
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(ArmarSql(a, "d.codigo", movimiento), conn);
            cmd.Parameters.AddWithValue("@emp", empresaId);
            if (movimiento != null) cmd.Parameters.AddWithValue("@mov", movimiento);

            int max = 0;
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                int n = NumeroDe(rd[0]?.ToString() ?? "");
                if (n > max) max = n;
            }
            return max;
        });

        private static bool Existe(Ambito a, string codigo, string empresaId, string? movimiento) => SqlRetry.Ejecutar(() =>
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(
                ArmarSql(a, "COUNT(*)", movimiento) + " AND d.codigo = @cod", conn);
            cmd.Parameters.AddWithValue("@emp", empresaId);
            if (movimiento != null) cmd.Parameters.AddWithValue("@mov", movimiento);
            cmd.Parameters.AddWithValue("@cod", codigo);

            var r = cmd.ExecuteScalar();
            return r is not (null or DBNull) && Convert.ToInt32(r) > 0;
        });

        private static string ArmarSql(Ambito a, string seleccion, string? movimiento)
        {
            string sql = $"SELECT {seleccion} FROM {a.Tabla} AS d ";
            if (a.Join.Length > 0) sql += a.Join + " ";
            sql += $"WHERE {a.Filtro}";
            if (movimiento != null && a.CasoMovimiento != null)
                sql += $" AND ({a.CasoMovimiento}) = @mov";
            return sql;
        }

        // Número de un código, tolerando los códigos viejos con signo ("A12" → 12)
        // que siguen en la base hasta que se corra el regenerador: sin esto el
        // correlativo se reiniciaría en 1 y chocaría con los documentos ya
        // numerados. Devuelve 0 si no termina en dígitos.
        private static int NumeroDe(string codigo)
        {
            int i = codigo.Length;
            while (i > 0 && char.IsDigit(codigo[i - 1])) i--;
            return i < codigo.Length && int.TryParse(codigo.Substring(i), out int n) ? n : 0;
        }
    }
}
