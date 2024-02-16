using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
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

            var fontComboBox = this.Find<ComboBox>("fontComboBox");
            var fontFamilies = FontManager.Current.SystemFonts.ToList();
            //fontFamilies.Sort();
            fontComboBox.ItemsSource = fontFamilies;
            fontComboBox.SelectedIndex = 0;

            TextAlignmentComboBox.ItemsSource = Enum.GetValues(typeof(TextAlignment)).Cast<TextAlignment>();

            // designsListBox.DataContextChanged += (sender, args) =>
            // {
            this.WhenAnyValue(v => v.designsListBox.ItemsSource)
                .Subscribe((x) =>
                {
                    if (designsListBox.SelectedIndex == -1)
                    {
                        designsListBox.SelectedIndex = 0;
                    }
                });
            // };
        }

        private void RemoveItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                if (sender is Control control)
                {
                    if (control.DataContext is BaseSlideTheme item)
                    {
                        mainViewModel.Playlist.Designs.Remove(item);
                    }
                }
            }
        }

        private void AddItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.Playlist.Designs.Add(new BaseSlideTheme());
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
                        mainViewModel.Playlist.Designs.Add(new BaseSlideTheme()
                        {
                            Name = $"{item.Name} (Copy)",
                            FontFamily = FontFamily.Parse("Arial"),//dxitem.FontFamily,
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
    }
}