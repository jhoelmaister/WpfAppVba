using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfAppVba
{
    internal static class GridFocusHelper
    {
        // Devuelve el foco de teclado a la celda seleccionada del DataGrid.
        // Diferido a DispatcherPriority.Background para correr después de que WPF
        // restaure el foco propio al cerrar un ShowDialog(), y establece CurrentCell
        // explícitamente para que ArrowUp/ArrowDown vuelvan a navegar.
        internal static void EnfocarCeldaSeleccionada(DataGrid grid)
        {
            grid.Dispatcher.BeginInvoke(new Action(() =>
            {
                var item = grid.SelectedItem;
                if (item == null) { grid.Focus(); return; }

                grid.ScrollIntoView(item);
                grid.UpdateLayout();

                var row = grid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                if (row == null) { grid.Focus(); return; }

                var cell = ObtenerCelda(row, 0);
                if (cell != null)
                {
                    grid.CurrentCell = new DataGridCellInfo(item, grid.Columns[0]);
                    cell.Focus();
                    Keyboard.Focus(cell);
                }
                else
                {
                    row.Focus();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static DataGridCell? ObtenerCelda(DataGridRow row, int columna)
        {
            var presenter = BuscarHijoVisual<DataGridCellsPresenter>(row);
            return presenter?.ItemContainerGenerator.ContainerFromIndex(columna) as DataGridCell;
        }

        private static T? BuscarHijoVisual<T>(DependencyObject parent) where T : DependencyObject
        {
            int n = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < n; i++)
            {
                var hijo = VisualTreeHelper.GetChild(parent, i);
                if (hijo is T encontrado) return encontrado;
                var resultado = BuscarHijoVisual<T>(hijo);
                if (resultado != null) return resultado;
            }
            return null;
        }
    }
}
