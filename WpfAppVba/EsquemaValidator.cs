using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;

#pragma warning disable IDE0130
namespace WpfAppVba.Data
{
    /// <summary>Familia de tipo SQL esperada para una columna (no el tipo exacto:
    /// alcanza con la familia para detectar una base de datos incompatible sin
    /// ser frágil ante diferencias de precisión/longitud sin importancia).</summary>
    public enum TipoColumnaEsperado
    {
        Identificador, // uniqueidentifier
        Texto,         // char/varchar/nchar/nvarchar/text/ntext
        Fecha,         // date/datetime/datetime2/smalldatetime/datetimeoffset
        Numero,        // cualquier tipo numérico (incluye enteros)
        Entero,        // subconjunto de Numero: int/bigint/smallint/tinyint (secuencia/indice)
    }

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
    /// tenga las tablas/columnas/tipos que la app necesita. El catálogo de abajo
    /// se armó auditando todas las llamadas ObtenerItem/EstablecerItem (DataConsulta)
    /// y las consultas de AppLoader.cs/ConsultasEmpresa.cs — ambas apps
    /// (WpfAppVba y VisorEmpresa) comparten el mismo esquema de base de datos.
    /// </summary>
    public static class EsquemaValidator
    {
        private const TipoColumnaEsperado Id = TipoColumnaEsperado.Identificador;
        private const TipoColumnaEsperado Tx = TipoColumnaEsperado.Texto;
        private const TipoColumnaEsperado Fe = TipoColumnaEsperado.Fecha;
        private const TipoColumnaEsperado Nu = TipoColumnaEsperado.Numero;
        private const TipoColumnaEsperado En = TipoColumnaEsperado.Entero;

        // Columnas estructurales presentes en TODAS las tablas del esquema.
        private static readonly (string nombre, TipoColumnaEsperado tipo)[] Base =
        {
            ("id", Id), ("estadof", Tx), ("secuencia", En),
        };

