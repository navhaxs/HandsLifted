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

        // Aspect mode (opt-in): when > 0, ItemHeight is ignored and each item's height is derived from its
        // stretched width: (slotWidth - ExtraWidth) / AspectRatio + ExtraHeight. 0 = legacy fixed ItemHeight.
        public static readonly StyledProperty<double> AspectRatioProperty =
            AvaloniaProperty.Register<JustifiedWrapPanel, double>(nameof(AspectRatio), 0);

        public static readonly StyledProperty<double> ExtraWidthProperty =
            AvaloniaProperty.Register<JustifiedWrapPanel, double>(nameof(ExtraWidth), 0);

        public static readonly StyledProperty<double> ExtraHeightProperty =
            AvaloniaProperty.Register<JustifiedWrapPanel, double>(nameof(ExtraHeight), 0);

        static JustifiedWrapPanel()
        {
            AffectsMeasure<JustifiedWrapPanel>(ItemWidthProperty, ItemHeightProperty, SpacingProperty,
                AspectRatioProperty, ExtraWidthProperty, ExtraHeightProperty);
            AffectsArrange<JustifiedWrapPanel>(ItemWidthProperty, ItemHeightProperty, SpacingProperty,
                AspectRatioProperty, ExtraWidthProperty, ExtraHeightProperty);
        }

        public double AspectRatio
        {
            get => GetValue(AspectRatioProperty);
            set => SetValue(AspectRatioProperty, value);
        }

        public double ExtraWidth
        {
            get => GetValue(ExtraWidthProperty);
            set => SetValue(ExtraWidthProperty, value);
        }

        public double ExtraHeight
        {
            get => GetValue(ExtraHeightProperty);
            set => SetValue(ExtraHeightProperty, value);
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

        private bool AspectMode => AspectRatio > 0;

        private double SlotWidthFor(double availableWidth)
        {
            int columns = ColumnsFor(availableWidth);
            if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth))
                return ItemWidth;
            if (columns > 1 || AspectMode)
                return Math.Max(ItemWidth, (availableWidth - (columns - 1) * Spacing) / columns);
            return ItemWidth;
        }

        private double SlotHeightFor(double slotWidth) =>
            AspectMode ? Math.Max(0, (slotWidth - ExtraWidth) / AspectRatio + ExtraHeight) : ItemHeight;

        protected override Size MeasureOverride(Size availableSize)
        {
            double slotWidth = SlotWidthFor(availableSize.Width);
            double slotHeight = SlotHeightFor(slotWidth);
            var childConstraint = AspectMode ? new Size(slotWidth, slotHeight) : new Size(ItemWidth, ItemHeight);
            foreach (var child in Children)
                child.Measure(childConstraint);

            if (Children.Count == 0)
                return new Size(0, 0);

            int columns = ColumnsFor(availableSize.Width);
            int rows = (int)Math.Ceiling(Children.Count / (double)columns);
            double height = rows * slotHeight + Math.Max(0, rows - 1) * Spacing;

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
            double itemWidth = AspectMode ? SlotWidthFor(finalSize.Width)
                : columns > 1
                    ? (finalSize.Width - (columns - 1) * Spacing) / columns
                    : ItemWidth;
            double itemHeight = SlotHeightFor(itemWidth);

            double x = 0, y = 0;
            int col = 0;

            foreach (var child in Children)
            {
                child.Arrange(new Rect(x, y, itemWidth, itemHeight));
                col++;
                if (col >= columns)
                {
                    col = 0;
                    x = 0;
                    y += itemHeight + Spacing;
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
