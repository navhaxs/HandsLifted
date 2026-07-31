using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
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
using DryIoc.ImTools;
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
            var fontFamilies = FontManager.Current.SystemFonts.Map(x => x.Name).ToList();
            fontFamilies.Sort();
            fontComboBox.ItemsSource = fontFamilies;

            FontWeightComboBox.ItemsSource = FontWeightOptions;

            // TextAlignmentComboBox.ItemsSource = Enum.GetValues(typeof(TextAlignment)).Cast<TextAlignment>();

            this.WhenAnyValue(v => v.designsListBox.ItemsSource)
                .Subscribe((x) =>
                {
                    if (designsListBox.SelectedIndex == -1)
                        designsListBox.SelectedIndex = 0;
                    SyncEditorToSelection();
                });

            designsListBox.SelectionChanged += (sender, args) => SyncEditorToSelection();

            designsListBox.DataContextChanged += (sender, args) => SyncEditorToSelection();
        }

        private const string PreviewText =
            "Shine Jesus shine\nFill this land\nWith the Father's glory\nBlaze Spirit blaze\nSet our hearts on fire";

        private const string PreviewTitleText = "Amazing Grace";
        private const string PreviewCopyrightText =
            "John Newton\nCCLI Song #22025\nPublic Domain\nCCLI License #317371";

        private void SyncEditorToSelection()
        {
            var item = designsListBox.SelectedItem as BaseSlideTheme;
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
            }
            else
            {
                themePreviewSlideView.SetSlide(null);
                themePreviewTitleSlideView.SetSlide(null);
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
            // sibling. The handler body below is a pure function of both toggles' current
            // IsChecked state, so it's safe - and necessary - to let it run on both
            // transitions: the first pass may briefly show both panels, but the second pass
            // (after the sibling settles) recomputes from the final state and corrects it.
            // Guarding to only the first transition (as the old WPF-style Checked-only
            // semantics would) would leave the transient "both visible" result uncorrected.
            themePreviewSlideView.IsVisible = previewLyricToggle.IsChecked == true;
            themePreviewTitleSlideView.IsVisible = previewTitleToggle.IsChecked == true;
        }

        private void SetDefaultSongTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                mainViewModel.Playlist.DefaultSongThemeId = item.Id;
            }
        }

        private void SetDefaultSongMotionTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                mainViewModel.Playlist.DefaultSongMotionThemeId = item.Id;
            }
        }

        private void SetDefaultScriptureTheme_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Control control &&
                control.DataContext is BaseSlideTheme item)
            {
                mainViewModel.Playlist.DefaultScriptureThemeId = item.Id;
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
                        if (item.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id
                            || item.Id == mainViewModel.Playlist.DefaultSongThemeId
                            || item.Id == mainViewModel.Playlist.DefaultSongMotionThemeId
                            || item.Id == mainViewModel.Playlist.DefaultScriptureThemeId)
                        {
                            MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                { Title = "Cannot remove a theme that is set as a default" });
                        }
                        else if (mainViewModel.Playlist.Designs.Count > 1)
                        {
                            designsListBox.SelectedIndex = 0;
                            mainViewModel.Playlist.Designs.Remove(item);
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
            if (this.DataContext is MainViewModel mainViewModel)
            {
                var newTheme = new BaseSlideTheme();
                mainViewModel.Playlist.Designs.Add(newTheme);
                designsListBox.SelectedIndex = mainViewModel.Playlist.Designs.Count - 1;
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
                        designsListBox.SelectedIndex = mainViewModel.Playlist.Designs.Count - 1;
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
                        designsListBox.SelectedIndex = mainViewModel.Playlist.Designs.Count - 1;
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
                        var selectedTheme = designsListBox.SelectedItem as BaseSlideTheme;
                        var isSharedDefaultTheme = selectedTheme != null
                            && selectedTheme.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id;

                        if (!isSharedDefaultTheme)
                        {
                            localPath = PortableAssetCopier.CopyIntoSubfolder(
                                localPath,
                                Globals.Instance.MainViewModel.Playlist.PlaylistWorkingDirectory,
                                Path.Combine("Themes", "Backgrounds"));
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