using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;

namespace HandsLiftedApp.Core.Views.Editors
{
    /// <summary>
    /// Contextual toolbar for the custom slide editor. Shows the sections that fit the current selection (nothing,
    /// a text element, an image element), binds formatting controls straight to the selected element, and raises
    /// *Requested events for everything else; the host runs the commands (<see cref="SlideElementCommands"/>) and
    /// reports its undo state back through <see cref="SetUndoState"/>.
    /// </summary>
    public partial class CustomSlideToolbar : UserControl
    {
        public static readonly StyledProperty<SlideElement?> SelectedElementProperty =
            AvaloniaProperty.Register<CustomSlideToolbar, SlideElement?>(nameof(SelectedElement));

        public static readonly StyledProperty<CustomSlide?> SlideProperty =
            AvaloniaProperty.Register<CustomSlideToolbar, CustomSlide?>(nameof(Slide));

        /// <summary>The element being edited, or null when nothing is selected (the slide section shows).</summary>
        public SlideElement? SelectedElement
        {
            get => GetValue(SelectedElementProperty);
            set => SetValue(SelectedElementProperty, value);
        }

        /// <summary>The slide being edited (for the background colour / graphic section).</summary>
        public CustomSlide? Slide
        {
            get => GetValue(SlideProperty);
            set => SetValue(SlideProperty, value);
        }

        public event EventHandler? AddTextRequested;
        public event EventHandler? AddImageRequested;
        public event EventHandler? ReplaceImageRequested;
        public event EventHandler? FitImageRequested;
        public event EventHandler? DuplicateRequested;
        public event EventHandler? DeleteRequested;
        public event EventHandler? BringForwardRequested;
        public event EventHandler? SendBackwardRequested;
        public event EventHandler? UndoRequested;
        public event EventHandler? RedoRequested;
        public event EventHandler? ChangeBackgroundGraphicRequested;

        private static List<string>? _systemFontFamilies;

        // The font box's items: the system fonts (filled on Loaded) plus, at the top, the selected element's own font
        // when it isn't installed (_extraFont), so the box can always show it.
        private readonly ObservableCollection<string> _fontItems = new();
        private bool _systemFontsLoaded;
        private string? _extraFont;
        private bool _syncingFont;
        private TextElement? _fontElement;

        public CustomSlideToolbar()
        {
            InitializeComponent();
            FontComboBox.ItemsSource = _fontItems;
            SyncSections();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SyncSections();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            WatchFontOf(null); // don't let a still-alive element keep this toolbar alive
        }

        /// <summary>Enables the Undo/Redo buttons and sets their tooltips (e.g. "Undo Move").</summary>
        public void SetUndoState(bool canUndo, bool canRedo, string? undoText, string? redoText)
        {
            UndoButton.IsEnabled = canUndo;
            RedoButton.IsEnabled = canRedo;
            ToolTip.SetTip(UndoButton, string.IsNullOrWhiteSpace(undoText) ? "Undo" : undoText);
            ToolTip.SetTip(RedoButton, string.IsNullOrWhiteSpace(redoText) ? "Redo" : redoText);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SelectedElementProperty || change.Property == SlideProperty)
                SyncSections();
        }

        // Section DataContexts are set here rather than through element-name path bindings, which go stale when the
        // selection changes (CLAUDE.md: "Element-name path bindings don't track intermediate object changes").
        private void SyncSections()
        {
            var element = SelectedElement;
            var text = element as TextElement;
            var image = element as ImageElement;

            // null first so the old element's bindings let go before the new one is attached
            TextSection.DataContext = null;
            TextSection.DataContext = text;
            TextSection.IsVisible = text != null;
            WatchFontOf(text);

            ImageSection.DataContext = image;
            ImageSection.IsVisible = image != null;

            SelectionActions.IsVisible = element != null;

            SlideSection.DataContext = Slide;
            SlideSection.IsVisible = element == null;
        }

        // ---------------- font box ----------------
        // The box is one long-lived control reused for every selected element, so it is kept in step from here rather
        // than by a two-way SelectedItem binding: selecting an element only ever writes to the BOX, and only a user
        // pick (a non-null item that differs from the element's font) writes to the ELEMENT. A missing font is shown
        // as an extra item instead of being dropped, so it can never be replaced by null/default.

