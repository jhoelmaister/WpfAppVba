using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace WpfAppVba.Data
{
    public class DataConsulta
    {
        private DataTable _tabla = new();
        private Dictionary<string, DataRow> _indiceId = new(); // id → DataRow, O(1)
        private string _nombreTabla = "";
        private string _consulta    = "";

        // ─── CONECTAR / CARGAR ────────────────────────────────────────────────

        public void Conectar(string tabla, string consulta = "")
        {
            _nombreTabla = tabla;
            _consulta    = string.IsNullOrEmpty(consulta)
                           ? $"SELECT * FROM {tabla} WHERE estadof = 'normal'"
                           : consulta;
            ObtenerDatos(_consulta);
        }

        public void Actualizar() => ObtenerDatos(_consulta);

        // ─── LECTURA DE DATOS ─────────────────────────────────────────────────

        public object? ObtenerItem(string columna, string id)
        {
            if (!_indiceId.TryGetValue(id.ToLower(), out var row)) return null;
            if (!_tabla.Columns.Contains(columna)) return null;
            var val = row[columna];
            return val is DBNull ? null : val;
        }

        public void EstablecerItem(string columna, string id, object? valor)
        {
            if (!_indiceId.TryGetValue(id.ToLower(), out var row)) return;
            if (!_tabla.Columns.Contains(columna))  return;
            if (!_tabla.Columns.Contains("estadof")) return;

            // Strings vacíos se persisten como NULL en SQL Server (consistente con INSERT).
            if (valor is string s && s.Length == 0) valor = null;
            row[columna] = valor ?? DBNull.Value;

            var estadoActual = row["estadof"]?.ToString() ?? "";
            if (estadoActual != "nuevo")
                row["estadof"] = "editado";
        }

        public object? Buscar(string columna, string valor, string columnaDevuelta)
        {
            if (!_tabla.Columns.Contains(columna) || !_tabla.Columns.Contains(columnaDevuelta)) return null;

            foreach (DataRow row in _tabla.Rows)
            {
                if (string.Equals(row[columna]?.ToString(), valor, StringComparison.OrdinalIgnoreCase))
                {
                    var res = row[columnaDevuelta];
                    return res is DBNull ? null : res;
                }
            }
            return null;
        }

        /// <summary>Devuelve el id (UUID) de la fila cuyo <paramref name="columna"/> = <paramref name="valor"/>.
        /// Cadena vacía si no se encuentra.</summary>
        public string BuscarIdentificador(string columna, string valor)
        {
            var res = Buscar(columna, valor, "id");
            return res?.ToString() ?? "";
        }

        /// <summary>Devuelve el ID de la fila por índice (base 1).</summary>
        public object? Mover(int fila)
        {
            int row = fila - 1;
            if (row < 0 || row >= _tabla.Rows.Count) return null;
            if (!_tabla.Columns.Contains("id")) return null;
            var val = _tabla.Rows[row]["id"];
            return val is DBNull ? null : val;
        }

        public int ContarFilas => _tabla.Rows.Count;

        public int IndiceColumna(string nombre)
        {
            if (!_tabla.Columns.Contains(nombre)) return -1;
            return _tabla.Columns[nombre]!.Ordinal;
        }

        // ─── MÁXIMO / VERIFICAR ───────────────────────────────────────────────

        public object? Maximo(string columna)
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand($"SELECT MAX({columna}) AS maximo FROM {_nombreTabla}", conn);
            var result = cmd.ExecuteScalar();
            return result is DBNull ? null : result;
        }

        public bool VerificarId(string valor, string columna)
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(
                $"SELECT COUNT(*) FROM {_nombreTabla} WHERE {columna} = @val", conn);
            cmd.Parameters.AddWithValue("@val", valor);
            int total = (int)cmd.ExecuteScalar()!;
            return total == 0;
        }

        // ─── GENERACIÓN DE CÓDIGO ─────────────────────────────────────────────

        /// <summary>
        /// Siguiente código entero para tablas maestras (familias, productos, etc.):
        /// MAX(codigo) + 1 considerando solo filas en estado normal.
        /// </summary>
        public int SiguienteCodigoInt()
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(
                $"SELECT ISNULL(MAX(CAST(codigo AS INT)), 0) + 1 FROM {_nombreTabla} " +
                $"WHERE estadof = 'normal' AND ISNUMERIC(codigo) = 1", conn);
            var r = cmd.ExecuteScalar();
            return (r is null or DBNull) ? 1 : Convert.ToInt32(r);
        }

        /// <summary>
        /// Siguiente número correlativo para documentos cuyo codigo = signo + número
        /// (ej. "A5"). Toma el MAX(número) de las filas en estado normal cuyo
        /// <paramref name="filtroColumna"/> = <paramref name="filtroValor"/>
        /// (ej. sucursal/region/origen activa) y devuelve número + 1.
        /// </summary>
        public int SiguienteNumeroDoc(string signo, string filtroColumna, string filtroValor)
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(
                $"SELECT codigo FROM {_nombreTabla} " +
                $"WHERE estadof = 'normal' AND {filtroColumna} = @f", conn);
            cmd.Parameters.AddWithValue("@f", filtroValor);

            int max = 0;
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                string c = rd[0]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(signo) && c.StartsWith(signo, StringComparison.OrdinalIgnoreCase))
                    c = c.Substring(signo.Length);
                if (int.TryParse(c, out int n) && n > max) max = n;
            }
            return max + 1;
        }

        /// <summary>
        /// Siguiente número correlativo para documentos cuyo codigo = signo + número,
        /// agrupado por EMPRESA. La empresa se obtiene por la cascada
        /// emitido (sucursal) → sucursales.empresa. Pensado para documentosT (traspasos).
        /// </summary>
        public int SiguienteNumeroDocPorEmpresa(string signo, string empresaId)
        {
            if (string.IsNullOrEmpty(empresaId)) return 1;

            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(
                $"SELECT d.codigo FROM {_nombreTabla} AS d " +
                $"INNER JOIN sucursales AS s ON s.id = d.emitido " +
                $"WHERE d.estadof = 'normal' AND s.empresa = @emp", conn);
            cmd.Parameters.AddWithValue("@emp", empresaId);

            int max = 0;
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                string c = rd[0]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(signo) && c.StartsWith(signo, StringComparison.OrdinalIgnoreCase))
                    c = c.Substring(signo.Length);
                if (int.TryParse(c, out int n) && n > max) max = n;
            }
            return max + 1;
        }

        /// <summary>
        /// Indica si ya existe otra fila (en estado normal) con el mismo codigo.
        /// Si se indica <paramref name="idActual"/>, esa fila se excluye (modo editar).
        /// </summary>
        public bool CodigoExiste(string codigo, string idActual = "")
        {
            var conn = DatabaseConnection.ObtenerConexion();
            string sql = $"SELECT COUNT(*) FROM {_nombreTabla} WHERE estadof = 'normal' AND codigo = @c";
            if (!string.IsNullOrEmpty(idActual)) sql += " AND id <> @id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@c", codigo);
            if (!string.IsNullOrEmpty(idActual)) cmd.Parameters.AddWithValue("@id", idActual);

            int total = (int)cmd.ExecuteScalar()!;
            return total > 0;
        }

        // ─── CRUD EN MEMORIA ──────────────────────────────────────────────────

        public void Nuevo(string id)
        {
            if (!_tabla.Columns.Contains("id") || !_tabla.Columns.Contains("estadof")) return;

            // Limpia cualquier fila en memoria con el mismo id (ej. de un intento de
            // guardado previo fallido que dejó una fila "nuevo" sin persistir).
            if (_indiceId.TryGetValue(id.ToLower(), out var existente))
            {
                _tabla.Rows.Remove(existente);
                _indiceId.Remove(id.ToLower());
            }

            var row = _tabla.NewRow();
            row["id"]      = id;
            row["estadof"] = "nuevo";
            _tabla.Rows.Add(row);
            _indiceId[id.ToLower()] = row;
        }

        public void Ocultar(string id)
        {
            if (!_indiceId.TryGetValue(id.ToLower(), out var row)) return;
            if (!_tabla.Columns.Contains("estadof")) return;
            row["estadof"] = "ocultado";
            _indiceId.Remove(id.ToLower());
        }

        public void Eliminar(string id)
        {
            if (!_indiceId.TryGetValue(id.ToLower(), out var row)) return;
            if (!_tabla.Columns.Contains("estadof")) return;
            row["estadof"] = "eliminado";
            _indiceId.Remove(id.ToLower());
        }

        // ─── ORDENAR ──────────────────────────────────────────────────────────

        public void OrdenarData(params (string columna, bool descendente)[] condiciones)
        {
            ExportarItems();
            EliminarItems();

            if (_tabla.Rows.Count == 0 || condiciones.Length == 0) return;

            string sortExpr = string.Join(", ",
                condiciones.Select(c => $"[{c.columna}] {(c.descendente ? "DESC" : "ASC")}"));

            _tabla.DefaultView.Sort = sortExpr;
            _tabla = _tabla.DefaultView.ToTable();

            _indiceId.Clear();
            foreach (DataRow row in _tabla.Rows)
            {
                var key = row["id"]?.ToString()?.ToLower() ?? "";
                if (!string.IsNullOrEmpty(key))
                    _indiceId[key] = row;
            }
        }

        // ─── EXPORTAR A SQL SERVER ────────────────────────────────────────────

        // Columnas autogeneradas por SQL Server que el app NO debe escribir
        // (columna IDENTITY 'secuencia'). Se excluyen de INSERT y UPDATE.
        private static bool EsAutogenerada(DataColumn c)
        {
            string n = c.ColumnName.ToLowerInvariant();
            return n == "secuencia" || n == "secuensia";
        }

        private IEnumerable<DataColumn> ColumnasPersistibles =>
            _tabla.Columns.Cast<DataColumn>().Where(c => !EsAutogenerada(c));

        public void ExportarItems()
        {
            if (!_tabla.Columns.Contains("estadof")) return;

            var conn       = DatabaseConnection.ObtenerConexion();
            var insertRows = new List<DataRow>();
            var updateRows = new List<DataRow>();

            foreach (DataRow row in _tabla.Rows)
            {
                switch (row["estadof"]?.ToString() ?? "")
                {
                    case "nuevo":     insertRows.Add(row);                        break;
                    // "ocultado" y "eliminado" son borrados LÓGICOS: se persisten con un
                    // UPDATE del estadof. NUNCA se borra físicamente una fila en SQL Server.
                    case "editado":
                    case "ocultado":
                    case "eliminado": updateRows.Add(row);                        break;
                }
            }

            // ── INSERCIONES ──────────────────────────────────────────────────
            if (insertRows.Count > 0)
            {
                string colsStr = string.Join(",",
                    ColumnasPersistibles.Select(c => c.ColumnName));

                foreach (var bloque in Chunks(insertRows, 1000))
                {
                    var values = new List<string>();
                    foreach (var row in bloque)
                    {
                        row["estadof"] = "normal";
                        values.Add("(" + FormatearFila(row) + ")");
                    }
                    string sql = $"INSERT INTO {_nombreTabla} ({colsStr}) VALUES {string.Join(",", values)}";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.ExecuteNonQuery();
                }
            }

            // ── ACTUALIZACIONES ──────────────────────────────────────────────
            if (updateRows.Count > 0)
            {
                var cols = ColumnasPersistibles
                    .Where(c => !string.Equals(c.ColumnName, "id", StringComparison.OrdinalIgnoreCase))
                    .ToList(); // sin id ni columnas autogeneradas (secuencia)
                string setParts  = string.Join(", ", cols.Select((c, i) => $"{c.ColumnName} = @p{i}"));
                string sqlUpdate = $"UPDATE {_nombreTabla} SET {setParts} WHERE id = @id";

                foreach (var row in updateRows)
                {
                    if (row["estadof"]?.ToString() == "editado")
                        row["estadof"] = "normal";

                    using var cmd = new SqlCommand(sqlUpdate, conn);
                    cmd.Parameters.AddWithValue("@id", row["id"] ?? DBNull.Value);
                    for (int i = 0; i < cols.Count; i++)
                    {
                        var v = row[cols[i]];
                        // strings vacíos → NULL en SQL Server
                        if (v is string s && s.Length == 0) v = DBNull.Value;
                        cmd.Parameters.AddWithValue($"@p{i}", v ?? DBNull.Value);
                    }
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ─── ELIMINAR FILAS OCULTAS/ELIMINADAS DE LA CACHÉ ───────────────────

        private void EliminarItems()
        {
            if (!_tabla.Columns.Contains("estadof")) return;

            var aEliminar = _tabla.Rows.Cast<DataRow>()
                .Where(r => r["estadof"]?.ToString() is "eliminado" or "ocultado")
                .ToList();

            foreach (var row in aEliminar)
                _tabla.Rows.Remove(row);
        }

        // ─── CARGA DESDE SQL SERVER ───────────────────────────────────────────

        private void ObtenerDatos(string consulta)
        {
            _tabla.Clear();
            _indiceId.Clear();

            var conn = DatabaseConnection.ObtenerConexion();
            using var adapter = new SqlDataAdapter(consulta, conn);
            adapter.Fill(_tabla);

            foreach (DataRow row in _tabla.Rows)
            {
                var key = row["id"]?.ToString()?.ToLower() ?? "";
                if (string.IsNullOrEmpty(key)) continue;

                // Omitir filas que no estén en estado normal
                if (_tabla.Columns.Contains("estadof"))
                {
                    var estado = row["estadof"]?.ToString() ?? "normal";
                    if (estado != "normal" && estado != "") continue;
                }

                _indiceId[key] = row;
            }
        }

        private string FormatearFila(DataRow row)
        {
            var partes = new List<string>();
            foreach (DataColumn col in ColumnasPersistibles)
            {
                var val = row[col];
                if (val == null || val is DBNull || val.ToString() == "")
                    partes.Add("NULL");
                else if (val is DateTime dt)
                    partes.Add($"'{dt:yyyyMMdd HH:mm:ss}'");
                else
                    partes.Add($"'{val.ToString()!.Replace("'", "''")}'");
            }
            return string.Join(",", partes);
        }

        private static IEnumerable<List<T>> Chunks<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
