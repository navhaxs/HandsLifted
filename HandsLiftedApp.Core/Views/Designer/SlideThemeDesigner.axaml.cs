using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.UI;
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

namespace HandsLiftedApp.Core.Views.Designer
{
    public partial class SlideThemeDesigner : UserControl
    {

        public class FontWeightOption
        {
            public FontWeight FontWeight;
            public String Label;
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
            (XmlFontWeight)FontWeight.Black,
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

        private void SyncEditorToSelection()
        {
            var item = designsListBox.SelectedItem as BaseSlideTheme;
            themeEditorPanel.DataContext = item;
            if (item != null)
            {
                fontComboBox.SelectedValue = item.FontFamilyAsText;
                FontWeightComboBox.SelectedValue = item.FontWeight;
                themePreviewSlideView.SetSlide(new SongSlideInstance(null, null, null)
                {
                    Text = PreviewText,
                    Theme = item,
                });
            }
            else
            {
                themePreviewSlideView.SetSlide(null);
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
                        if (item.Id == Globals.Instance.AppPreferences?.DefaultTheme?.Id)
                        {
                            MessageBus.Current.SendMessage(new MessageWindowViewModel()
                                { Title = "Cannot remove the global default theme" });
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

                if (AssetLoader.Exists(filePaths[0].Path) || File.Exists(filePaths[0].TryGetLocalPath()))
                {
                    bgGraphicFilePath.Text = filePaths[0].TryGetLocalPath();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error changing theme background graphic");
            }
        }
    }
}