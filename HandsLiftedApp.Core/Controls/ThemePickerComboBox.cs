using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Controls
{
    /// <summary>
    /// A theme dropdown whose list is grouped by theme type (see <see cref="ThemePickerItems"/>): set
    /// <see cref="Themes"/> (not ItemsSource), <see cref="Target"/> and optionally <see cref="IncludeBlankOption"/>.
    /// <see cref="SelectingItemsControl.SelectedItem"/> is still the theme itself (or null).
    /// <para>
    /// Group header rows are disabled and not focusable, so the pointer, Up/Down and the open list's focus
    /// navigation skip them; themes the target does not use stay in the list (so a selection pointing at one
    /// survives) but are hidden and disabled the same way. Type-ahead matches shown theme names (the stock text
    /// search would match a header's text, or a theme's ToString, which is JSON).
    /// </para>
    /// Styled exactly like a ComboBox (<c>ComboBox.themePicker</c> in ThemePickerStyles.axaml); the header rows get
    /// the <see cref="HeaderClass"/> class.
    /// </summary>
    public class ThemePickerComboBox : ComboBox
    {
        public const string HeaderClass = "themeGroupHeader";

        public static readonly StyledProperty<IEnumerable<BaseSlideTheme>?> ThemesProperty =
            AvaloniaProperty.Register<ThemePickerComboBox, IEnumerable<BaseSlideTheme>?>(nameof(Themes));

        public static readonly StyledProperty<ThemePickerTarget> TargetProperty =
            AvaloniaProperty.Register<ThemePickerComboBox, ThemePickerTarget>(nameof(Target));

        public static readonly StyledProperty<bool> IncludeBlankOptionProperty =
            AvaloniaProperty.Register<ThemePickerComboBox, bool>(nameof(IncludeBlankOption));

        private static readonly IDataTemplate HeaderTemplate =
            new FuncDataTemplate<ThemePickerGroupHeader>((header, _) => new TextBlock { Text = header?.Title });

        private static readonly TimeSpan TypeAheadTimeout = TimeSpan.FromSeconds(1);

        private readonly ThemePickerItems _items = new();
        private string _typeAhead = "";
        private DateTime _typeAheadAt;

        public ThemePickerComboBox()
        {
            _items.Resynced += (_, _) => RefreshItemStates();
            IsTextSearchEnabled = false;
        }

        protected override Type StyleKeyOverride => typeof(ComboBox);

        /// <summary>The themes to choose from (usually the playlist's Designs).</summary>
        public IEnumerable<BaseSlideTheme>? Themes
        {
            get => GetValue(ThemesProperty);
            set => SetValue(ThemesProperty, value);
        }

        public ThemePickerTarget Target
        {
            get => GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        /// <summary>Offer a blank (null) "(None)" option first, outside any group.</summary>
        public bool IncludeBlankOption
        {
            get => GetValue(IncludeBlankOptionProperty);
            set => SetValue(IncludeBlankOptionProperty, value);
        }

        /// <summary>The grouped list this dropdown shows (its ItemsSource while <see cref="Themes"/> is set).</summary>
        public ThemePickerItems PickerItems => _items;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedItemProperty)
            {
                _items.KeepInPlace = SelectedItem;
            }
            else if (change.Property == ThemesProperty)
            {
                // No themes: no list at all, as when ItemsSource was bound to a null Designs.
                if (Themes == null)
                {
                    ItemsSource = null;
                    _items.Source = null;
                }
                else
                {
                    _items.Source = Themes;
                    ItemsSource = _items;
                }
            }
            else if (change.Property == TargetProperty)
            {
                _items.Target = Target;
            }
            else if (change.Property == IncludeBlankOptionProperty)
            {
                _items.IncludeBlankOption = IncludeBlankOption;
            }
        }

        protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
        {
            base.PrepareContainerForItemOverride(container, item, index);
            ApplyItemState(container, item);
        }

        protected override void ClearContainerForItemOverride(Control container)
        {
            base.ClearContainerForItemOverride(container);
            container.Classes.Remove(HeaderClass);
            container.ClearValue(ContentControl.ContentTemplateProperty);
            container.ClearValue(IsEnabledProperty);
            container.ClearValue(FocusableProperty);
            container.ClearValue(IsVisibleProperty);
        }

        private void ApplyItemState(Control container, object? item)
        {
            if (item is ThemePickerGroupHeader)
            {
                container.Classes.Add(HeaderClass);
                container.IsEnabled = false;
                container.Focusable = false;
                container.ClearValue(IsVisibleProperty);
                if (container is ContentControl contentControl) contentControl.ContentTemplate = HeaderTemplate;
                return;
            }

            container.Classes.Remove(HeaderClass);
            container.ClearValue(FocusableProperty);
            if (container is ContentControl { ContentTemplate: var template } tile && ReferenceEquals(template, HeaderTemplate))
                tile.ClearValue(ContentControl.ContentTemplateProperty); // a container that showed a header before
            if (_items.IsShown(item))
            {
                container.ClearValue(IsEnabledProperty);
                container.ClearValue(IsVisibleProperty);
            }
            else
            {
                container.IsEnabled = false;
                container.IsVisible = false;
            }
        }

        /// <summary>A theme's Type changed: whether it is shown may have changed with it.</summary>
        private void RefreshItemStates()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (ContainerFromIndex(i) is { } container) ApplyItemState(container, _items[i]);
            }
        }

        /// <summary>
        /// Arrows and Home/End only ever reach a selectable theme (or the blank option), never a header or a hidden theme.
        /// <list type="bullet">
        /// <item>Open list: they move the focus over the tiles. Up/Left = the previous tile and Down/Right = the next
        /// tile in LIST order (not the tile above/below in the two-column grid). The stock handling asks the WrapPanel
        /// for its first/last/neighbouring child, which can be a header that takes no focus, so Home did nothing.</item>
        /// <item>Closed box: Up/Down select the previous/next choice, Home/End the first/last, stopping at the ends.
        /// The stock version only skips items whose container is disabled, and a dropdown that was never opened has no
        /// containers, so it selected headers and hidden themes.</item>
        /// </list>
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.None &&
                (IsDropDownOpen ? MoveFocusInList(e.Key) : MoveSelection(e.Key)))
            {
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        /// <summary>The wheel over the closed, focused box steps the selection like Up/Down (as the stock ComboBox does, minus headers and hidden themes).</summary>
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            if (!e.Handled && !IsDropDownOpen && IsFocused && e.Delta.Y != 0)
            {
                MoveSelection(e.Delta.Y < 0 ? Key.Down : Key.Up);
                e.Handled = true;
                return;
            }

            base.OnPointerWheelChanged(e);
        }

        /// <summary>The items a user can choose, in list order: shown themes, plus the blank option where offered.</summary>
        private List<object?> Choices() =>
            _items.Where(item => item is not ThemePickerGroupHeader && _items.IsShown(item)).ToList();

        private bool MoveSelection(Key key)
        {
            if (key is not (Key.Home or Key.End or Key.Up or Key.Down)) return false;

            var choices = Choices();
            if (choices.Count == 0) return true; // nothing to choose: swallow the key rather than let the stock code pick a header

            var current = choices.FindIndex(c => ReferenceEquals(c, SelectedItem));
            var target = key switch
            {
                Key.Home => 0,
                Key.End => choices.Count - 1,
                Key.Down => current < 0 ? 0 : Math.Min(current + 1, choices.Count - 1),
                _ => current < 0 ? choices.Count - 1 : Math.Max(current - 1, 0),
            };
            if (!ReferenceEquals(choices[target], SelectedItem)) SelectedItem = choices[target];
            return true;
        }

        private bool MoveFocusInList(Key key)
        {
            if (key is not (Key.Home or Key.End or Key.Up or Key.Down or Key.Left or Key.Right)) return false;

            var tiles = Enumerable.Range(0, ItemCount)
                .Select(ContainerFromIndex)
                .OfType<Control>()
                .Where(c => c.IsVisible && c.IsEffectivelyEnabled && c.Focusable)
                .ToList();
            if (tiles.Count == 0) return false;

            var current = tiles.FindIndex(c => c.IsKeyboardFocusWithin);
            var target = key switch
            {
                Key.Home => 0,
                Key.End => tiles.Count - 1,
                Key.Down or Key.Right => current < 0 ? 0 : Math.Min(current + 1, tiles.Count - 1),
                _ => current < 0 ? tiles.Count - 1 : Math.Max(current - 1, 0),
            };
            tiles[target].Focus(NavigationMethod.Directional);
            return true;
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            if (e.Handled || string.IsNullOrEmpty(e.Text)) return;

            var now = DateTime.UtcNow;
            var running = _typeAhead.Length > 0 && now - _typeAheadAt <= TypeAheadTimeout;
            if (!running && string.IsNullOrWhiteSpace(e.Text)) return; // Space etc. cannot start (and so poison) a run
            _typeAhead = running ? _typeAhead + e.Text : e.Text;
            _typeAheadAt = now;

            // Keep typing to refine; a fresh start if the run so far matches nothing.
            var match = FindShownTheme(_typeAhead);
            if (match == null && _typeAhead != e.Text && !string.IsNullOrWhiteSpace(e.Text))
            {
                _typeAhead = e.Text;
                match = FindShownTheme(_typeAhead);
            }

            if (match == null) return;
            if (IsDropDownOpen) ContainerFromItem(match)?.Focus(NavigationMethod.Directional);
            else SelectedItem = match;
            e.Handled = true;
        }

        private BaseSlideTheme? FindShownTheme(string prefix) =>
            _items.OfType<BaseSlideTheme>().FirstOrDefault(t =>
                _items.IsShown(t) && (t.Name ?? "").StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
    }
}
