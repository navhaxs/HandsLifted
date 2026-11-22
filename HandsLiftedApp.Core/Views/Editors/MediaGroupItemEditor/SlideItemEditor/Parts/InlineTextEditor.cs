using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.Threading;
using HandsLiftedApp.Data.Models.SlideElement;

namespace HandsLiftedApp.Core.Views.Editors
{
    /// <summary>
    /// The box a text element is typed into on the slide designer canvas. It sits exactly over the element (same
    /// rect, font, size, weight, colours, alignment, line height, no padding) so the text does not visibly move when
    /// editing starts or ends, while the canvas skips drawing that element's own text.
    ///
    /// Typing never touches the model: <see cref="Committed"/> hands the final text to whoever opened the box, which
    /// writes it once (one undo step). Ctrl+Z inside the box is the box's own local undo.
    /// </summary>
    public sealed class InlineTextEditor : TextBox
    {
        public const string StyleClass = "inlineTextEditor";

        // Use TextBox's own theme/template (a subclass would otherwise look for an InlineTextEditor theme).
        protected override Type StyleKeyOverride => typeof(TextBox);

        public delegate void CommittedHandler(TextElement element, string newText);

        /// <summary>Raised once per edit, when it ends (Esc, focus leaving, or <see cref="Commit"/>).</summary>
        public event CommittedHandler? Committed;

        /// <summary>The element being edited, or null.</summary>
        public TextElement? Element { get; private set; }

        public bool IsEditing => Element != null;

        public InlineTextEditor()
        {
            Classes.Add(StyleClass);
            IsVisible = false;
            AcceptsReturn = true;
            TextWrapping = TextWrapping.Wrap;
            MinWidth = 0;
            MinHeight = 0;
            CornerRadius = default;
            FocusAdorner = null;
            ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(this, ScrollBarVisibility.Hidden);

            // Themes repaint the template's border from their own resources on :pointerover / :focus (Fluent paints
            // PART_BorderElement's background, border brush and a thicker focus border; Simple paints #border's
            // border brush). Pin the template border to this box's own Background with no border in every state,
            // so the box always looks like the element's fill and nothing else. Styles on the control itself win
            // over its control theme's styles.
            foreach (var borderName in new[] { "border", "PART_BorderElement" })
            foreach (var state in new[] { null, ":pointerover", ":focus", ":focus-within" })
                Styles.Add(TemplateBorderStyle(borderName, state));
        }

