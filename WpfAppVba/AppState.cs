using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace WpfAppVba.Data
{
    /// <summary>
    /// Equivalente a Cargar_General.bas
    /// Centraliza el estado global de la sesión y los métodos de carga/actualización.
    /// </summary>
    public static class AppState
    {
        // ─── Estado de sesión ─────────────────────────────────────────────────
        public static bool   SesionActiva      { get; set; } = false;
        public static string EmpresaActiva     { get; set; } = "";
        public static string RegionActiva      { get; set; } = "";
        public static string SucursalActiva    { get; set; } = "";
        public static string UsuarioActivo     { get; set; } = "";
        public static string AperturaIdActiva  { get; set; } = "";
        public static string TemaActivo        { get; set; } = "claro";
        public static string TipoUsuario       { get; set; } = "";
        public static bool   EsAdmin           => TipoUsuario == "admin";

        // ─── Actualización pendiente (Velopack) ──────────────────────────────
        // Null = sin actualización pendiente. Si tiene valor, hay una versión nueva
        // publicada: se bloquean guardados/eliminaciones/actualizaciones en toda la
        // app hasta que el usuario actualice y reinicie.
        public static string? VersionPendiente { get; set; }

        // ─── Rango de fechas activo ───────────────────────────────────────────
        public static DateTime DataFechaInicio { get; set; }
        public static DateTime DataFechaFinal  { get; set; }
        public static string   PeriodoActivo   { get; set; } = "";
        public static DateTime AperturaFecha   { get; set; }

        // ─── Tipos de operación ───────────────────────────────────────────────
        public static string TipoPedido      { get; set; } = "";
        public static string TipoMovimiento  { get; set; } = "";
        public static string TipoCorreccion  { get; set; } = "";
        public static string TipoValido      { get; set; } = "";

        // ─── Eventos de formularios (equivalente a eventoFormularioX) ─────────
        public static string EventoFormularioI { get; set; } = "";
        public static string EventoFormularioM { get; set; } = "";
        public static string EventoFormularioC { get; set; } = "";
        public static string EventoFormularioA { get; set; } = "";
        public static string EventoFormularioL { get; set; } = "";
        public static string EventoFormularioF { get; set; } = "";

        /// <summary>
        /// Snapshot de la apertura activa: [articuloId, fechaApertura, cantidad]
        /// Equivalente al array aperturaActiva(3, uf) de VBA.
        /// Se usa como base para calcular stock.
        /// </summary>
        public static AperturaItem[] AperturaActiva { get; set; } = Array.Empty<AperturaItem>();

        // ─── Referencia corta al contenedor de datos ──────────────────────────
        private static SqlData Sql => SqlData.Instance;

        // ─── ACTUALIZACIONES PARCIALES ────────────────────────────────────────

        /// <summary>Equivalente a ActualisarProductos()</summary>
        public static void ActualizarProductos()
        {
            Sql.ArticulosObj.Actualizar();
            Sql.ProductosObj.Actualizar();
            Sql.FamiliasObj.Actualizar();
            Sql.CategoriasObj.Actualizar();
        }

        /// <summary>Equivalente a ActualisarReferencias() — clientes y proveedores</summary>
        public static void ActualizarReferencias()
        {
            Sql.TercerosObj.Actualizar();   // clientes y proveedores están en terceros
        }

        /// <summary>Equivalente a ActualisarDocumentos()</summary>
        public static void ActualizarDocumentos()
        {
            Sql.TraspasosObj.Actualizar();
        }

        public static void IniciarApp() 
        {
            MessageBox.Show("¡Hola desde WPF!");
            AppLoader.ConectarProductos();
            var valor = SqlData.Instance.ArticulosObj.ObtenerItem("descripcion","1");
            MessageBox.Show($"Se agregaron {valor}");
            DatabaseConnection.CerrarConexion();
        }

  

        // ─── ACTUALIZAR BASE (cálculo de apertura y periodo) ─────────────────

        /// <summary>
        /// Equivalente a actualizarBase(periodo).
        /// Determina la fecha de apertura más reciente, arma el array AperturaActiva
        /// y establece DataFechaInicio / DataFechaFinal según el periodo solicitado.
        /// </summary>
        public static void ActualizarBase(int periodo)
        {
            DataFechaInicio = default;
            DataFechaFinal  = default;

            // ── Buscar el documentoI más reciente (mayor fecha) ──────────────
            string aperturaidEncontrada = "";
            DateTime maximo = default;
            bool encontrado = false;

            int ufDocs = Sql.DocumentosIObj.ContarFilas;
            for (int ciclo = 1; ciclo <= ufDocs; ciclo++)
            {
                var idObj = Sql.DocumentosIObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                var fechaObj = Sql.DocumentosIObj.ObtenerItem("fecha", id);
                if (fechaObj == null) continue;
                DateTime fecha = Convert.ToDateTime(fechaObj);

                if (fecha > maximo)
                {
                    maximo               = fecha;
                    aperturaidEncontrada = id;
                    encontrado           = true;
                }
            }

            DateTime inicio;

            if (encontrado)
            {
                // Apertura desde el último inventario
                inicio           = maximo;
                AperturaIdActiva = aperturaidEncontrada;
            }
            else
            {
                // Sin inventario previo: usar la fecha de creación de la sucursal
                var fechaSucObj = Sql.SucursalesObj.ObtenerItem("fecha", SucursalActiva);
                inicio = fechaSucObj != null ? Convert.ToDateTime(fechaSucObj) : DateTime.Today;
            }

            AperturaFecha = inicio;

            // Si hay puente de período (rama de abajo), los precios de ESTA pasada se
            // descartan apenas se recalculan con el corte final → no consultarlos todavía
            // evita una consulta a precios/documentosL que terminaría desperdiciada.
            // La cantidad SÍ hay que calcularla igual: ContarStock (rama del puente) lee
            // AppState.AperturaActiva como base, así que esta pasada debe quedar asignada
            // antes de que se ejecute.
            bool periodoPuente = inicio.Year != periodo;

            int ufArticulos     = Sql.ArticulosObj.ContarFilas;
            int ufInventarios   = encontrado ? Sql.InventariosObj.ContarFilas : 0;
            var apertura        = new AperturaItem[ufArticulos + 1]; // base-1 como VBA
            var preciosApertura = periodoPuente ? new Dictionary<string, double>() : ObtenerPreciosApertura(inicio);

            for (int ciclo = 1; ciclo <= ufArticulos; ciclo++)
            {
                var idObj = Sql.ArticulosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                double cantidad = 0;

                if (encontrado)
                {
                    // Buscar en inventarios la cantidad para este artículo en la apertura
                    for (int ciclo2 = 1; ciclo2 <= ufInventarios; ciclo2++)
                    {
                        var id2Obj = Sql.InventariosObj.Mover(ciclo2);
                        if (id2Obj == null) continue;
                        string id2 = id2Obj.ToString()!;

                        var aperturaidInv = Sql.InventariosObj.ObtenerItem("documentoI", id2)?.ToString() ?? "";
                        if (aperturaidEncontrada != aperturaidInv) continue;

                        var articuloId = Sql.InventariosObj.ObtenerItem("articulo", id2)?.ToString() ?? "";
                        if (articuloId != id) continue;

                        var cantObj = Sql.InventariosObj.ObtenerItem("cantidad", id2);
                        cantidad = cantObj != null ? Convert.ToDouble(cantObj) : 0;
                        break;
                    }
                }

                apertura[ciclo] = new AperturaItem
                {
                    ArticuloId = id,
                    Fecha      = inicio,
                    Cantidad   = cantidad,
                    Precio     = preciosApertura.TryGetValue(id, out var precioApertura) ? precioApertura : 0
                };
            }

            AperturaActiva = apertura;

            // ── Determinar rango final según el periodo solicitado ────────────
            DateTime final;

            if (!periodoPuente)
            {
                // Mismo año: rango hasta fin de año
                final = new DateTime(periodo, 12, 31, 23, 59, 59);
            }
            else
            {
                // Periodo diferente: proyectar la apertura recién asignada arriba (base de
                // ContarStock) hacia el inicio del nuevo periodo.
                final = new DateTime(periodo, 1, 1, 0, 0, 0);
                AppLoader.ConectarDocumentos(inicio, final); // primera carga (trae también precios/documentosL)

                var aperturaNueva        = new AperturaItem[ufArticulos + 1];
                // Segunda consulta evitada: se escanea el caché de precios/documentosL que
                // dejó la carga de arriba, en vez de volver a consultar la base.
                var preciosAperturaFinal = ObtenerPreciosAperturaDesdeCache();

                for (int ciclo = 1; ciclo <= ufArticulos; ciclo++)
                {
                    var idObj = Sql.ArticulosObj.Mover(ciclo);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;

                    aperturaNueva[ciclo] = new AperturaItem
                    {
                        ArticuloId = id,
                        Fecha      = final,
                        Cantidad   = StockCalculator.ContarStock(id, final),
                        Precio     = preciosAperturaFinal.TryGetValue(id, out var precioApertura) ? precioApertura : 0
                    };
                }

                AperturaActiva = aperturaNueva;
                inicio = new DateTime(periodo, 1, 1, 0, 0, 0);
                final  = new DateTime(periodo, 12, 31, 23, 59, 59);
            }

            DataFechaInicio = inicio;
            DataFechaFinal  = final;
        }

        /// <summary>
        /// Precio de apertura por artículo cuando NO hay puente de período: el de la
        /// documentoL más reciente con fecha &lt;= fechaCorte. Consulta directa a SQL
        /// Server porque en este caso el caché de precios/documentosL todavía no se
        /// cargó (recién se carga después, filtrado por período, desde el llamador).
        /// Cuando SÍ hay puente, ese caché ya lo carga ConectarDocumentos dentro de
        /// ActualizarBase → ver <see cref="ObtenerPreciosAperturaDesdeCache"/>.
        /// </summary>
        private static Dictionary<string, double> ObtenerPreciosApertura(DateTime fechaCorte)
        {
            var resultado = new Dictionary<string, double>();
            var conn = DatabaseConnection.ObtenerConexion();
            using var cmd = new SqlCommand(
                "SELECT p.articulo, p.precio FROM precios AS p " +
                "INNER JOIN documentosL AS dl ON p.documentoL = dl.id " +
                "INNER JOIN ( " +
                "    SELECT p2.articulo, MAX(dl2.fecha) AS maxFecha " +
                "    FROM precios AS p2 " +
                "    INNER JOIN documentosL AS dl2 ON p2.documentoL = dl2.id " +
                "    WHERE p2.estadof = 'normal' AND dl2.estadof = 'normal' AND dl2.fecha <= @fecha " +
                "    GROUP BY p2.articulo " +
                ") AS ult ON ult.articulo = p.articulo AND dl.fecha = ult.maxFecha " +
                "WHERE p.estadof = 'normal' AND dl.estadof = 'normal'", conn);
            cmd.Parameters.AddWithValue("@fecha", fechaCorte);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string articuloId = reader["articulo"].ToString() ?? "";
                if (articuloId == "" || resultado.ContainsKey(articuloId)) continue;
                resultado[articuloId] = Convert.ToDouble(reader["precio"]);
            }
            return resultado;
        }

        /// <summary>
        /// Precio de apertura por artículo cuando SÍ hay puente de período: el de mayor
        /// documentosL.fecha por artículo, escaneando el caché de Sql.PreciosObj /
        /// Sql.DocumentosLObj que ya dejó cargado AppLoader.ConectarDocumentos (llamado
        /// justo antes, dentro del mismo puente) — sin volver a consultar la base.
        /// </summary>
        private static Dictionary<string, double> ObtenerPreciosAperturaDesdeCache()
        {
            var resultado  = new Dictionary<string, double>();
            var mejorFecha = new Dictionary<string, DateTime>();

            int ufPrecios = Sql.PreciosObj.ContarFilas;
            for (int ciclo = 1; ciclo <= ufPrecios; ciclo++)
            {
                var idObj = Sql.PreciosObj.Mover(ciclo);
                if (idObj == null) continue;
                string id = idObj.ToString()!;

                string articuloId = Sql.PreciosObj.ObtenerItem("articulo", id)?.ToString() ?? "";
                if (articuloId == "") continue;

                string documentoL = Sql.PreciosObj.ObtenerItem("documentoL", id)?.ToString() ?? "";
                if (documentoL == "") continue;

                var fechaObj = Sql.DocumentosLObj.ObtenerItem("fecha", documentoL);
                if (fechaObj == null) continue;
                DateTime fecha = Convert.ToDateTime(fechaObj);

                if (mejorFecha.TryGetValue(articuloId, out var actual) && fecha <= actual) continue;

                mejorFecha[articuloId] = fecha;
                resultado[articuloId]  = Convert.ToDouble(Sql.PreciosObj.ObtenerItem("precio", id) ?? 0);
            }

            return resultado;
        }
    }

    // ─── Tipos auxiliares ─────────────────────────────────────────────────────

    /// <summary>Una fila del array aperturaActiva(3, uf) de VBA.</summary>
    public class AperturaItem
    {
        public string   ArticuloId { get; set; } = "";
        public DateTime Fecha      { get; set; }
        public double   Cantidad   { get; set; }
        public double   Precio     { get; set; }
    }

    /// <summary>Resultado de IniciarAsync() — indica qué ventana mostrar.</summary>
    public enum AccionInicio
    {
        SinConexion,
        MostrarLogin,
        MostrarPrincipal
    }
}
