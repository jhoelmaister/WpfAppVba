using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;

#pragma warning disable IDE0130
namespace SistemaGestion.Data
{
    public class ProblemaEsquema
    {
        public string  Tabla   { get; set; } = "";
        public string? Columna { get; set; } // null = falta la tabla completa
        public string  Detalle { get; set; } = "";
    }

    public class ResultadoValidacionEsquema
    {
        public bool EsCompatible => Problemas.Count == 0;
        public List<ProblemaEsquema> Problemas { get; } = new();
    }

    /// <summary>
    /// Verifica, ANTES de dejar usar una base de datos como conexión activa, que
    /// tenga las tablas/columnas que la app necesita.
    ///
    /// Por ahora SOLO valida existencia de tabla y columna, sin tipos: la primera
    /// versión validaba tipos de dato inferidos del código y dio falsos positivos
    /// contra la base real (ej. "codigo" es int en varias tablas maestras aunque
    /// el código sugería texto, o "documentosL.empresa" es nvarchar en vez de
    /// uniqueidentifier pero igual funciona porque las consultas comparan contra
    /// literales de texto entrecomillados). El catálogo de columnas por tabla es
    /// un volcado del esquema real (script.sql de la base "edberBase" del
    /// usuario), no una suposición — así que ya no debería haber falsos positivos
    /// de columnas "faltantes" que en realidad nunca existieron a propósito (ej.
    /// usuarios no tiene emision/edicion/usuario/usuarioE, documentosT no tiene
    /// movimiento/sucursal).
    /// </summary>
    public static class EsquemaValidator
    {
        // Columnas estructurales presentes en TODAS las tablas del esquema.
        private static readonly string[] Base = { "id", "estadof" };

        // Tabla → columnas específicas (además de las de Base). Volcado 1:1 del
        // script real de la base de datos (script.sql, edberBase) — no son
        // suposiciones: cada tabla lista exactamente las columnas que esa base
        // tiene hoy. "usuarios" y "documentosT", por ejemplo, no tienen columnas
        // emision/edicion/usuario/usuarioE ni movimiento/sucursal respectivamente
        // aunque el código a veces las toque vía ObtenerItem/EstablecerItem (ese
        // acceso ya degrada solo si la columna no existe, ver DataConsulta.cs).
        private static readonly Dictionary<string, string[]> Manifiesto =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["usuarios"]       = new[] { "secuencia", "cuenta", "llave", "nombres", "apellidos", "tipo", "temaC", "codigo", "sucursal", "empresa" },
            ["empresas"]       = new[] { "secuencia", "descripcion", "signo", "observacion", "fecha", "emision", "edicion", "usuario", "usuarioE", "codigo" },
            ["articulos"]      = new[] { "secuencia", "descripcion", "indice", "modelo", "observacion", "estado", "emision", "edicion", "codigo", "categoria", "familia", "industria", "usuario", "usuarioE" },
            ["familias"]       = new[] { "secuencia", "descripcion", "observacion", "emision", "edicion", "codigo", "producto", "usuario", "usuarioE" },
            ["productos"]      = new[] { "secuencia", "descripcion", "emision", "edicion", "codigo", "usuario", "usuarioE", "empresa" },
            ["Categorias"]     = new[] { "secuencia", "descripcion", "emision", "edicion", "codigo", "usuario", "usuarioE", "empresa" },
            ["industrias"]     = new[] { "secuencia", "descripcion", "emision", "edicion", "codigo", "usuario", "usuarioE", "empresa" },
            ["terceros"]       = new[] { "secuencia", "nit", "descripcion", "telefono", "contacto", "direccion", "contacto2", "telefono2", "observacion", "emision", "edicion", "codigo", "usuario", "usuarioE", "empresa" },
            ["sucursales"]     = new[] { "secuencia", "nit", "descripcion", "direccion", "telefono", "observacion", "emision", "edicion", "fecha", "codigo", "region", "usuario", "usuarioE", "signo", "empresa", "tipo" },
            ["regiones"]       = new[] { "secuencia", "descripcion", "emision", "edicion", "codigo", "usuario", "usuarioE", "signo", "empresa" },
            ["documentosL"]    = new[] { "secuencia", "codigo", "fecha", "emision", "edicion", "observacion", "usuario", "usuarioE", "referencia", "region", "estado", "empresa" },
            ["precios"]        = new[] { "secuencia", "precio", "articulo", "documentoL" },
            ["documentosI"]    = new[] { "secuencia", "observacion", "fecha", "emision", "edicion", "codigo", "sucursal", "usuario", "usuarioE", "referencia" },
            ["inventarios"]    = new[] { "secuencia", "cantidad", "documentoI", "articulo" },
            ["documentosP"]    = new[] { "secuencia", "fecha", "estado", "tipo", "emision", "edicion", "referencia", "movimiento", "observacion", "estadoC", "codigo", "sucursal", "emitido", "usuario", "usuarioE", "tercero" },
            ["pedidos"]        = new[] { "secuencia", "indice", "cantidad", "importe", "tipo", "forma", "contable", "documentoP", "articulo" },
            ["transacciones"]  = new[] { "secuencia", "fecha", "descripcion", "indice", "importe", "forma", "documentoP" },
            ["entregas"]       = new[] { "secuencia", "indice", "cantidad", "fecha", "documentoP", "articulo" },
            ["documentosT"]    = new[] { "secuencia", "fecha", "estado", "emision", "edicion", "referencia", "observacion", "codigo", "origen", "destino", "emitido", "usuario", "usuarioE" },
            ["traspasos"]      = new[] { "secuencia", "indice", "cantidad", "documentoT", "articulo" },
            ["documentosC"]    = new[] { "secuencia", "fecha", "emision", "edicion", "referencia", "movimiento", "observacion", "motivo", "codigo", "sucursal", "usuario", "usuarioE" },
            ["correcciones"]   = new[] { "secuencia", "indice", "cantidad", "documentoC", "articulo" },
            ["documentosF"]    = new[] { "secuencia", "codigo", "fecha", "emision", "edicion", "observacion", "referencia", "sucursal", "usuario", "usuarioE", "estado", "estadoC", "movimiento", "tercero" },
            ["facturas"]       = new[] { "secuencia", "indice", "concepto", "importe", "documentoF", "categoria" },
            ["transaccionesF"] = new[] { "secuencia", "fecha", "descripcion", "indice", "importe", "forma", "documentoF" },
        };

