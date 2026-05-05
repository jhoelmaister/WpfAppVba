using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace TuProyecto.Data
{
    /// <summary>
    /// Equivalente a Data_Consultas.cls
    ///
    /// Arquitectura de caché en memoria:
    ///   _mData  = object[columnas, filas]   → datos reales (equivalente a mData1)
    ///   _mCols  = object[1, columnas]       → nombres de columnas (equivalente a mData2)
    ///   _mFilas = Dictionary&lt;id, índice&gt;   → acceso O(1) por ID
    ///   _mCols  = Dictionary&lt;nombre, índice&gt; → acceso O(1) por nombre de columna
    /// </summary>
    public class DataConsulta
    {
        // ─── Almacenamiento interno ───────────────────────────────────────────
        private object[,]? _mData;                         // [col, fila]
        private string[]   _colNames = Array.Empty<string>(); // nombres de columnas
        private Dictionary<string, int> _mFilas   = new(); // id   → índice fila
        private Dictionary<string, int> _mColumnas = new(); // nombre → índice columna
        private string _nombreTabla = "";
        private string _consulta    = "";

        // ─── CONECTAR / CARGAR ────────────────────────────────────────────────
        /// <summary>
        /// Equivalente a: sqlData.obj.conectar(consulta) = tabla
        /// </summary>
        public void Conectar(string tabla, string consulta = "")
        {
            _nombreTabla = tabla;
            _consulta    = string.IsNullOrEmpty(consulta)
                           ? $"SELECT * FROM {tabla}"
                           : consulta;
            ObtenerDatos(_consulta);
        }

        /// <summary>Vuelve a ejecutar la última consulta (refresca la caché).</summary>
        public void Actualizar() => ObtenerDatos(_consulta);

        // ─── LECTURA DE DATOS ─────────────────────────────────────────────────

        /// <summary>Equivalente a items(columna, id) get → devuelve el valor de la celda.</summary>
        public object? ObtenerItem(string columna, string id)
        {
            if (_mData == null) return null;
            if (!_mColumnas.TryGetValue(columna.ToLower(), out int col)) return null;
            if (!_mFilas.TryGetValue(id.ToLower(),         out int row)) return null;
            var val = _mData[col, row];
            return val is DBNull ? null : val;
        }

        /// <summary>Equivalente a items(columna, id) let → modifica la celda y marca la fila como "editado".</summary>
        public void EstablecerItem(string columna, string id, object? valor)
        {
            if (_mData == null) return;
            if (!_mColumnas.TryGetValue(columna.ToLower(),   out int col))    return;
            if (!_mColumnas.TryGetValue("estadof",           out int colEst)) return;
            if (!_mFilas.TryGetValue(id.ToLower(),           out int row))    return;

            _mData[col, row] = valor ?? DBNull.Value;

            var estadoActual = _mData[colEst, row]?.ToString() ?? "";
            if (estadoActual != "nuevo")
                _mData[colEst, row] = "editado";
        }

        /// <summary>
        /// Equivalente a buscar(columna, valor, columnaDevuelta).
        /// Busca la primera fila donde columna == valor y devuelve columnaDevuelta.
        /// </summary>
        public object? Buscar(string columna, string valor, string columnaDevuelta)
        {
            if (_mData == null) return null;
            if (!_mColumnas.TryGetValue(columna.ToLower(),         out int col))  return null;
            if (!_mColumnas.TryGetValue(columnaDevuelta.ToLower(), out int col2)) return null;

            int filas = _mData.GetLength(1);
            for (int i = 0; i < filas; i++)
            {
                var celda = _mData[col, i]?.ToString() ?? "";
                if (string.Equals(celda, valor, StringComparison.OrdinalIgnoreCase))
                {
                    var res = _mData[col2, i];
                    return res is DBNull ? null : res;
                }
            }
            return null;
        }

        /// <summary>Busca la primera fila donde columna == valor y devuelve el ID.</summary>
        public long BuscarIdentificador(string columna, string valor)
        {
            var res = Buscar(columna, valor, "id");
            return res != null ? Convert.ToInt64(res) : 0;
        }

        /// <summary>Equivalente a mover(fila) → devuelve el ID de la fila por índice (base 1).</summary>
        public object? Mover(int fila)
        {
            if (_mData == null) return null;
            if (!_mColumnas.TryGetValue("id", out int col)) return null;
            int row = fila - 1;
            if (row < 0 || row >= _mData.GetLength(1)) return null;
            var val = _mData[col, row];
            return val is DBNull ? null : val;
        }

        /// <summary>Cantidad de filas cargadas en la caché.</summary>
        public int ContarFilas => _mData?.GetLength(1) ?? 0;

        /// <summary>Devuelve el índice de columna por nombre.</summary>
        public int IndiceColumna(string nombre) =>
            _mColumnas.TryGetValue(nombre.ToLower(), out int idx) ? idx : -1;

        // ─── MÁXIMO / VERIFICAR ───────────────────────────────────────────────

        /// <summary>Equivalente a maximo(columna) → ejecuta MAX() directo en SQL.</summary>
        public object? Maximo(string columna)
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand($"SELECT MAX({columna}) AS maximo FROM {_nombreTabla}", conn);
            var result = cmd.ExecuteScalar();
            return result is DBNull ? null : result;
        }

        /// <summary>Equivalente a verificarId → devuelve true si el valor NO existe en la columna.</summary>
        public bool VerificarId(string valor, string columna)
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(
                $"SELECT COUNT(*) FROM {_nombreTabla} WHERE {columna} = @val", conn);
            cmd.Parameters.AddWithValue("@val", valor);
            int total = (int)cmd.ExecuteScalar()!;
            return total == 0;
        }

        // ─── CRUD EN MEMORIA ──────────────────────────────────────────────────

        /// <summary>Agrega una nueva fila en memoria con estado "nuevo".</summary>
        public void Nuevo(string id)
        {
            if (!_mColumnas.TryGetValue("id",      out int colId))  return;
            if (!_mColumnas.TryGetValue("estadof", out int colEst)) return;

            int numCols = _colNames.Length;

            if (_mData == null)
            {
                _mData = new object[numCols, 1];
                _mData[colId,  0] = id;
                _mData[colEst, 0] = "nuevo";
                _mFilas[id.ToLower()] = 0;
            }
            else
            {
                int filas   = _mData.GetLength(1);
                var nueva   = new object[numCols, filas + 1];
                // Copiar datos existentes
                for (int c = 0; c < numCols; c++)
                    for (int r = 0; r < filas; r++)
                        nueva[c, r] = _mData[c, r];
                // Nueva fila
                nueva[colId,  filas] = id;
                nueva[colEst, filas] = "nuevo";
                _mData = nueva;
                _mFilas[id.ToLower()] = filas;
            }
        }

        /// <summary>Marca la fila como "ocultado" y la elimina del índice de filas.</summary>
        public void Ocultar(string id)
        {
            if (_mData == null) return;
            if (!_mColumnas.TryGetValue("estadof", out int col)) return;
            if (!_mFilas.TryGetValue(id.ToLower(), out int row)) return;
            _mData[col, row] = "ocultado";
            _mFilas.Remove(id.ToLower());
        }

        /// <summary>Marca la fila como "eliminado" y la elimina del índice de filas.</summary>
        public void Eliminar(string id)
        {
            if (_mData == null) return;
            if (!_mColumnas.TryGetValue("estadof", out int col)) return;
            if (!_mFilas.TryGetValue(id.ToLower(), out int row)) return;
            _mData[col, row] = "eliminado";
            _mFilas.Remove(id.ToLower());
        }

        // ─── ORDENAR ──────────────────────────────────────────────────────────

        /// <summary>
        /// Equivalente a ordenarData(columna1, desc1, columna2, desc2, ...)
        /// Primero exporta cambios pendientes, elimina filas marcadas,
        /// ordena en memoria y recarga el diccionario.
        /// </summary>
        public void OrdenarData(params (string columna, bool descendente)[] condiciones)
        {
            ExportarItems();
            EliminarItems();

            if (_mData == null || condiciones.Length == 0) return;

            int filas = _mData.GetLength(1);
            int cols  = _mData.GetLength(0);

            // Convertir a lista de arrays para poder ordenarla
            var lista = new List<object[]>(filas);
            for (int r = 0; r < filas; r++)
            {
                var fila = new object[cols];
                for (int c = 0; c < cols; c++)
                    fila[c] = _mData[c, r];
                lista.Add(fila);
            }

            // Ordenar multi-columna
            lista.Sort((a, b) =>
            {
                foreach (var (columna, desc) in condiciones)
                {
                    if (!_mColumnas.TryGetValue(columna.ToLower(), out int ci)) continue;
                    int cmp = Comparar(a[ci], b[ci]);
                    if (cmp != 0) return desc ? -cmp : cmp;
                }
                return 0;
            });

            // Volver al array 2D
            for (int r = 0; r < filas; r++)
                for (int c = 0; c < cols; c++)
                    _mData[c, r] = lista[r][c];

            CargarDiccionario();
        }

        private static int Comparar(object? a, object? b)
        {
            if (a == null || a is DBNull) return b == null || b is DBNull ? 0 : -1;
            if (b == null || b is DBNull) return 1;
            if (a is IComparable ca) return ca.CompareTo(b);
            return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        // ─── EXPORTAR A SQL SERVER ────────────────────────────────────────────

        /// <summary>
        /// Equivalente a exportarItems().
        /// Envía a SQL Server todas las filas con estado "eliminado", "nuevo" o "editado"/"ocultado"
        /// usando operaciones batch de hasta 1000 registros.
        /// </summary>
        public void ExportarItems()
        {
            if (_mData == null) return;
            if (!_mColumnas.TryGetValue("estadof", out int colEst)) return;

            int filas    = _mData.GetLength(1);
            int numCols  = _mData.GetLength(0);
            var conn     = DatabaseConnection.ObtenerConexion();

            var deleteIds  = new List<string>();
            var insertRows = new List<int>();
            var updateRows = new List<int>();

            for (int i = 0; i < filas; i++)
            {
                var estado = _mData[colEst, i]?.ToString() ?? "";
                switch (estado)
                {
                    case "eliminado": deleteIds.Add(i.ToString());  break;   // usaremos el ID real abajo
                    case "nuevo":     insertRows.Add(i);            break;
                    case "editado":
                    case "ocultado":  updateRows.Add(i);            break;
                }
            }

            // ── ELIMINACIONES ────────────────────────────────────────────────
            if (deleteIds.Count > 0 && _mColumnas.TryGetValue("id", out int colId))
            {
                var ids = new List<string>();
                for (int i = 0; i < filas; i++)
                    if ((_mData[colEst, i]?.ToString() ?? "") == "eliminado")
                        ids.Add(_mData[colId, i]?.ToString() ?? "");

                foreach (var bloque in Chunks(ids, 1000))
                {
                    string lista = string.Join(",", bloque);
                    using var cmd = new SqlCommand(
                        $"DELETE FROM {_nombreTabla} WHERE id IN ({lista})", conn);
                    cmd.ExecuteNonQuery();
                }
            }

            // ── INSERCIONES ──────────────────────────────────────────────────
            if (insertRows.Count > 0)
            {
                string colsStr = string.Join(",", _colNames);
                foreach (var bloque in Chunks(insertRows, 1000))
                {
                    var values = new List<string>();
                    foreach (int r in bloque)
                    {
                        _mData[colEst, r] = "normal";
                        values.Add("(" + FormatearFila(r, numCols) + ")");
                    }
                    string sql = $"INSERT INTO {_nombreTabla} ({colsStr}) VALUES {string.Join(",", values)}";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.ExecuteNonQuery();
                }
            }

            // ── ACTUALIZACIONES (UPDATE con VALUES trick) ────────────────────
            if (updateRows.Count > 0 && _mColumnas.TryGetValue("id", out colId))
            {
                foreach (var bloque in Chunks(updateRows, 1000))
                {
                    foreach (int r in bloque)
                    {
                        if ((_mData[colEst, r]?.ToString() ?? "") == "editado")
                            _mData[colEst, r] = "normal";
                    }

                    // Construir SET dinámico
                    var setClauses = new List<string>();
                    for (int c = 1; c < numCols; c++)          // saltar columna id (col 0)
                        setClauses.Add($"{_colNames[c]} = @c{c}_{"{r}"}");

                    // UPDATE individual por fila (más limpio en C# que el VALUES trick de VBA)
                    foreach (int r in bloque)
                    {
                        var sqlParts = new List<string>();
                        for (int c = 1; c < numCols; c++)
                            sqlParts.Add($"{_colNames[c]} = @p{c}");

                        string sql = $"UPDATE {_nombreTabla} SET {string.Join(", ", sqlParts)} WHERE id = @id";
                        using var cmd = new SqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@id", _mData[colId, r] ?? DBNull.Value);
                        for (int c = 1; c < numCols; c++)
                            cmd.Parameters.AddWithValue($"@p{c}", _mData[c, r] ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private string FormatearFila(int row, int numCols)
        {
            var partes = new List<string>();
            for (int c = 0; c < numCols; c++)
            {
                var val = _mData![c, row];
                if (val == null || val is DBNull || val.ToString() == "")
                    partes.Add("NULL");
                else if (val is DateTime dt)
                    partes.Add($"'{dt:yyyyMMdd HH:mm:ss}'");
                else
                    partes.Add($"'{val.ToString()!.Replace("'", "''")}'");
            }
            return string.Join(",", partes);
        }

        // ─── ELIMINAR FILAS OCULTAS/ELIMINADAS DE LA CACHÉ ───────────────────

        private void EliminarItems()
        {
            if (_mData == null) return;
            if (!_mColumnas.TryGetValue("estadof", out int colEst)) return;

            int filas   = _mData.GetLength(1);
            int numCols = _mData.GetLength(0);

            var filasValidas = new List<int>();
            for (int i = 0; i < filas; i++)
            {
                var estado = _mData[colEst, i]?.ToString() ?? "";
                if (estado != "eliminado" && estado != "ocultado")
                    filasValidas.Add(i);
            }

            if (filasValidas.Count == 0)
            {
                _mData = null;
                return;
            }

            if (filasValidas.Count == filas) return; // nada que hacer

            var nueva = new object[numCols, filasValidas.Count];
            for (int k = 0; k < filasValidas.Count; k++)
            {
                int orig = filasValidas[k];
                for (int c = 0; c < numCols; c++)
                    nueva[c, k] = _mData[c, orig];
            }
            _mData = nueva;
        }

        // ─── CARGA DESDE SQL SERVER ───────────────────────────────────────────

        private void ObtenerDatos(string consulta)
        {
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd    = new SqlCommand(consulta, conn);
            using var reader = cmd.ExecuteReader();

            // Leer nombres de columnas
            int numCols = reader.FieldCount;
            if (numCols == 0) throw new Exception($"La tabla/consulta no devolvió columnas: {consulta}");

            _colNames = new string[numCols];
            for (int c = 0; c < numCols; c++)
                _colNames[c] = reader.GetName(c);

            // Leer todas las filas en una lista primero
            var rows = new List<object[]>();
            while (reader.Read())
            {
                var row = new object[numCols];
                reader.GetValues(row);
                rows.Add(row);
            }
            reader.Close();

            // Convertir a array 2D [col, fila] (mismo layout que VBA mData1)
            if (rows.Count > 0)
            {
                _mData = new object[numCols, rows.Count];
                for (int r = 0; r < rows.Count; r++)
                    for (int c = 0; c < numCols; c++)
                        _mData[c, r] = rows[r][c];
            }
            else
            {
                _mData = null;
            }

            CargarDiccionario();
        }

        private void CargarDiccionario()
        {
            _mFilas    = new Dictionary<string, int>();
            _mColumnas = new Dictionary<string, int>();

            // Columnas
            for (int c = 0; c < _colNames.Length; c++)
                _mColumnas[_colNames[c].ToLower()] = c;

            // Filas (indexadas por el valor de la columna 0, que es el ID)
            if (_mData != null)
            {
                int filas = _mData.GetLength(1);
                for (int r = 0; r < filas; r++)
                {
                    var key = _mData[0, r]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(key))
                        _mFilas[key.ToLower()] = r;
                }
            }
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────

        private static IEnumerable<List<T>> Chunks<T>(List<T> source, int size)
        {
            for (int i = 0; i < source.Count; i += size)
                yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }
}
