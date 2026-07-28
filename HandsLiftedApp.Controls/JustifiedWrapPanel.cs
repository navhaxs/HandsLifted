using System;
using Avalonia;
using Avalonia.Controls;

namespace HandsLiftedApp.Controls
{
    // Arranges fixed-size children into rows, stretching each item's width (not the gap) so
    // every full row fills the available width edge-to-edge (like Windows Explorer's icon view).
    // Gap between items stays fixed at Spacing; the leftmost item in every row stays pinned at x=0.
    // Unlike WrapPanel, rows use the full container width instead of leaving dead space on the right.
    public class JustifiedWrapPanel : Panel
    {
        public static readonly StyledProperty<double> ItemWidthProperty =
            AvaloniaProperty.Register<JustifiedWrapPanel, double>(nameof(ItemWidth), 290);

        public static readonly StyledProperty<double> ItemHeightProperty =
            AvaloniaProperty.Register<JustifiedWrapPanel, double>(nameof(ItemHeight), 200);

        // Fixed horizontal gap between columns (and vertical gap between rows); also used to decide how many columns fit.
        public static readonly StyledProperty<double> SpacingProperty =
            AvaloniaProperty.Register<JustifiedWrapPanel, double>(nameof(Spacing), 8);

        static JustifiedWrapPanel()
        {
            AffectsMeasure<JustifiedWrapPanel>(ItemWidthProperty, ItemHeightProperty, SpacingProperty);
            AffectsArrange<JustifiedWrapPanel>(ItemWidthProperty, ItemHeightProperty, SpacingProperty);
        }

        public double ItemWidth
        {
            get => GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public double ItemHeight
        {
            get => GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public double Spacing
        {
            get => GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        private int ColumnsFor(double width)
        {
            if (ItemWidth <= 0 || double.IsNaN(width) || double.IsInfinity(width))
                return 1;

            return Math.Max(1, (int)((width + Spacing) / (ItemWidth + Spacing)));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var childConstraint = new Size(ItemWidth, ItemHeight);
            foreach (var child in Children)
                child.Measure(childConstraint);

            if (Children.Count == 0)
                return new Size(0, 0);

            int columns = ColumnsFor(availableSize.Width);
            int rows = (int)Math.Ceiling(Children.Count / (double)columns);
            double height = rows * ItemHeight + Math.Max(0, rows - 1) * Spacing;

            double width = double.IsInfinity(availableSize.Width)
                ? columns * ItemWidth + Math.Max(0, columns - 1) * Spacing
                : availableSize.Width;

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Children.Count == 0)
                return finalSize;

            int columns = ColumnsFor(finalSize.Width);

            // Only stretch item width across multiple columns. ColumnsFor guarantees the nominal
            // (columns * ItemWidth + gaps) fits within finalSize.Width, so itemWidth only ever grows.
            double itemWidth = columns > 1
                ? (finalSize.Width - (columns - 1) * Spacing) / columns
                : ItemWidth;

            double x = 0, y = 0;
            int col = 0;

            foreach (var child in Children)
            {
                child.Arrange(new Rect(x, y, itemWidth, ItemHeight));
                col++;
                if (col >= columns)
                {
                    col = 0;
                    x = 0;
                    y += ItemHeight + Spacing;
                }
                else
                {
                    x += itemWidth + Spacing;
                }
            }

            return finalSize;
        }
    }
}
