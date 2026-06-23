using System;
using Microsoft.Data.SqlClient;

namespace WpfAppVba.Data
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

        // ─── ConectarUsuarios ─────────────────────────────────────────────────
        /// <summary>
        /// Carga la tabla de usuarios y empresas completas. Se llama DESPUÉS de
        /// autenticar (ver <see cref="ValidarLogin"/>), nunca antes: así no se
        /// descargan las contraseñas de todas las cuentas a un cliente sin loguear.
        /// El resto de catálogos se cargan luego con <see cref="ConectarProductos"/>,
        /// ya filtrados por la empresa del usuario.
        /// </summary>
        public static void ConectarUsuarios()
        {
            Sql.UsuariosObj.Conectar("usuarios",
                "SELECT * FROM usuarios WHERE estadof = 'normal' ORDER BY secuencia ASC");
            // Tabla de empresas (sin filtro de empresa).
            Sql.EmpresasObj.Conectar("empresas",
                "SELECT * FROM empresas WHERE estadof = 'normal' ORDER BY secuencia ASC");
        }

        // ─── ValidarLogin ─────────────────────────────────────────────────────
        /// <summary>
        /// Valida una cuenta/contraseña con una consulta puntual y parametrizada,
        /// SIN descargar la tabla completa de usuarios (con todas las contraseñas)
        /// antes de autenticar. Devuelve el id del usuario si las credenciales son
        /// correctas, o cadena vacía si no.
        /// Migra automáticamente contraseñas antiguas en texto plano: si la
        /// contraseña almacenada no tiene el formato de hash pero coincide en texto
        /// plano, la re-hashea y la guarda antes de devolver el id.
        /// </summary>
        public static string ValidarLogin(string cuenta, string contrasena)
        {
            var conn = DatabaseConnection.ObtenerConexion();

            string id = "";
            string llaveDb = "";
            using (var cmd = new SqlCommand(
                "SELECT id, llave FROM usuarios WHERE cuenta = @cuenta AND estadof = 'normal'", conn))
            {
                cmd.Parameters.AddWithValue("@cuenta", cuenta);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    id      = reader["id"].ToString() ?? "";
                    llaveDb = reader["llave"] is DBNull ? "" : reader["llave"].ToString() ?? "";
                }
            }

            if (string.IsNullOrEmpty(id)) return "";

            if (PasswordHasher.Verificar(contrasena, llaveDb)) return id;

            // Migración: contraseña antigua en texto plano que coincide → re-hashear.
            if (!PasswordHasher.EsHash(llaveDb) && llaveDb == contrasena)
            {
                using var cmdUpd = new SqlCommand(
                    "UPDATE usuarios SET llave = @llave WHERE id = @id", conn);
                cmdUpd.Parameters.AddWithValue("@llave", PasswordHasher.Hashear(contrasena));
                cmdUpd.Parameters.AddWithValue("@id", new Guid(id));
                cmdUpd.ExecuteNonQuery();
                return id;
            }

            return "";
        }

        // ─── ConectarProductos ────────────────────────────────────────────────
        /// <summary>
        /// Carga todos los catálogos/maestros al iniciar la aplicación.
        /// Equivalente a conectarProductos() en Módulo4.bas
        /// </summary>
        public static void ConectarProductos()
        {
            var inicio = DateTime.Now;

            string emp = AppState.EmpresaActiva;
            // Filtro directo por empresa (solo cuando hay empresa activa). Aplica a
            // productos, industrias, terceros, regiones, sucursales, categorías y
            // documentosL (columna empresa propia, sin cascada por región).
            string fEmp = string.IsNullOrEmpty(emp) ? "" : $" AND empresa = '{emp}'";

            // Cascada de la empresa por relaciones (igual que documentosT→traspasos), en
            // vez de confiar en la columna 'empresa' de las tablas hijas:
            //   productos (empresa) → familias (familias.producto) → articulos (articulos.familia)
            string fFamilias = string.IsNullOrEmpty(emp) ? ""
                : $" AND producto IN (SELECT id FROM productos WHERE estadof = 'normal' AND empresa = '{emp}')";
            string fArticulos = string.IsNullOrEmpty(emp) ? ""
                : $" AND a.familia IN (SELECT id FROM familias WHERE estadof = 'normal'" +
                  $" AND producto IN (SELECT id FROM productos WHERE estadof = 'normal' AND empresa = '{emp}'))";
            // precios no tiene columna empresa → cascada por los documentosL de la empresa.
            string fPrecios = string.IsNullOrEmpty(emp)
                ? ""
                : $" AND documentoL IN (SELECT id FROM documentosL WHERE estadof = 'normal'{fEmp})";



            // usuarios: NO se filtra por empresa (necesario para el login).
            Sql.UsuariosObj.Conectar("usuarios",
                "SELECT * FROM usuarios WHERE estadof = 'normal' ORDER BY secuencia ASC");

            // 'familia' es uniqueidentifier: ordenar por ese GUID no respeta el orden de
            // familias. Se hace JOIN para ordenar por la 'secuencia' de la familia.
            // LEFT JOIN para no perder artículos sin familia (quedan al inicio). SELECT a.*
            // conserva el esquema de la caché idéntico al de la tabla articulos.
            // Filtro en cascada: solo artículos cuya familia pertenece a la empresa activa.
            Sql.ArticulosObj.Conectar("articulos",
                $"SELECT a.* FROM articulos AS a " +
                $"LEFT JOIN familias AS f ON a.familia = f.id " +
                $"WHERE a.estadof = 'normal'{fArticulos} ORDER BY f.secuencia ASC, a.indice ASC");

            // Filtro en cascada: solo familias cuyo producto pertenece a la empresa activa.
            Sql.FamiliasObj.Conectar("familias",
                $"SELECT * FROM familias WHERE estadof = 'normal'{fFamilias} ORDER BY secuencia ASC");

            Sql.ProductosObj.Conectar("productos",
                $"SELECT * FROM productos WHERE estadof = 'normal'{fEmp} ORDER BY secuencia ASC");

            Sql.CategoriasObj.Conectar("Categorias",
                $"SELECT * FROM Categorias WHERE estadof = 'normal'{fEmp} ORDER BY secuencia ASC");

            Sql.IndustriasObj.Conectar("industrias",
                $"SELECT * FROM industrias WHERE estadof = 'normal'{fEmp} ORDER BY secuencia ASC");

            Sql.TercerosObj.Conectar("terceros",
                $"SELECT * FROM terceros WHERE estadof = 'normal'{fEmp} ORDER BY secuencia ASC");

            Sql.SucursalesObj.Conectar("sucursales",
                $"SELECT * FROM sucursales WHERE estadof = 'normal'{fEmp} ORDER BY secuencia ASC");

            Sql.RegionesObj.Conectar("regiones",
                $"SELECT * FROM regiones WHERE estadof = 'normal'{fEmp} ORDER BY secuencia ASC");

            // documentosL (cabecera de listas de precios) + precios (líneas). Filtro
            // directo por empresa (columna propia, ya no se cascada por región).
            Sql.DocumentosLObj.Conectar("documentosL",
                $"SELECT * FROM documentosL WHERE estadof = 'normal'{fEmp} ORDER BY fecha ASC");

            Sql.PreciosObj.Conectar("precios",
                $"SELECT * FROM precios WHERE estadof = 'normal'{fPrecios} ORDER BY documentoL ASC");

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
            // Sucursal vacía (usuario sin sucursal): usar un GUID nulo válido para que
            // la comparación contra la columna uniqueidentifier no falle y devuelva 0 filas.
            string suc = string.IsNullOrEmpty(AppState.SucursalActiva)
                ? "00000000-0000-0000-0000-000000000000"
                : AppState.SucursalActiva;

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
            // Sucursal vacía (usuario sin sucursal): usar un GUID nulo válido para que
            // la comparación contra la columna uniqueidentifier no falle y devuelva 0 filas.
            string suc = string.IsNullOrEmpty(AppState.SucursalActiva)
                ? "00000000-0000-0000-0000-000000000000"
                : AppState.SucursalActiva;
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
                $"ORDER BY vd.documentoP ASC, vd.indice ASC");

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

            // ── DocumentosC (correcciones de stock) ───────────────────────────
            Sql.DocumentosCObj.Conectar("documentosC",
                $"SELECT * FROM documentosC " +
                $"WHERE estadof = 'normal' " +
                $"AND fecha >= '{aper}' AND fecha <= '{cier}' " +
                $"AND sucursal = '{suc}' " +
                $"ORDER BY fecha ASC");

            // ── Correcciones ──────────────────────────────────────────────────
            Sql.CorreccionesObj.Conectar("correcciones",
                $"SELECT vd.* FROM correcciones AS vd " +
                $"INNER JOIN documentosC AS vg ON vd.documentoC = vg.id " +
                $"WHERE vg.estadof = 'normal' " +
                $"AND vg.fecha >= '{aper}' AND vg.fecha <= '{cier}' " +
                $"AND vg.sucursal = '{suc}' " +
                $"ORDER BY vd.documentoC ASC, vd.indice ASC");
        }
    }
}
