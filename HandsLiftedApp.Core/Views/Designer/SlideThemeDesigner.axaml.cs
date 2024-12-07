using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Designer
{
    public partial class SlideThemeDesigner : UserControl
    {
        public SlideThemeDesigner()
        {
            InitializeComponent();

            var fontComboBox = this.Find<ComboBox>("fontComboBox");
            var fontFamilies = FontManager.Current.SystemFonts.ToList().OrderBy(x => x.Name);
            fontComboBox.ItemsSource = fontFamilies;
            // fontComboBox.SelectedIndex = 0;

            FontWeightComboBox.ItemsSource = (FontWeight[])Enum.GetNames(typeof(FontWeight))
                .Select(x => Enum.Parse<FontWeight>(x)).Distinct().ToArray();

            TextAlignmentComboBox.ItemsSource = Enum.GetValues(typeof(TextAlignment)).Cast<TextAlignment>();

            this.WhenAnyValue(v => v.designsListBox.ItemsSource)
                .Subscribe((x) =>
                {
                    if (designsListBox.SelectedIndex == -1)
                    {
                        designsListBox.SelectedIndex = 0;
                    }
                });
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
                            LineHeight = item.LineHeight,
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
                var filePaths = await Globals.Instance.MainViewModel.ShowOpenFileDialog.Handle(Unit.Default);
                if (filePaths == null || filePaths.Length == 0) return;

                if (AssetLoader.Exists(new Uri(filePaths[0])) || File.Exists(filePaths[0]))
                {
                    bgGraphicFilePath.Text = filePaths[0];
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }
        }

        private void FontWeightComboBox_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            e.Handled = true;
        }
    }
}