using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.ViewModels.Editor;
using HandsLiftedApp.Views.Editor;
using ReactiveUI;
using System;
using System.ComponentModel;

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
            this.Closed += MainWindow_Closed;
            //var x = new MainWindowViewModel();

            //this.DataContext = x;

            SplashWindow v = new SplashWindow();
            v.Show();

            //WebBrowserWindow w = new WebBrowserWindow();
            //w.Show();

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

            // TODO: debug
            // TODO: debug
            // TODO: debug
            // TODO: debug
            // TODO: debug
            // TODO: debug
            //SongEditorWindow songEditorWindow = new SongEditorWindow();
            //songEditorWindow.DataContext = new SongEditorViewModel();
            //songEditorWindow.Show();
               

            this.KeyDown += ZoomBorder_KeyDown;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);


        }

        private void ZoomBorder_KeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                ListBox _list = this.Find<ListBox>("List");

                switch (e.Key)
                {
                    case Key.Right:
                        _list.SelectedIndex += 1;
                        break;
                    case Key.Left:
                        _list.SelectedIndex -= 1;
                        break;
                }
            }
            catch (System.Exception)
            {

                throw;
            }
          
        }

        private void MainWindow_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        }
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            ((ClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current.ApplicationLifetime).Shutdown(0);
        }
    }
}
