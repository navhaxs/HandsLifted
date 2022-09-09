using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HandsLiftedApp.Converters;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.ViewModels;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using ReactiveUI;
using System;
using System.Linq;

namespace HandsLiftedApp.Views
{
    public partial class ProjectorWindow : Window
    {
        DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        Grid OverlayControls;

        public ProjectorWindow() : this(null)
        {
        }

        public ProjectorWindow(ViewModelBase? viewModel)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            OverlayControls = this.FindControl<Grid>("OverlayControls");
            this.FindControl<MenuItem>("toggleFullscreen").Click += (s, e) =>
            {
                toggleFullscreen();
            };
            this.FindControl<MenuItem>("close").Click += (s, e) =>
            {
                Close();
            };

            if (Design.IsDesignMode)
                return;

            this.KeyDown += OnKeyDown;

            timer.Tick += (sender, e) =>
            {
                timer.Stop();
                OverlayControls.IsVisible = false;
            };

            timer.Start();
            this.PointerMoved += (o, e) =>
            {
                timer.Stop();
                timer.Start();
                OverlayControls.IsVisible = true;
            };

            this.DataContext = viewModel;


            if (this.Screens.ScreenCount > 1)
            {
                var secondaryScreen = this.Screens.All.Where(screen => screen.Primary == false).First();
                this.Position = new PixelPoint(secondaryScreen.Bounds.X, secondaryScreen.Bounds.Y);
                this.WindowState = WindowState.FullScreen;

                // perhaps a bug, the WindowState.FullScreen needs to be set again for it to stick
                // bug observable in toggle
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.WindowState = WindowState.FullScreen;
                });
                //this.Width = this.Screens.All[1].Bounds.Width;
                //this.Height= this.Screens.All[1].Bounds.Height;
            }

        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ProjectorWindow_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (IControlExtension.FindAncestor<Button>((IControl)e.Source) != null)
                return;

            toggleFullscreen();
        }

        public void toggleFullscreen()
        {
            this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        }
        private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Right:
                case Key.Space:
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.NextSlide });
                    break;
                case Key.Left:
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
                    break;
            }
            e.Handled = true;
        }
    }
}
