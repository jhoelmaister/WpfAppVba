using System;
using System.Windows;
using System.Windows.Controls;
using SistemaGestion.Data;

namespace VisorEmpresa
{
    /// <summary>
    /// Duplicado de SistemaGestion.ArticulosDetalle para el visor: mismo layout
    /// (IDENTIFICACIÓN/CLASIFICACIÓN/DETALLES/OBSERVACIONES), pero siempre de
    /// SOLO LECTURA — abierto desde VisorEmpresa.ArticulosGeneral con doble
    /// clic para "ver artículo". Sin selectores de Familia/Industria/Categoría
    /// (abren FamiliasGeneral/IndustriasGeneral/CategoriasGeneral, que no están
    /// vinculados en VisorEmpresa.csproj) ni botón Ver Movimientos
    /// (MovimientosGeneral, tampoco vinculado): innecesarios en un formulario
    /// que nunca permite editar.
    /// </summary>
    public partial class ArticulosDetalle : UserControl
    {
        public event Action? Cerrando;
        private static SqlData Sql => SqlData.Instance;
        private readonly string _idVer;
        private bool _iniciado = false;

        public ArticulosDetalle(string idVer)
        {
            InitializeComponent();
            _idVer = idVer;
            Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarUserform(); };
        }

        private void CargarUserform()
        {
            string id = _idVer;

            Box_Codigo.Text = Sql.ArticulosObj.ObtenerItem("codigo", id)?.ToString() ?? "";
            Box_Indice.Text = Sql.ArticulosObj.ObtenerItem("indice", id)?.ToString() ?? "";

            string famId = Sql.ArticulosObj.ObtenerItem("familia", id)?.ToString() ?? "";
            Box_Familia.Text = CombinarCodigoDescripcion(
                Sql.FamiliasObj.ObtenerItem("codigo", famId)?.ToString() ?? "",
                Sql.FamiliasObj.ObtenerItem("descripcion", famId)?.ToString() ?? "");

            string indId = Sql.ArticulosObj.ObtenerItem("industria", id)?.ToString() ?? "";
            Box_Industria.Text = CombinarCodigoDescripcion(
                Sql.IndustriasObj.ObtenerItem("codigo", indId)?.ToString() ?? "",
                Sql.IndustriasObj.ObtenerItem("descripcion", indId)?.ToString() ?? "");

            string catId = Sql.ArticulosObj.ObtenerItem("Categoria", id)?.ToString() ?? "";
            Box_Categoria.Text = CombinarCodigoDescripcion(
                Sql.CategoriasObj.ObtenerItem("codigo", catId)?.ToString() ?? "",
                Sql.CategoriasObj.ObtenerItem("descripcion", catId)?.ToString() ?? "");

            Box_Descripcion.Text = Sql.ArticulosObj.ObtenerItem("descripcion", id)?.ToString() ?? "";
            Box_Modelo.Text      = Sql.ArticulosObj.ObtenerItem("modelo",      id)?.ToString() ?? "";
            Box_Observacion.Text = Sql.ArticulosObj.ObtenerItem("observacion", id)?.ToString() ?? "";

            LblTitulo.Text = string.IsNullOrEmpty(Box_Codigo.Text) ? "Artículo" : $"Artículo {Box_Codigo.Text}";
        }

        private static string CombinarCodigoDescripcion(string codigo, string descripcion)
        {
            if (string.IsNullOrEmpty(codigo)) return descripcion;
            if (string.IsNullOrEmpty(descripcion)) return codigo;
            return $"{codigo} - {descripcion}";
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e) => Cerrando?.Invoke();

        // ─── Llamado por el botón X de la pestaña ─────────────────────────────
        public void IntentarCerrar() => Cerrando?.Invoke();
    }
}
