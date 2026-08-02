using System;
using System.Windows;
using System.Windows.Controls;

namespace VisorEmpresa
{
    /// <summary>
    /// Panel de encabezados del TabControl de la consola, al estilo Chrome.
    ///
    /// El <see cref="TabPanel"/> por defecto de WPF apila las pestañas en varias
    /// filas cuando no entran a lo ancho, y esas filas le comen alto al contenido.
    /// Acá siempre hay UNA sola fila: cuando no entran todas, se ocultan las más
    /// ANTIGUAS para que se vean las más recientes. Las nuevas se insertan justo
    /// después de la fija (ver AbrirPestaña), así que la fila va de la más nueva a
    /// la más vieja y las que se caen del borde derecho son las viejas.
    ///
    /// Quedan siempre visibles:
    ///   • la primera pestaña (la fija de la sección — es el "home" del panel), y
    ///   • la pestaña seleccionada (si se elige una oculta desde el botón "▾",
    ///     entra a la vista sola).
    ///
    /// Las ocultas no desaparecen: se llegan por el botón "▾" de la barra, que
    /// abre la lista completa (ver ConsolaVisor.BtnPestanasTodas_Click).
    /// </summary>
    public class TabStripPanel : Panel
    {
        private TabControl? _tabControl;

        public TabStripPanel()
        {
            Loaded   += (_, _) => Enganchar();
            Unloaded += (_, _) => Desenganchar();
        }

        private void Enganchar()
        {
            if (_tabControl != null) return;
            _tabControl = ItemsControl.GetItemsOwner(this) as TabControl;
            if (_tabControl != null) _tabControl.SelectionChanged += OnSelectionChanged;
        }

        private void Desenganchar()
        {
            if (_tabControl == null) return;
            _tabControl.SelectionChanged -= OnSelectionChanged;
            _tabControl = null;
        }

        /// <summary>
        /// Cambiar de pestaña puede hacer entrar o salir pestañas del espacio
        /// visible (la seleccionada siempre se muestra) y eso no invalida el
        /// layout por sí solo: hay que pedirlo a mano.
        /// </summary>
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectionChanged burbujea desde cualquier Selector de adentro del
            // contenido (grillas, combos): solo interesa el del propio TabControl.
            if (!ReferenceEquals(e.OriginalSource, _tabControl)) return;
            InvalidateArrange();
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var libre = new Size(double.PositiveInfinity, double.PositiveInfinity);
            double alto = 0, anchoTotal = 0;

            foreach (UIElement hijo in InternalChildren)
            {
                hijo.Measure(libre);
                alto        = Math.Max(alto, hijo.DesiredSize.Height);
                anchoTotal += hijo.DesiredSize.Width;
            }

            double ancho = double.IsInfinity(constraint.Width)
                           ? anchoTotal
                           : Math.Min(anchoTotal, constraint.Width);
            return new Size(ancho, alto);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int total = InternalChildren.Count;
            if (total == 0) return finalSize;

            var visible   = new bool[total];
            double usado  = 0;

            // 1. La pestaña fija (la primera) siempre se ve.
            visible[0] = true;
            usado     += InternalChildren[0].DesiredSize.Width;

            // 2. La seleccionada también, esté donde esté.
            int sel = IndiceSeleccionado();
            if (sel > 0)
            {
                visible[sel] = true;
                usado       += InternalChildren[sel].DesiredSize.Width;
            }

            // 3. El resto se llena de izquierda a derecha. Las pestañas nuevas se
            //    insertan justo después de la fija (ver AbrirPestaña), así que ese
            //    recorrido va de la MÁS RECIENTE a la MÁS ANTIGUA: en cuanto una no
            //    entra se corta, y las que quedan afuera son siempre las viejas.
            for (int i = 1; i < total; i++)
            {
                if (visible[i]) continue;
                double ancho = InternalChildren[i].DesiredSize.Width;
                if (usado + ancho > finalSize.Width) break;
                visible[i] = true;
                usado     += ancho;
            }

            // 4. Arreglo final, en orden. Las ocultas se mandan fuera de pantalla con
            //    tamaño cero: no se ven ni reciben clics, pero siguen en el TabControl.
            double x = 0;
            for (int i = 0; i < total; i++)
            {
                var hijo = InternalChildren[i];
                if (!visible[i]) { hijo.Arrange(new Rect(-10000, 0, 0, 0)); continue; }
                double ancho = hijo.DesiredSize.Width;
                hijo.Arrange(new Rect(x, 0, ancho, finalSize.Height));
                x += ancho;
            }

            return finalSize;
        }

        private int IndiceSeleccionado()
        {
            for (int i = 0; i < InternalChildren.Count; i++)
                if (InternalChildren[i] is TabItem t && t.IsSelected) return i;
            return -1;
        }
    }
}
