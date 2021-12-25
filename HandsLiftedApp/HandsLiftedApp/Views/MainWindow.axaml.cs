using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using HandsLiftedApp.ViewModels;
using ReactiveUI;

namespace HandsLiftedApp.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            //var x = new MainWindowViewModel();

            //this.DataContext = x;

            SplashWindow v = new SplashWindow();
            v.Show();

            //WebBrowserWindow w = new WebBrowserWindow();
            //w.Show();

            TestWindow testWindow = new TestWindow();
            testWindow.Show();

            //LowerThirdSlideTemplate livePreview = this.FindControl<LowerThirdSlideTemplate>("LivePreview");
            //livePreview.DataContext = x;
            //new ProjectorViewModel();
            // Setup the bindings.
            // Note: We have to use WhenActivated here, since we need to dispose the
            // bindings on XAML-based platforms, or else the bindings leak memory.
            //this.Bind(ViewModel, x => x.UserName, x => x.Username.Text);
            //    //this.Bind(ViewModel, x => x.Password, x => x.Password.Text)
            //    //    .DisposeWith(disposable);
            //    //this.Bind(ViewModel, x => x.Address, x => x.Address.Text)
            //    //   .DisposeWith(disposable);
            //    //this.Bind(ViewModel, x => x.Phone, x => x.Phone.Text)
            //    //   .DisposeWith(disposable);
            //    //this.BindCommand(ViewModel, x => x.RegisterCommand, x => x.Register)

            //    //    .DisposeWith(disposable);
            //    //this.Bind(ViewModel, x => x.Result, x => x.Result.Text)
            //    //   .DisposeWith(disposable);
            //});

            //LowerThirdSlideTemplate nextPreview = this.FindControl<LowerThirdSlideTemplate>("NextPreview");
            //nextPreview.DataContext = DataContext; // new ProjectorViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void MainWindow_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        }
    }
}