        // Tabla → columnas específicas (además de las de Base) que la app necesita.
        private static readonly Dictionary<string, (string nombre, TipoColumnaEsperado tipo)[]> Manifiesto =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["usuarios"] = new[]
            {
                ("codigo", Tx), ("cuenta", Tx), ("llave", Tx), ("nombres", Tx), ("apellidos", Tx),
                ("tipo", Tx), ("empresa", Id), ("sucursal", Id),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["empresas"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("signo", Tx), ("observacion", Tx), ("fecha", Fe),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["articulos"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("modelo", Tx), ("observacion", Tx), ("indice", En),
                ("familia", Id), ("industria", Id), ("categoria", Id),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["familias"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("observacion", Tx), ("producto", Id),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["productos"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("empresa", Id),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["Categorias"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("empresa", Id),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["industrias"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("empresa", Id),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["terceros"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("direccion", Tx), ("nit", Tx),
                ("telefono", Tx), ("telefono2", Tx), ("contacto", Tx), ("contacto2", Tx),
                ("observacion", Tx), ("empresa", Id),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["sucursales"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("direccion", Tx), ("nit", Tx), ("telefono", Tx),
                ("signo", Tx), ("tipo", Tx), ("observacion", Tx), ("empresa", Id), ("region", Id),
                ("fecha", Fe), ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["regiones"] = new[]
            {
                ("codigo", Tx), ("descripcion", Tx), ("empresa", Id),
                ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["documentosI"] = new[]
            {
                ("codigo", Tx), ("observacion", Tx), ("referencia", Tx), ("sucursal", Id),
                ("fecha", Fe), ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["inventarios"] = new[]
            {
                ("articulo", Id), ("documentoI", Id), ("cantidad", Nu),
            },
            ["documentosP"] = new[]
            {
                ("codigo", Tx), ("observacion", Tx), ("referencia", Tx), ("estado", Tx), ("estadoc", Tx),
                ("movimiento", Tx), ("tipo", Tx), ("sucursal", Id), ("emitido", Id), ("tercero", Id),
                ("fecha", Fe), ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["pedidos"] = new[]
            {
                ("articulo", Id), ("documentoP", Id), ("cantidad", Nu), ("importe", Nu),
                ("contable", Nu), ("tipo", Tx), ("forma", Tx), ("indice", En),
            },
            ["transacciones"] = new[]
            {
                ("articulo", Id), ("documentoP", Id), ("cantidad", Nu), ("importe", Nu),
                ("forma", Tx), ("descripcion", Tx), ("fecha", Fe), ("indice", En),
            },
            ["entregas"] = new[]
            {
                ("articulo", Id), ("documentoP", Id), ("cantidad", Nu), ("fecha", Fe), ("indice", En),
            },
            ["documentosT"] = new[]
            {
                ("codigo", Tx), ("observacion", Tx), ("referencia", Tx), ("estado", Tx),
                ("movimiento", Tx), ("origen", Id), ("destino", Id), ("emitido", Id), ("sucursal", Id),
                ("fecha", Fe), ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["traspasos"] = new[]
            {
                ("articulo", Id), ("documentoT", Id), ("cantidad", Nu), ("indice", En),
            },
            ["documentosC"] = new[]
            {
                ("codigo", Tx), ("observacion", Tx), ("referencia", Tx), ("motivo", Tx),
                ("movimiento", Tx), ("sucursal", Id),
                ("fecha", Fe), ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["correcciones"] = new[]
            {
                ("articulo", Id), ("documentoC", Id), ("cantidad", Nu), ("indice", En),
            },
            ["documentosL"] = new[]
            {
                ("codigo", Tx), ("observacion", Tx), ("referencia", Tx), ("estado", Tx),
                ("region", Id), ("empresa", Id),
                ("fecha", Fe), ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["precios"] = new[]
            {
                ("articulo", Id), ("documentoL", Id), ("precio", Nu),
            },
            ["documentosF"] = new[]
            {
                ("codigo", Tx), ("observacion", Tx), ("referencia", Tx), ("estado", Tx), ("estadoc", Tx),
                ("movimiento", Tx), ("sucursal", Id), ("tercero", Id),
                ("fecha", Fe), ("emision", Fe), ("edicion", Fe), ("usuario", Id), ("usuarioe", Id),
            },
            ["facturas"] = new[]
            {
                ("categoria", Id), ("concepto", Tx), ("documentoF", Id), ("importe", Nu), ("indice", En),
            },
            ["transaccionesF"] = new[]
            {
                ("descripcion", Tx), ("documentoF", Id), ("fecha", Fe), ("forma", Tx),
                ("importe", Nu), ("indice", En),
            },
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

            var columnasPorTabla = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqlCommand(
                "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS", conn))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    string tabla   = rd.GetString(0);
                    string columna = rd.GetString(1);
                    string tipo    = rd.GetString(2);
                    if (!columnasPorTabla.TryGetValue(tabla, out var cols))
                        columnasPorTabla[tabla] = cols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    cols[columna] = tipo;
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
                columnasReales ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var (nombreCol, tipoEsperado) in Base.Concat(columnasEsperadas))
                {
                    if (!columnasReales.TryGetValue(nombreCol, out var tipoReal))
                    {
                        resultado.Problemas.Add(new ProblemaEsquema
                        {
                            Tabla   = tabla,
                            Columna = nombreCol,
                            Detalle = $"{tabla}.{nombreCol}: falta la columna."
                        });
                        continue;
                    }

                    if (!TipoCompatible(tipoEsperado, tipoReal))
                    {
                        resultado.Problemas.Add(new ProblemaEsquema
                        {
                            Tabla   = tabla,
                            Columna = nombreCol,
                            Detalle = $"{tabla}.{nombreCol}: tipo \"{tipoReal}\" incompatible " +
                                      $"(se esperaba {DescripcionTipo(tipoEsperado)})."
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

        private static bool TipoCompatible(TipoColumnaEsperado esperado, string tipoReal)
        {
            string t = tipoReal.ToLowerInvariant();
            return esperado switch
            {
                TipoColumnaEsperado.Identificador => t == "uniqueidentifier",
                TipoColumnaEsperado.Texto         => t is "char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext",
                TipoColumnaEsperado.Fecha         => t is "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset",
                TipoColumnaEsperado.Numero        => t is "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "numeric" or "float" or "real" or "money" or "smallmoney",
                TipoColumnaEsperado.Entero        => t is "int" or "bigint" or "smallint" or "tinyint",
                _ => true
            };
        }

        private static string DescripcionTipo(TipoColumnaEsperado t) => t switch
        {
            TipoColumnaEsperado.Identificador => "uniqueidentifier",
            TipoColumnaEsperado.Texto         => "texto (char/varchar/nvarchar)",
            TipoColumnaEsperado.Fecha         => "fecha/hora (datetime)",
            TipoColumnaEsperado.Numero        => "numérico",
            TipoColumnaEsperado.Entero        => "entero",
            _ => "desconocido"
        };

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