        private static Style TemplateBorderStyle(string borderName, string? pseudoClass)
        {
            var style = new Style(x =>
            {
                var self = x.OfType<TextBox>().Class(StyleClass);
                if (pseudoClass != null) self = self.Class(pseudoClass);
                return self.Template().OfType<Border>().Name(borderName);
            });
            style.Setters.Add(new Setter(Border.BackgroundProperty,
                new Binding(nameof(Background)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) }));
            style.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(0)));
            return style;
        }

        /// <summary>
        /// Gives <paramref name="box"/> the element's look and box, matching <c>CustomSlideTextLayout</c>: wrap width
        /// = element width, zero padding, line height = (int)(font size x em), vertical Stretch drawn as Top.
        /// </summary>
        public static void ApplyStyle(TextBox box, TextElement el)
        {
            box.FontFamily = string.IsNullOrWhiteSpace(el.FontFamilyAsText)
                ? FontFamily.Default
                : new FontFamily(el.FontFamilyAsText);
            box.FontSize = el.FontSize;
            box.FontWeight = el.FontWeight;
            box.FontStyle = el.Italic ? FontStyle.Italic : FontStyle.Normal;
            // Underline is not shown while typing: Avalonia's TextBox/TextPresenter have no TextDecorations.

            var foreground = new SolidColorBrush(el.ForegroundAvaloniaColour);
            box.Foreground = foreground;
            box.CaretBrush = foreground;
            // The canvas still draws the element's fill underneath (only its text is hidden while editing). An opaque
            // fill is repeated here so the box is solid even before the canvas redraws; a see-through one is not, or
            // it would be blended twice and visibly darken while typing.
            var fill = el.BackgroundAvaloniaColour;
            box.Background = fill.A == 255 ? new SolidColorBrush(fill) : Brushes.Transparent;

            box.TextAlignment = el.TextAlignment;
            box.VerticalContentAlignment = el.VerticalAlignment switch
            {
                VerticalAlignment.Center => VerticalAlignment.Center,
                VerticalAlignment.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Top, // Top, and Stretch (the Skia layout draws Stretch from the top)
            };
            box.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var lineHeight = LineHeightOf(el);
            box.LineHeight = lineHeight;

            // No padding, except one correction: when the line height is taller than the font's own, Avalonia puts
            // half the extra space above each line's text, while the Skia layout puts the baseline one ascent below
            // the line top. Shift the text area up by that much (same height, so all three vertical alignments
            // stay put) so the text does not drop when editing starts.
            var shift = HalfLeadingShift(box, el.FontSize, lineHeight);
            box.Padding = new Thickness(0, -shift, 0, shift);
            box.BorderThickness = new Thickness(0);
            box.TextWrapping = TextWrapping.Wrap;
            box.AcceptsReturn = true;
            box.Width = Math.Max(0, el.Width);
            box.Height = Math.Max(0, el.Height);
        }

        /// <summary>
        /// How far below the line top Avalonia puts the first baseline beyond the font's ascent (where the Skia layout
        /// puts it) for this line height: 0 when the line height is not taller than the font's natural one.
        /// </summary>
        internal static double HalfLeadingShift(TextBox box, double fontSize, double lineHeight)
        {
            if (fontSize <= 0) return 0;
            try
            {
                var typeface = new Typeface(box.FontFamily, box.FontStyle, box.FontWeight);
                var metrics = typeface.GlyphTypeface.Metrics;
                if (metrics.DesignEmHeight <= 0) return 0;
                // the Skia layout's baseline offset from the line top: the font's ascent
                var ascent = -metrics.Ascent * fontSize / metrics.DesignEmHeight;
                using var spaced = new TextLayout("X", typeface, fontSize, null, lineHeight: lineHeight);
                if (spaced.TextLines.Count == 0) return 0;
                return Math.Max(0, spaced.TextLines[0].Baseline - ascent);
            }
            catch (Exception)
            {
                return 0; // no font system (e.g. design time): no correction
            }
        }

        // Same value as TextElement.LineHeight (which updates asynchronously), and the same fallback as the layout.
        private static double LineHeightOf(TextElement el)
        {
            var lineHeight = (int)(el.FontSize * el.LineHeightEm);
            return lineHeight > 0 ? lineHeight : el.FontSize * 1.2;
        }

        /// <summary>
        /// Starts editing <paramref name="el"/> (committing any edit already open), restyling whenever the element
        /// changes. With <paramref name="selectAll"/> the existing text is selected so typing replaces it.
        /// </summary>
        public void Begin(TextElement el, bool selectAll)
        {
            if (IsEditing) Commit();

            Element = el;
            ApplyStyle(this, el);
            Text = el.Text ?? "";
            ClearUndoHistory();
            el.PropertyChanged += OnElementPropertyChanged;
            IsVisible = true;

            if (!Focus())
            {
                // e.g. just made visible inside a layout pass that has not run yet: try again once it has
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(Element, el) && !IsFocused) Focus();
                }, DispatcherPriority.Loaded);
            }

            if (selectAll) SelectAll();
            else
            {
                ClearSelection();
                CaretIndex = Text?.Length ?? 0;
            }
        }

        /// <summary>Ends the edit (if any) and raises <see cref="Committed"/> with the box's text.</summary>
        public void Commit()
        {
            var el = Element;
            if (el == null) return;

            Stop(el);
            Committed?.Invoke(el, Text ?? "");
        }

        /// <summary>Ends the edit (if any) without raising <see cref="Committed"/>.</summary>
        public void Cancel()
        {
            if (Element is { } el) Stop(el);
        }

        private void Stop(TextElement el)
        {
            el.PropertyChanged -= OnElementPropertyChanged;
            Element = null;
            IsVisible = false;
        }

        private void ClearUndoHistory()
        {
            // a new edit must not be able to Ctrl+Z back into the previous element's text
            if (IsUndoEnabled)
            {
                IsUndoEnabled = false;
                IsUndoEnabled = true;
            }
        }

        private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Only the element's own settings. The text is the box's while editing, and the derived properties
            // (LineHeight, Calculated*) update asynchronously and carry nothing ApplyStyle does not already compute.
            if (e.PropertyName is not (nameof(SlideElement.X) or nameof(SlideElement.Y) or nameof(SlideElement.Width)
                or nameof(SlideElement.Height) or nameof(TextElement.FontFamilyAsText)
                or nameof(TextElement.FontFamilyAsAvalonia) or nameof(TextElement.FontSize)
                or nameof(TextElement.FontWeight) or nameof(TextElement.IsBold) or nameof(TextElement.Italic)
                or nameof(TextElement.Underline) or nameof(TextElement.TextAlignment)
                or nameof(TextElement.VerticalAlignment) or nameof(TextElement.ForegroundAvaloniaColour)
                or nameof(TextElement.BackgroundAvaloniaColour) or nameof(TextElement.LineHeightEm)))
                return;

            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnElementPropertyChanged(sender, e));
                return;
            }

            if (sender is not TextElement el || !ReferenceEquals(el, Element)) return;
            ApplyStyle(this, el);
            ElementChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Raised after the edited element changed and the box was restyled (e.g. so its host can move it).</summary>
        public event EventHandler? ElementChanged;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (IsEditing && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
            {
                Commit();
                e.Handled = true; // Esc ends typing here; it must not also reach the canvas (deselect)
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void OnLostFocus(FocusChangedEventArgs e)
        {
            base.OnLostFocus(e);
            Commit();
        }

        // The proxies and the canvas sit underneath: pointer input inside the box (caret placement, drag-selecting
        // text, double-click word selection) must not reach them, or it would change the selection.
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            e.Handled = true;
        }
    }
}