        // ─── Validar una conexión ya abierta ─────────────────────────────────
        public static ResultadoValidacionEsquema Validar(SqlConnection conn)
        {
            var resultado = new ResultadoValidacionEsquema();

            var tablasExistentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand(
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", conn))
            using (var rd = cmd.ExecuteReader())
                while (rd.Read()) tablasExistentes.Add(rd.GetString(0));

            var columnasPorTabla = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand(
                "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS", conn))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    string tabla   = rd.GetString(0);
                    string columna = rd.GetString(1);
                    if (!columnasPorTabla.TryGetValue(tabla, out var cols))
                        columnasPorTabla[tabla] = cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    cols.Add(columna);
                }
            }

            foreach (var (tabla, columnasEsperadas) in Manifiesto)
            {
                if (!tablasExistentes.Contains(tabla))
                {
                    resultado.Problemas.Add(new ProblemaEsquema
                    {
                        Tabla   = tabla,
                        Detalle = $"Falta la tabla \"{tabla}\"."
                    });
                    continue; // sin la tabla no tiene sentido revisar sus columnas
                }

                columnasPorTabla.TryGetValue(tabla, out var columnasReales);
                columnasReales ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var nombreCol in Base.Concat(columnasEsperadas))
                {
                    if (!columnasReales.Contains(nombreCol))
                    {
                        resultado.Problemas.Add(new ProblemaEsquema
                        {
                            Tabla   = tabla,
                            Columna = nombreCol,
                            Detalle = $"{tabla}.{nombreCol}: falta la columna."
                        });
                    }
                }
            }

            return resultado;
        }

        // ─── Validar credenciales sueltas (sin tocar la conexión global) ─────
        // Abre una conexión propia y desechable, igual que DatabaseConnection.Sondear,
        // para no interferir con la conexión activa mientras se prueba otro servidor.
        public static ResultadoValidacionEsquema ValidarConexion(
            string server, string database, string user, string password, int timeoutSeg = 15)
        {
            string cs = $"Server={server};Database={database};User Id={user};Password={password};" +
                        $"Application Name=edber;Connect Timeout={timeoutSeg};Command Timeout={timeoutSeg};" +
                        "TrustServerCertificate=True;";
            using var conn = new SqlConnection(cs);
            conn.Open();
            return Validar(conn);
        }

        // ─── Texto legible para mostrar en un MessageBox (acotado) ───────────
        public static string DescribirProblemas(ResultadoValidacionEsquema resultado, int maxLineas = 20)
        {
            var lineas = resultado.Problemas.Select(p => p.Detalle).ToList();
            if (lineas.Count <= maxLineas)
                return string.Join("\n", lineas);

            int restantes = lineas.Count - maxLineas;
            return string.Join("\n", lineas.Take(maxLineas)) + $"\n… y {restantes} problema(s) más.";
        }
    }
}
