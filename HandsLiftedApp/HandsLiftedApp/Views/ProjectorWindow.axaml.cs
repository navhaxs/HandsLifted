using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HandsLiftedApp.Converters;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.ViewModels;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
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

            if (Design.IsDesignMode)
                return;

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
                var secondaryScreen = this.Screens.All.Where(screen => screen.Primary == false).Last();
                this.Position = new PixelPoint(secondaryScreen.Bounds.X, secondaryScreen.Bounds.Y);
                this.WindowState = WindowState.FullScreen;
                //this.Width = this.Screens.All[1].Bounds.Width;
                //this.Height= this.Screens.All[1].Bounds.Height;
            }

        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);


            //if (Design.IsDesignMode)
            //    return;


            //VideoView = this.Get<VideoView>("VideoView");

            //_libVLC = new LibVLC();
            //_mediaPlayer = new MediaPlayer(_libVLC);
            //VideoView.MediaPlayer = _mediaPlayer;
        }

        private void ProjectorWindow_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (IControlExtension.FindAncestor<Button>((IControl)e.Source) != null)
                return;


            this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        }


        //private void StopButton_Click(object sender, RoutedEventArgs e)
        //{
        //    if (VideoView.MediaPlayer.IsPlaying)
        //    {
        //        VideoView.MediaPlayer.Stop();
        //    }
        //}

        //private void PlayButton_Click(object sender, RoutedEventArgs e)
        //{
        //    if (!VideoView.MediaPlayer.IsPlaying)
        //    {
        //        VideoView.MediaPlayer.Play(new Media(_libVLC,
        //            "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4", FromType.FromLocation));
        //    }
        //}

        //private void PauseButton_Click(object sender, RoutedEventArgs e)
        //{
        //    VideoView.MediaPlayer.Pause();
        //}
    }
}
