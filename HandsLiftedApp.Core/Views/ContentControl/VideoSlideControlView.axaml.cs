using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;

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
            Globals.Instance.MpvContextInstance.Command("stop");
            Globals.Instance.MpvContextInstance.SetPropertyFlag("pause", true);
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is VideoSlideInstance videoSlideInstance)
            {
                if (videoSlideInstance.TimePos == null)
                {
                    videoSlideInstance.PlayFromStart();
                    return;
                }
            }
            
            Globals.Instance.MpvContextInstance.Command("cycle", "pause");;
        }
    }
}
