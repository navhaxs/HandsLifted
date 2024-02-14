using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.SlideTheme;

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
