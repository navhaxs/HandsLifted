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

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.Playlist.Designs.Add(new BaseSlideTheme());
            }
        }
    }
}
