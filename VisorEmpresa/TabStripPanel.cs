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
    /// ANTIGUAS para que se vean las más recientes. Las nuevas entran como PRIMERAS
    /// de la barra (ver AbrirPestaña), así que la fila va de la más nueva a la más
    /// vieja, con la fija cerrando a la derecha, y las que se caen son las viejas.
    ///
    /// Quedan siempre visibles:
    ///   • la pestaña marcada con <see cref="FijaProperty"/> (la de la sección), y
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

            // 1. La pestaña fija nunca se oculta.
            int fija = IndiceFija();
            visible[fija] = true;
            usado        += InternalChildren[fija].DesiredSize.Width;

            // 2. La seleccionada tampoco, esté donde esté.
            int sel = IndiceSeleccionado();
            if (sel >= 0 && !visible[sel])
            {
                visible[sel] = true;
                usado       += InternalChildren[sel].DesiredSize.Width;
            }

            // 3. El resto se llena de izquierda a derecha. Las pestañas nuevas entran
            //    como PRIMERAS de la barra (ver AbrirPestaña), así que ese recorrido
            //    va de la MÁS RECIENTE a la MÁS ANTIGUA: en cuanto una no entra se
            //    corta, y las que quedan afuera son siempre las viejas.
            for (int i = 0; i < total; i++)
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

        /// <summary>
        /// Posición de la pestaña marcada con <c>TabStripPanel.Fija</c>. Si ninguna
        /// lo está, se toma la última: es donde vive la fija, porque las nuevas se
        /// insertan siempre antes de ella.
        /// </summary>
        private int IndiceFija()
        {
            for (int i = InternalChildren.Count - 1; i >= 0; i--)
                if (GetFija(InternalChildren[i])) return i;
            return InternalChildren.Count - 1;
        }

        // ─── Fija (attached) ──────────────────────────────────────────────────
        // Marca la pestaña que nunca se oculta, aunque no entre por ancho. La pone
        // la consola sobre su TabItem fijo (el del panel de la sección).

        public static readonly DependencyProperty FijaProperty =
            DependencyProperty.RegisterAttached(
                "Fija", typeof(bool), typeof(TabStripPanel),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public static void SetFija(DependencyObject elemento, bool valor)
            => elemento.SetValue(FijaProperty, valor);

        public static bool GetFija(DependencyObject elemento)
            => (bool)elemento.GetValue(FijaProperty);
    }
}
