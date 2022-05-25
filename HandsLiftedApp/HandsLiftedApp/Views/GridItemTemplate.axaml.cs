using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Views
{
    public partial class GridItemTemplate : UserControl
    {
        public GridItemTemplate()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);


            Grid root = this.FindControl<Grid>("root");
            root.Tapped += Root_Tapped;
        }

        private void Root_Tapped(object? sender, RoutedEventArgs e)
        {
            string next = ((SongSlide)((Control)sender).DataContext).Text;
            //throw new System.NotImplementedException();
        }
    }
}
