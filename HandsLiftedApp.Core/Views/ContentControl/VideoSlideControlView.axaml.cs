using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using HandsLiftedApp.Core;

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
        }

        private void VideoSlide_DataContextChanged(object? sender, EventArgs e)
        {
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.MpvContextInstance.Command("stop");
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.MpvContextInstance.SetPropertyFlag("pause", false);
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            Globals.MpvContextInstance.SetPropertyFlag("pause", true);
        }
    }
}
