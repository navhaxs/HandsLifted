using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaNDI;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using System;
using System.Linq;

namespace HandsLiftedApp.Views
{
    public partial class ProjectorWindow : Window
    {
        DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        //Grid OverlayControls;

        public ProjectorWindow() : this(null)
        {
        }

        public ProjectorWindow(ViewModelBase? viewModel)
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            this.Closing += ProjectorWindow_Closing;

            //OverlayControls = this.FindControl<Grid>("OverlayControls");
            this.FindControl<MenuItem>("toggleFullscreen").Click += (s, e) =>
            {
                onToggleFullscreen();
            };
            this.FindControl<MenuItem>("toggleTopmost").Click += (s, e) =>
            {
                this.Topmost = !this.Topmost;
            };
            this.FindControl<MenuItem>("close").Click += (s, e) =>
            {
                Close();
            };

            if (Design.IsDesignMode)
                return;

            bool initial = true;

            this.Opened += (s, e) =>
            {
                if (initial && !Globals.Preferences.OnStartupShowOutput)
                {
                    UpdateWindowVisibility(false);
                }

                this.WhenAnyValue(wnd => wnd.IsVisible)
                    .Subscribe(UpdateWindowVisibility);

                initial = false;
            };

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
                onToggleFullscreen(true);

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

        public void UpdateWindowVisibility(bool isVisible)
        {
            this.IsVisible = isVisible;
            this.ShowInTaskbar = !isVisible;
            this.Opacity = isVisible ? 1 : 0;
            this.IsHitTestVisible = isVisible;
            this.SystemDecorations = isVisible ? SystemDecorations.Full : SystemDecorations.None;
            this.Height = 0;
            this.Width = 0;
        }

        private void ProjectorWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            UpdateWindowVisibility(false);
            e.Cancel = true;
        }

        private void ProjectorWindow_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (ControlExtension.FindAncestor<Button>((Control)e.Source) != null)
                return;

            onToggleFullscreen();
        }

        public void onToggleFullscreen(bool? fullscreen = null)
        {
            bool isFullScreenNext = (fullscreen != null) ? (bool)fullscreen : (this.WindowState != WindowState.FullScreen);
            this.WindowState = isFullScreenNext ? WindowState.FullScreen : WindowState.Normal;
            //this.Topmost = isFullScreenNext;
        }

        public bool IsContentHighResCheckFunc(NDISendContainer sendContainer)
        {
            if (((MainWindowViewModel)this.DataContext).ActiveSlide is VideoSlide<VideoSlideStateImpl>)
                return true;

            return false;
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
