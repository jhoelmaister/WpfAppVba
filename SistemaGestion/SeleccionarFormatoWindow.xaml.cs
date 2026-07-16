using System.Windows;

namespace SistemaGestion
{
    // Diálogo modal chico: el llamador hace ShowDialog() y, si vuelve true,
    // lee FormatoElegido ("excel" o "pdf") para decidir qué generar.
    public partial class SeleccionarFormatoWindow : Window
    {
        public string FormatoElegido { get; private set; } = "";

        public SeleccionarFormatoWindow() => InitializeComponent();

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            FormatoElegido = "excel";
            DialogResult = true;
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            FormatoElegido = "pdf";
            DialogResult = true;
        }
    }
}
