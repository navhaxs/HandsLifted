using System;
using System.Collections.Generic;
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

        public CustomSlideToolbar()
        {
            InitializeComponent();
            SyncSections();
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

            ImageSection.DataContext = image;
            ImageSection.IsVisible = image != null;

            SelectionActions.IsVisible = element != null;

            SlideSection.DataContext = Slide;
            SlideSection.IsVisible = element == null;
        }

        private void FontComboBox_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (sender is not ComboBox comboBox) return;
            _systemFontFamilies ??= FontManager.Current.SystemFonts
                .Select(x => x.Name)
                .OrderBy(x => x)
                .ToList();

            // setting ItemsSource can clear the selection; put the element's font back afterwards
            var current = (TextSection.DataContext as TextElement)?.FontFamilyAsText;
            comboBox.ItemsSource = _systemFontFamilies;
            if (current != null && !Equals(comboBox.SelectedItem, current))
                comboBox.SelectedItem = current;
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
