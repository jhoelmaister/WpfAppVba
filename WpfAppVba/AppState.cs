using System;
using System.Threading.Tasks;
using System.Windows;

namespace TuProyecto.Data
{
    /// <summary>
    /// Equivalente a Cargar_General.bas
    /// Centraliza el estado global de la sesión y los métodos de carga/actualización.
    /// </summary>
    public static class AppState
    {
        // ─── Estado de sesión ─────────────────────────────────────────────────
        public static bool   SesionActiva      { get; set; } = false;
        public static long   RegionActiva      { get; set; }
        public static long   SucursalActiva    { get; set; }
        public static long   UsuarioActivo     { get; set; }
        public static long   AperturaIdActiva  { get; set; }

        // ─── Rango de fechas activo ───────────────────────────────────────────
        public static DateTime DataFechaInicio { get; set; }
        public static DateTime DataFechaFinal  { get; set; }
        public static string   PeriodoActivo   { get; set; } = "";
        public static DateTime AperturaFecha   { get; set; }

        // ─── Tipos de operación ───────────────────────────────────────────────
        public static string TipoPedido      { get; set; } = "";
        public static string TipoMovimiento  { get; set; } = "";
        public static string TipoValido      { get; set; } = "";

        // ─── Eventos de formularios (equivalente a eventoFormularioX) ─────────
        public static string EventoFormularioI { get; set; } = "";
        public static string EventoFormularioM { get; set; } = "";
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
            var uf = SqlData.Instance.ArticulosObj.ObtenerItem("descripcion","1");
            MessageBox.Show($"Se agregaron {uf}");
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
                    maximo            = fecha;
                    aperturaidEncontrada = id;
                    encontrado        = true;
                }
            }

            DateTime inicio;

            if (encontrado)
            {
                // Apertura desde el último inventario
                inicio        = maximo;
                AperturaFecha = inicio;
                AperturaIdActiva = Convert.ToInt64(aperturaidEncontrada);

                int ufArticulos   = Sql.ArticulosObj.ContarFilas;
                int ufInventarios = Sql.InventariosObj.ContarFilas;
                var apertura      = new AperturaItem[ufArticulos + 1]; // base-1 como VBA

                for (int ciclo = 1; ciclo <= ufArticulos; ciclo++)
                {
                    var idObj = Sql.ArticulosObj.Mover(ciclo);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;

                    double cantidad = 0;

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

                    apertura[ciclo] = new AperturaItem
                    {
                        ArticuloId = id,
                        Fecha      = inicio,
                        Cantidad   = cantidad
                    };
                }

                AperturaActiva = apertura;
            }
            else
            {
                // Sin inventario previo: usar la fecha de creación de la sucursal
                var fechaSucObj = Sql.SucursalesObj.ObtenerItem("fecha", SucursalActiva.ToString());
                inicio        = fechaSucObj != null ? Convert.ToDateTime(fechaSucObj) : DateTime.Today;
                AperturaFecha = inicio;

                int ufArticulos = Sql.ArticulosObj.ContarFilas;
                var apertura    = new AperturaItem[ufArticulos + 1];

                for (int ciclo = 1; ciclo <= ufArticulos; ciclo++)
                {
                    var idObj = Sql.ArticulosObj.Mover(ciclo);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;

                    apertura[ciclo] = new AperturaItem
                    {
                        ArticuloId = id,
                        Fecha      = inicio,
                        Cantidad   = 0
                    };
                }

                AperturaActiva = apertura;
            }

            // ── Determinar rango final según el periodo solicitado ────────────
            DateTime final;

            if (inicio.Year == periodo)
            {
                // Mismo año: rango hasta fin de año
                final = new DateTime(periodo, 12, 31, 23, 59, 59);
            }
            else
            {
                // Periodo diferente: calcular apertura al inicio del periodo
                final = new DateTime(periodo, 1, 1, 0, 0, 0);
                AppLoader.ConectarDocumentos(inicio, final);

                int ufArticulos = Sql.ArticulosObj.ContarFilas;
                var aperturaNueva = new AperturaItem[ufArticulos + 1];

                for (int ciclo = 1; ciclo <= ufArticulos; ciclo++)
                {
                    var idObj = Sql.ArticulosObj.Mover(ciclo);
                    if (idObj == null) continue;
                    string id = idObj.ToString()!;

                    aperturaNueva[ciclo] = new AperturaItem
                    {
                        ArticuloId = id,
                        Fecha      = final,
                        Cantidad   = StockCalculator.ContarStock(id, final)
                    };
                }

                AperturaActiva = aperturaNueva;
                inicio = new DateTime(periodo, 1, 1, 0, 0, 0);
                final  = new DateTime(periodo, 12, 31, 23, 59, 59);
            }

            DataFechaInicio = inicio;
            DataFechaFinal  = final;
        }
    }

    // ─── Tipos auxiliares ─────────────────────────────────────────────────────

    /// <summary>Una fila del array aperturaActiva(3, uf) de VBA.</summary>
    public class AperturaItem
    {
        public string   ArticuloId { get; set; } = "";
        public DateTime Fecha      { get; set; }
        public double   Cantidad   { get; set; }
    }

    /// <summary>Resultado de IniciarAsync() — indica qué ventana mostrar.</summary>
    public enum AccionInicio
    {
        SinConexion,
        MostrarLogin,
        MostrarPrincipal
    }
}
