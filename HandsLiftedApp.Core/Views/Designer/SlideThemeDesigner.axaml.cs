using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Designer
{
    public partial class SlideThemeDesigner : UserControl
    {
        public SlideThemeDesigner()
        {
            InitializeComponent();

            // var fontComboBox = this.Find<ComboBox>("fontComboBox");
            // var fontFamilies = FontManager.Current.SystemFonts.ToList().OrderBy(x => x.Name);
            // fontComboBox.ItemsSource = fontFamilies;
            // fontComboBox.SelectedIndex = 0;

            // FontWeightComboBox.ItemsSource = (FontWeight[])Enum.GetNames(typeof(FontWeight)).Select(x => Enum.Parse<FontWeight>(x)).ToArray();

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
            Globals.AppPreferences.Designs.Add(new BaseSlideTheme());
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

        private async void ChangeThemeBgGraphic_OnClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var filePaths = await Globals.MainViewModel.ShowOpenFileDialog.Handle(Unit.Default);
                if (filePaths == null || filePaths.Length == 0) return;

                if (AssetLoader.Exists(new Uri(filePaths[0])) || File.Exists(filePaths[0]))
                {
                    // bgGraphicFilePath.Text = filePaths[0];
                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
            }        
        }
    }
}