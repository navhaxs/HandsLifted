using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.Undo;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using HandsLiftedApp.Data.Data.Models.Types;
using Serilog;
using SkiaSharp;

namespace HandsLiftedApp.Core.Views.Designer
{
    public partial class SlideThemeDesigner : UserControl
    {

        public class FontWeightOption
        {
            public FontWeight FontWeight;
            public String Label;
        }

        private readonly Dictionary<string, HashSet<int>> _fontWeightCache = new();

        private bool _suppressSelectionSync;

        private BaseSlideTheme? SelectedDesign =>
            generalDesignsListBox.SelectedItem as BaseSlideTheme
            ?? songDesignsListBox.SelectedItem as BaseSlideTheme
            ?? scriptureDesignsListBox.SelectedItem as BaseSlideTheme;

        private ListBox ListBoxFor(SlideThemeType type) => type switch
        {
            SlideThemeType.SongTheme => songDesignsListBox,
            SlideThemeType.ScriptureTheme => scriptureDesignsListBox,
            _ => generalDesignsListBox,
        };

        private void SelectDesign(BaseSlideTheme design)
        {
            _suppressSelectionSync = true;
            try
            {
                generalDesignsListBox.SelectedItem = null;
                songDesignsListBox.SelectedItem = null;
                scriptureDesignsListBox.SelectedItem = null;
                ListBoxFor(design.Type).SelectedItem = design;
            }
            finally
            {
                _suppressSelectionSync = false;
            }
            SyncPreviewModeToDesign(design);
            SyncEditorToSelection();
        }

        // General designs preview fine in every mode, so they leave the current mode alone. A song
        // design keeps Song Title if already on it, otherwise falls back to Song Lyric.
        private void SyncPreviewModeToDesign(BaseSlideTheme design)
        {
            switch (design.Type)
            {
                case SlideThemeType.ScriptureTheme:
                    previewScriptureToggle.IsChecked = true;
                    break;
                case SlideThemeType.SongTheme when previewScriptureToggle.IsChecked == true:
                    previewLyricToggle.IsChecked = true;
                    break;
            }
        }

        private void DesignsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSync) return;
            if (sender is not ListBox changedListBox || changedListBox.SelectedItem is not BaseSlideTheme selected)
                return;