        private void WatchFontOf(TextElement? element)
        {
            if (!ReferenceEquals(_fontElement, element))
            {
                if (_fontElement != null) _fontElement.PropertyChanged -= FontElement_OnPropertyChanged;
                _fontElement = element;
                if (_fontElement != null) _fontElement.PropertyChanged += FontElement_OnPropertyChanged;
            }
            SyncFontBox();
        }

        // the element's font can change elsewhere (undo, the code tab): show it
        private void FontElement_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName)
                || e.PropertyName == nameof(TextElement.FontFamilyAsText)
                || e.PropertyName == nameof(TextElement.FontFamilyAsAvalonia))
                SyncFontBox();
        }

        private void SyncFontBox()
        {
            var font = _fontElement?.FontFamilyAsText;
            _syncingFont = true;
            try
            {
                if (_extraFont != null && _extraFont != font)
                {
                    _fontItems.Remove(_extraFont);
                    _extraFont = null;
                }
                if (!string.IsNullOrEmpty(font) && !_fontItems.Contains(font))
                {
                    _fontItems.Insert(0, font);
                    _extraFont = font;
                }
                FontComboBox.SelectedItem = string.IsNullOrEmpty(font) ? null : font;
            }
            finally
            {
                _syncingFont = false;
            }
        }

        private void FontComboBox_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (!_systemFontsLoaded)
            {
                _systemFontsLoaded = true;
                _systemFontFamilies ??= FontManager.Current.SystemFonts
                    .Select(x => x.Name)
                    .OrderBy(x => x)
                    .ToList();

                _syncingFont = true;
                try
                {
                    // the shown font turned out to be installed: drop the extra copy first so the loop below
                    // lists it in its sorted place (it is then no longer an extra)
                    if (_extraFont != null && _systemFontFamilies.Contains(_extraFont))
                    {
                        _fontItems.Remove(_extraFont);
                        _extraFont = null;
                    }
                    var present = new HashSet<string>(_fontItems);
                    foreach (var name in _systemFontFamilies)
                        if (present.Add(name)) _fontItems.Add(name);
                }
                finally
                {
                    _syncingFont = false;
                }
            }
            SyncFontBox();
        }

        private void FontComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_syncingFont || _fontElement == null) return;
            if (FontComboBox.SelectedItem is not string picked || string.IsNullOrEmpty(picked)) return;
            if (picked == _fontElement.FontFamilyAsText) return;
            _fontElement.FontFamilyAsText = picked;
        }

        private void UndoButton_OnClick(object? sender, RoutedEventArgs e) => UndoRequested?.Invoke(this, EventArgs.Empty);
        private void RedoButton_OnClick(object? sender, RoutedEventArgs e) => RedoRequested?.Invoke(this, EventArgs.Empty);
        private void AddTextButton_OnClick(object? sender, RoutedEventArgs e) => AddTextRequested?.Invoke(this, EventArgs.Empty);
        private void AddImageButton_OnClick(object? sender, RoutedEventArgs e) => AddImageRequested?.Invoke(this, EventArgs.Empty);
        private void DuplicateButton_OnClick(object? sender, RoutedEventArgs e) => DuplicateRequested?.Invoke(this, EventArgs.Empty);
        private void DeleteButton_OnClick(object? sender, RoutedEventArgs e) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        private void BringForwardButton_OnClick(object? sender, RoutedEventArgs e) => BringForwardRequested?.Invoke(this, EventArgs.Empty);
        private void SendBackwardButton_OnClick(object? sender, RoutedEventArgs e) => SendBackwardRequested?.Invoke(this, EventArgs.Empty);
        private void ReplaceImageButton_OnClick(object? sender, RoutedEventArgs e) => ReplaceImageRequested?.Invoke(this, EventArgs.Empty);
        private void FitImageButton_OnClick(object? sender, RoutedEventArgs e) => FitImageRequested?.Invoke(this, EventArgs.Empty);
        private void BackgroundGraphicButton_OnClick(object? sender, RoutedEventArgs e) => ChangeBackgroundGraphicRequested?.Invoke(this, EventArgs.Empty);
    }
}
