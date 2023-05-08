using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using System;

namespace HandsLiftedApp.Views.ControlModules
{
    public partial class VideoSlideControlView : UserControl
    {
        public VideoSlideControlView()
        {
            InitializeComponent();

            this.DataContextChanged += VideoSlide_DataContextChanged;
            this.AttachedToVisualTree += VideoSlideRenderer_AttachedToVisualTree;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void VideoSlideRenderer_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (this.DataContext is VideoSlide<VideoSlideStateImpl>)
            {

                MediaPlayer = ((VideoSlide<VideoSlideStateImpl>)this.DataContext).State.MediaPlayer;
            }
        }

        private void VideoSlide_DataContextChanged(object? sender, EventArgs e)
        {
            if (this.DataContext == null)
            {
                MediaPlayer = null;
            }
            else if (this.DataContext is VideoSlide<VideoSlideStateImpl>)
            {
                MediaPlayer = ((VideoSlide<VideoSlideStateImpl>)this.DataContext).State.MediaPlayer;
            }
        }

        private MediaPlayer? MediaPlayer;

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.Stop();
            }
            // Globals.GlobalMpvContext.Command("stop");
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            // Globals.GlobalMpvContext.SetPropertyFlag("pause", false);

            if (MediaPlayer != null && !MediaPlayer.IsPlaying)
            {
                if (MediaPlayer.State == VLCState.Ended)
                {
                    MediaPlayer.Stop();
                    MediaPlayer.Position = 0;
                }
                MediaPlayer.Play();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer != null)
            {
                MediaPlayer.Pause();
            }
            // Globals.GlobalMpvContext.SetPropertyFlag("pause", true);
        }
    }
}