            _suppressSelectionSync = true;
            try
            {
                if (changedListBox != generalDesignsListBox) generalDesignsListBox.SelectedItem = null;
                if (changedListBox != songDesignsListBox) songDesignsListBox.SelectedItem = null;
                if (changedListBox != scriptureDesignsListBox) scriptureDesignsListBox.SelectedItem = null;
            }
            finally
            {
                _suppressSelectionSync = false;
            }
            SyncPreviewModeToDesign(selected);
            SyncEditorToSelection();
        }

        public List<XmlFontWeight> FontWeightOptions = new()
        {
            (XmlFontWeight)FontWeight.Thin,
            (XmlFontWeight)FontWeight.ExtraLight,
            (XmlFontWeight)FontWeight.Light,
            (XmlFontWeight)FontWeight.SemiLight,
            (XmlFontWeight)FontWeight.Regular,
            (XmlFontWeight)FontWeight.Medium,
            (XmlFontWeight)FontWeight.SemiBold,
            (XmlFontWeight)FontWeight.Bold,
            (XmlFontWeight)FontWeight.ExtraBold,
            (XmlFontWeight)FontWeight.Black,
            (XmlFontWeight)FontWeight.ExtraBlack,
        };
        
        public SlideThemeDesigner()
        {
            InitializeComponent();

            var fontComboBox = this.Find<ComboBox>("fontComboBox");
            var fontFamilies = FontManager.Current.SystemFonts.Select(x => x.Name).ToList();
            fontFamilies.Sort();
            fontComboBox.ItemsSource = fontFamilies;

            FontWeightComboBox.ItemsSource = FontWeightOptions;

            // TextAlignmentComboBox.ItemsSource = Enum.GetValues(typeof(TextAlignment)).Cast<TextAlignment>();

            themeTypeComboBox.ItemsSource = Enum.GetValues<SlideThemeType>();

            backgroundModeComboBox.ItemsSource = Enum.GetValues<ThemeBackgroundMode>();

            this.WhenAnyValue(v => v.generalDesignsListBox.ItemsSource)
                .Subscribe((x) =>
                {
                    if (SelectedDesign == null)
                    {
                        var first = (generalDesignsListBox.ItemsSource as System.Collections.IEnumerable)?
                            .Cast<BaseSlideTheme>().FirstOrDefault();
                        if (first != null)
                            SelectDesign(first);
                    }
                    SyncEditorToSelection();
                });

            generalDesignsListBox.DataContextChanged += (sender, args) => SyncEditorToSelection();

            AttachedToVisualTree += (_, _) => ActivateHistory();
            DetachedFromVisualTree += (_, _) => DeactivateHistory();
            DataContextChanged += (_, _) => ActivateHistory();
        }

        // ---------------- undo/redo ----------------
        // One history per window (see ThemeEditHistories): it covers edits to every theme, adding/removing themes and
        // "Set as default". It lives outside this control so taking the designer off screen doesn't lose it.

        private ThemeEditHistory? _history;
        private System.ComponentModel.PropertyChangedEventHandler? _historyHandler;
        private System.Collections.ObjectModel.ObservableCollection<BaseSlideTheme>? _watchedDesigns;

        private void ActivateHistory()
        {
            DeactivateHistory();
            if (DataContext is not MainViewModel vm || !this.IsAttachedToVisualTree()) return;

            var window = TopLevel.GetTopLevel(this);
            if (window == null) return;

            _history = ThemeEditHistories.Get(window, vm.Playlist.Designs);
            _historyHandler = (_, _) => RefreshUndoButtons();
            _history.Stack.PropertyChanged += _historyHandler;

            _watchedDesigns = _history.Themes;
            _watchedDesigns.CollectionChanged += OnDesignsChangedByUndo;
            RefreshUndoButtons();
        }

        private void DeactivateHistory()
        {
            if (_history != null && _historyHandler != null) _history.Stack.PropertyChanged -= _historyHandler;
            if (_watchedDesigns != null) _watchedDesigns.CollectionChanged -= OnDesignsChangedByUndo;
            _history = null;
            _historyHandler = null;
            _watchedDesigns = null;
            RefreshUndoButtons();
        }

        // An undo/redo that adds or removes a theme must leave the designer showing something sensible: bring a
        // restored theme into view, and move off a theme that undo just removed.
        private void OnDesignsChangedByUndo(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_history is not { Stack.IsApplying: true }) return;

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                && e.NewItems?.Count > 0 && e.NewItems[0] is BaseSlideTheme added)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => SelectDesign(added), Avalonia.Threading.DispatcherPriority.Background);
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var themes = _history?.Themes;
                    if (themes == null || themes.Count == 0) return;
                    if (SelectedDesign == null || !themes.Contains(SelectedDesign)) SelectDesign(themes[0]);
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        private void RefreshUndoButtons()
        {
            var stack = _history?.Stack;
            UndoButton.IsEnabled = stack?.CanUndo == true;
            RedoButton.IsEnabled = stack?.CanRedo == true;
            ToolTip.SetTip(UndoButton, stack?.UndoMenuText ?? "Undo");
            ToolTip.SetTip(RedoButton, stack?.RedoMenuText ?? "Redo");
        }

        private void UndoButton_OnClick(object? sender, RoutedEventArgs e) => _history?.Stack.Undo();
        private void RedoButton_OnClick(object? sender, RoutedEventArgs e) => _history?.Stack.Redo();

        /// <summary>Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) for this window's theme history, unless a text box has focus (it keeps its own).</summary>
        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || _history == null) return;
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;

            var ctrl = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0;
            var shift = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) != 0;
            var alt = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Alt) != 0;
            if (!ctrl || alt) return;

            if (e.Key == Avalonia.Input.Key.Z && !shift) _history.Stack.Undo();
            else if (e.Key == Avalonia.Input.Key.Y || (e.Key == Avalonia.Input.Key.Z && shift)) _history.Stack.Redo();
            else return;

            e.Handled = true;
        }

        /// <summary>Makes <paramref name="theme"/> the app-wide default of the given kind, as one undoable step.</summary>
        private void SetDefaultTheme(MainViewModel mainViewModel, DefaultThemeKind kind, BaseSlideTheme theme)
        {
            var action = new SetDefaultThemeAction(Globals.Instance.AppPreferences, mainViewModel.Playlist, kind,
                theme.Id, Globals.Instance.SaveAppPreferences);

            if (_history != null) _history.Stack.Execute(action);
            else action.Do();
        }

        private const string PreviewText =
            "Shine Jesus shine\nFill this land\nWith the Father's glory\nBlaze Spirit blaze\nSet our hearts on fire";

        private const string PreviewTitleText = "Amazing Grace";
        private const string PreviewCopyrightText =
            "John Newton\nCCLI Song #22025\nPublic Domain\nCCLI License #317371";

        private static readonly IReadOnlyList<ScriptureVerseRef> PreviewScriptureVerses = new List<ScriptureVerseRef>
        {
            new(3, 16, "For God so loved the world, that he gave his only begotten Son, that whosoever believeth in him should not perish, but have everlasting life."),
            new(3, 17, "For God sent not his Son into the world to condemn the world, but that the world through him might be saved."),
        };

        private const string PreviewScriptureHeader = "John 3:16-17";

        private void SyncEditorToSelection()
        {
            var item = SelectedDesign;
            themeEditorPanel.DataContext = item;
            if (item != null)
            {
                fontComboBox.SelectedValue = item.FontFamilyAsText;
                UpdateFontWeightOptions(item.FontFamilyAsText, item.FontWeight);
                FontWeightComboBox.SelectedValue = item.FontWeight;
                themePreviewSlideView.SetSlide(new SongSlideInstance(null, null, null)
                {
                    Text = PreviewText,
                    Theme = item,
                });
                themePreviewTitleSlideView.SetSlide(new SongTitleSlideInstance(null)
                {
                    Title = PreviewTitleText,
                    Copyright = PreviewCopyrightText,
                    Theme = item,
                });

                var scriptureSlide = new ScriptureSlideInstance(null, "theme-preview") { Theme = item };
                themePreviewScriptureView.SetSlide(scriptureSlide, PreviewScriptureVerses, PreviewScriptureHeader);
            }
            else
            {
                themePreviewSlideView.SetSlide(null);
                themePreviewTitleSlideView.SetSlide(null);
                themePreviewScriptureView.SetSlide(null);
            }
        }

        private void FontComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (themeEditorPanel.DataContext is BaseSlideTheme item)
            {
                // read the ComboBox's own SelectedItem rather than item.FontFamilyAsText - the two-way
                // binding that updates the latter runs off the same event, so its ordering relative to
                // this handler isn't guaranteed.
                var selectedFontFamily = fontComboBox.SelectedItem as string ?? item.FontFamilyAsText;
                UpdateFontWeightOptions(selectedFontFamily, item.FontWeight);
                FontWeightComboBox.SelectedValue = item.FontWeight;
            }
        }

        // Not every family ships every named weight (Arial only has Regular/Bold/Black, say) - offering
        // a weight the family doesn't have just falls back to the nearest match and looks like a no-op.
        private void UpdateFontWeightOptions(string? fontFamilyName, XmlFontWeight currentWeight)
        {
            var available = GetAvailableWeightInts(fontFamilyName);

            List<XmlFontWeight> options;
            if (available.Count == 0)
            {
                // unknown/unmatched family - don't guess, offer the full list
                options = FontWeightOptions;
            }
            else
            {
                options = FontWeightOptions.Where(w => available.Contains((int)w)).ToList();
                if (!available.Contains((int)currentWeight))
                {
                    // keep the theme's existing (now-unavailable) weight visible/selected rather than
                    // silently swapping it out for something else the user didn't choose
                    options.Insert(0, currentWeight);
                }
            }

            FontWeightComboBox.ItemsSource = options;
        }

        private HashSet<int> GetAvailableWeightInts(string? fontFamilyName)
        {
            if (string.IsNullOrWhiteSpace(fontFamilyName))
                return new HashSet<int>();

            if (_fontWeightCache.TryGetValue(fontFamilyName, out var cached))
                return cached;

            var weights = new HashSet<int>();
            using (var styleSet = SKFontManager.Default.GetFontStyles(fontFamilyName))
            {
                for (var i = 0; i < (styleSet?.Count ?? 0); i++)
                    weights.Add(styleSet![i].Weight);
            }

            _fontWeightCache[fontFamilyName] = weights;
            return weights;
        }

        private void PreviewModeToggle_OnChecked(object? sender, RoutedEventArgs e)
        {
            // Avalonia 12's ToggleButton only exposes IsCheckedChanged, which fires twice per
            // group toggle: once when the clicked radio button becomes checked (while the
            // sibling is still stale-checked), and again when the group manager unchecks the
            // sibling. The handler body below is a pure function of all three toggles' current
            // IsChecked state, so it's safe - and necessary - to let it run on every
            // transition: an intermediate pass may briefly show more than one panel, but the
            // final pass (after the siblings settle) recomputes from the final state and
            // corrects it. Guarding to only the first transition (as the old WPF-style
            // Checked-only semantics would) would leave a transient "multiple visible" result
            // uncorrected.
            themePreviewSlideView.IsVisible = previewLyricToggle.IsChecked == true;
            themePreviewTitleSlideView.IsVisible = previewTitleToggle.IsChecked == true;
            themePreviewScriptureView.IsVisible = previewScriptureToggle.IsChecked == true;
        }

        private void MoveToGroup_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: BaseSlideTheme item, Tag: string tagText } &&
                Enum.TryParse<SlideThemeType>(tagText, out var type))
            {
                item.Type = type;
                SelectDesign(item);
            }
        }

        private void SetDefaultSongTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                // also clears this playlist's override of the default
                SetDefaultTheme(mainViewModel, DefaultThemeKind.Song, item);
            }
        }

        private void SetDefaultSongMotionTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                SetDefaultTheme(mainViewModel, DefaultThemeKind.SongMotionBackground, item);
            }
        }

        private void SetDefaultScriptureTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                SetDefaultTheme(mainViewModel, DefaultThemeKind.Scripture, item);
            }
        }

        private void RemoveItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                if (sender is Control control)
                {
                    if (control.DataContext is BaseSlideTheme item)
                    {
                        var prefs = Globals.Instance.AppPreferences;
                        var playlist = mainViewModel.Playlist;
                        if (item.Id == prefs?.DefaultTheme?.Id
                            || item.Id == prefs?.DefaultSongThemeId
                            || item.Id == prefs?.DefaultSongMotionThemeId
                            || item.Id == prefs?.DefaultScriptureThemeId
                            || item.Id == playlist.DefaultSongThemeId
                            || item.Id == playlist.DefaultSongMotionThemeId
                            || item.Id == playlist.DefaultScriptureThemeId)
                        {
                            MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                { Title = "Cannot remove a theme that is set as a default" });
                        }
                        else if (mainViewModel.Playlist.Designs.Count > 1)
                        {
                            var remainingDesigns = mainViewModel.Playlist.Designs.Where(d => d.Id != item.Id).ToList();
                            mainViewModel.Playlist.Designs.Remove(item);
                            if (remainingDesigns.Count > 0)
                                SelectDesign(remainingDesigns[0]);
                        }
                        else
                        {
                            MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                { Title = "Must have at least one slide theme" });
                        }
                    }
                }
            }
        }

        private void AddItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Button { Tag: string tagText } &&
                Enum.TryParse<SlideThemeType>(tagText, out var type))
            {
                var newTheme = new BaseSlideTheme { Type = type };
                mainViewModel.Playlist.Designs.Add(newTheme);
                SelectDesign(newTheme);
            }
        }

        private void DuplicateItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                if (sender is Control control)
                {
                    if (control.DataContext is BaseSlideTheme item)
                    {
                        var copy = new BaseSlideTheme();
                        copy.CopyFrom(item);
                        copy.Id = Guid.NewGuid();
                        copy.Name = $"{item.Name} (Copy)";
                        mainViewModel.Playlist.Designs.Add(copy);
                        SelectDesign(copy);
                    }
                }
            }
        }

        private async void ExportItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                if (sender is Control control)
                {
                    if (control.DataContext is BaseSlideTheme item)
                    {
                        var topLevel = TopLevel.GetTopLevel(this);
                        var xmlFileType = new FilePickerFileType("XML Document")
                        {
                            Patterns = new[] { "*.xml" },
                            MimeTypes = new[] { "text/xml" }
                        };

                        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                        {
                            Title = "Save File",
                            FileTypeChoices = new[] { xmlFileType }
                        });

                        if (file != null)
                        {
                            await using (var stream = await file.OpenWriteAsync())
                            {
                                // OpenWriteAsync may not truncate an existing file; stale trailing bytes would corrupt it.
                                stream.SetLength(0);
                                // Serialize with centralized utility and error handling
                                var ok = await Utils.SlideThemeXmlSerializer.TrySerializeAsync(item, stream);
                                if (!ok)
                                {
                                    MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                    {
                                        Title = "Export failed",
                                        Content = "There was a problem writing the XML for this theme. Please check logs for details."
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void ImportItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var xmlFileType = new FilePickerFileType("XML Document")
                {
                    Patterns = new[] { "*.xml" },
                    MimeTypes = new[] { "text/xml" }
                };

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Save File",
                    FileTypeFilter = new[] { xmlFileType }
                });
                
                if (files.Count >= 1)
                {
                    await using var stream = await files[0].OpenReadAsync();

                    if (Utils.SlideThemeXmlSerializer.TryDeserialize(stream, out var theme) && theme != null)
                    {
                        if (mainViewModel.Playlist.Designs.Any(x => x.Id == theme.Id))
                            theme.Id = Guid.NewGuid();

                        mainViewModel.Playlist.Designs.Add(theme);
                        SelectDesign(theme);
                    }
                    else
                    {
                        MessageBus.Current.SendMessage(new MessageWindowViewModel()
                        {
                            Title = "Import failed",
                            Content = "There was a problem reading the XML for this theme. The file may be invalid or corrupted."
                        });
                    }
                }
            }
        }

        private async void ChangeThemeBgGraphic_OnClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var filePaths = await Globals.Instance.MainViewModel.ShowOpenFileDialog.Handle(
                    new FilePickerOpenOptions()
                    {
                        AllowMultiple = false,
                        Title = "Select Background Graphic",
                        FileTypeFilter = new List<FilePickerFileType>()
                        {
                            new FilePickerFileType("Image Files")
                            {
                                Patterns = new List<string>()
                                {
                                    "*.png",
                                    "*.jpg",
                                    "*.jpeg",
                                    "*.bmp"
                                }
                            },
                            new FilePickerFileType("All Files")
                            {
                                Patterns = new List<string>()
                                {
                                    "*.*"
                                }
                            }
                        }
                    });
                if (filePaths == null || filePaths.Count == 0) return;

                var localPath = filePaths[0].TryGetLocalPath();
                if (AssetLoader.Exists(filePaths[0].Path) || File.Exists(localPath))
                {
                    if (localPath != null && File.Exists(localPath))
                    {
                        var selectedTheme = SelectedDesign;
                        var isSharedDefaultTheme = selectedTheme != null
                            && selectedTheme.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id;

                        if (!isSharedDefaultTheme)
                        {
                            try
                            {
                                localPath = PortableAssetCopier.ResolveOrCopyIntoMediaLibrary(
                                    localPath, Globals.Instance.AppPreferences?.MediaLibraryPath);
                            }
                            catch (MediaLibraryNotConfiguredException)
                            {
                                MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                {
                                    Title = "Media Library not configured",
                                    Content = "Open Set Up and choose a Media Library folder before setting a theme background."
                                });
                                return;
                            }
                        }
                    }

                    bgGraphicFilePath.Text = localPath;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error changing theme background graphic");
            }
        }
    }
}