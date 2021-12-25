using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.ViewModels;

namespace HandsLiftedApp.Views
{
    public partial class ProjectorWindow : Window
    {
        public ProjectorWindow() : this(null)
        {
           
        } 
        
        public ProjectorWindow(ViewModelBase? viewModel)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            this.DataContext = viewModel; // new ProjectorViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ProjectorWindow_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        }
    }
}
