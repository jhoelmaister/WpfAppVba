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
    /// tenga las tablas/columnas que la app necesita para no fallar con un SQL
    /// error ("Invalid object/column name") al conectar.
    ///
    /// Solo exige columnas referenciadas LITERALMENTE en el texto de las consultas
    /// (WHERE/JOIN/ORDER BY/SELECT explícito) de AppLoader.cs y ConsultasEmpresa.cs —
    /// las que de verdad rompen la conexión si faltan. NO exige las columnas que la
    /// app solo lee/escribe vía DataConsulta.ObtenerItem/EstablecerItem: esas ya
    /// degradan solas (DataConsulta las ignora en silencio si no existen), así que
    /// no son requisito real para conectar.
    ///
    /// Tampoco valida TIPOS de columna: en la práctica el esquema real varía más de
    /// lo que el código deja ver (ej. "codigo" es int en varias tablas maestras
    /// aunque no en todas, o algunas columnas tipo "id" son nvarchar en vez de
    /// uniqueidentifier) y como las consultas comparan contra literales de texto
    /// entrecomillados, SQL Server los compara igual sin romperse. Validar tipos
    /// daba muchos falsos positivos contra bases reales — mejor solo existencia.
    /// </summary>
    public static class EsquemaValidator
    {
        // Columnas estructurales presentes en TODAS las tablas del esquema.
        private static readonly string[] Base = { "id", "estadof" };

        // Tabla → columnas específicas (además de las de Base) referenciadas en
        // consultas SQL crudas (no solo vía ObtenerItem/EstablecerItem).
        private static readonly Dictionary<string, string[]> Manifiesto =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["usuarios"]       = new[] { "secuencia", "cuenta", "llave" },
            ["empresas"]       = new[] { "secuencia" },
            ["familias"]       = new[] { "descripcion", "producto" },
            ["articulos"]      = new[] { "familia", "indice", "categoria" },
            ["productos"]      = new[] { "descripcion", "empresa" },
            ["Categorias"]     = new[] { "descripcion", "empresa" },
            ["industrias"]     = new[] { "descripcion", "empresa" },
            ["terceros"]       = new[] { "descripcion", "empresa" },
            ["sucursales"]     = new[] { "descripcion", "empresa" },
            ["regiones"]       = new[] { "descripcion", "empresa" },
            ["documentosL"]    = new[] { "fecha", "empresa" },
            ["precios"]        = new[] { "documentoL" },
            ["documentosI"]    = new[] { "sucursal", "fecha" },
            ["inventarios"]    = new[] { "documentoI", "articulo", "cantidad" },
            ["documentosP"]    = new[] { "fecha", "sucursal", "movimiento", "estado" },
            ["pedidos"]        = new[] { "documentoP", "indice", "articulo", "cantidad" },
            ["transacciones"]  = new[] { "documentoP", "indice" },
            ["entregas"]       = new[] { "documentoP", "indice" },
            ["documentosT"]    = new[] { "fecha", "origen", "destino" },
            ["traspasos"]      = new[] { "documentoT", "indice", "articulo", "cantidad" },
            ["documentosC"]    = new[] { "fecha", "sucursal", "movimiento" },
            ["correcciones"]   = new[] { "documentoC", "indice", "articulo", "cantidad" },
            ["documentosF"]    = new[] { "fecha", "sucursal" },
            ["facturas"]       = new[] { "documentoF", "indice" },
            ["transaccionesF"] = new[] { "documentoF", "indice" },
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
