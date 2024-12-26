using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Xml.Serialization;
using DryIoc.ImTools;
using HandsLiftedApp.Data.Data.Models.Types;

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
            var fontFamilies = FontManager.Current.SystemFonts.ToList().Map(x => x.Name);
            fontComboBox.ItemsSource = fontFamilies;

            FontWeightComboBox.ItemsSource = FontWeightOptions;

            // TextAlignmentComboBox.ItemsSource = Enum.GetValues(typeof(TextAlignment)).Cast<TextAlignment>();

            this.WhenAnyValue(v => v.designsListBox.ItemsSource)
                .Subscribe((x) =>
                {
                    if (designsListBox.SelectedIndex == -1)
                    {
                        designsListBox.SelectedIndex = 0;
                    }
                });

            designsListBox.SelectionChanged += (sender, args) =>
            {
                if (designsListBox.SelectedValue is BaseSlideTheme item)
                {
                    fontComboBox.SelectedValue = item.FontFamilyAsText;
                }
            };
            
            designsListBox.DataContextChanged += (sender, args) =>
            {
                if (designsListBox.SelectedValue is BaseSlideTheme item)
                {
                    fontComboBox.SelectedValue = item.FontFamilyAsText;
                    FontWeightComboBox.SelectedValue = item.FontWeight;
                }
            };
        
        }

        private void RemoveItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                if (sender is Control control)
                {
                    if (control.DataContext is BaseSlideTheme item)
                    {
                        if (mainViewModel.Playlist.Designs.Count > 1)
                        {
                            // TODO what happens if this theme is in use?
                            designsListBox.SelectedIndex =
                                0; // hack, keep at least 1 selected valid item to avoid avalonia crashing on some null font error
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
            Globals.Instance.AppPreferences.Designs.Add(new BaseSlideTheme());
            // if (this.DataContext is MainViewModel mainViewModel)
            // {
            //     mainViewModel.Playlist.Designs.Add(new BaseSlideTheme());
            // }
        }

        private void DuplicateItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                if (sender is Control control)
                {
                    if (control.DataContext is BaseSlideTheme item)
                    {
                        mainViewModel.Playlist.Designs.Add(new BaseSlideTheme()
                        {
                            Name = $"{item.Name} (Copy)",
                            FontFamilyAsAvalonia = FontFamily.Parse("Arial"), //dxitem.FontFamily,
                            FontWeight = item.FontWeight,
                            TextColour = item.TextColour,
                            TextAlignment = item.TextAlignment,
                            BackgroundColour = item.BackgroundColour,
                            FontSize = item.FontSize,
                            LineHeightEm = item.LineHeightEm,
                            BackgroundGraphicFilePath = item.BackgroundGraphicFilePath,
                        });
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
                            // User selected a file
                            // Save the file content
                            using (var stream = await file.OpenWriteAsync())
                            {
                                // Write content to the file
                                // Assuming you have a method to serialize the object to XML
                                XmlSerializer serializer = new XmlSerializer(typeof(BaseSlideTheme));

                                serializer.Serialize(stream, item);
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

                    XmlSerializer serializer = new XmlSerializer(typeof(BaseSlideTheme));

                    var item = serializer.Deserialize(stream);
                    if (item is BaseSlideTheme theme)
                    {
                        var existingMatch = mainViewModel.Playlist.Designs.FirstOrDefault(x => x.Id == theme.Id);
                        if (existingMatch != null)
                        {
                            theme.Id = new Guid();
                        }

                        // mainViewModel.Playlist.Designs.Add(theme);
                        // Globals.Instance.AppPreferences.Designs.Add(theme);
                        Globals.Instance.AppPreferences.DefaultTheme.CopyFrom(theme);
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
                Debug.Print(ex.Message);
            }
        }
    }
}