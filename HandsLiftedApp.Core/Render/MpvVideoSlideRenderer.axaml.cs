using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Core.Views;
using Serilog;

namespace HandsLiftedApp.Core.Render
{
    public partial class MpvVideoSlideRenderer : UserControl
    {
        bool _isMounted = false;
        private VideoSlideInstance? _attachedSlide;

        public MpvVideoSlideRenderer()
        {
            InitializeComponent();

            this.AttachedToVisualTree += VideoSlideRenderer_AttachedToVisualTree;
            this.DetachedFromVisualTree += MpvVideoSlideRenderer_DetachedFromVisualTree;
        }

        private void MpvVideoSlideRenderer_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_isMounted)
            {
                // Mirrors the ISlideRender lifecycle the legacy XTransitioningContentControl
                // used to drive - stops playback and cancels any pending delayed-start task.
                _attachedSlide?.OnLeaveSlide();
            }

            VideoView.MpvContext = null;
            _attachedSlide = null;
            _isMounted = false;
        }

        private void VideoSlideRenderer_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (this.DataContext is VideoSlideInstance videoSlide)
            {
                Log.Debug("MpvVideoSlideRenderer Attached");
                _isMounted = true;
                _attachedSlide = videoSlide;
                VideoView.MpvContext = Globals.Instance.MpvContextInstance;

                // This is now the only call site that reaches VideoSlideInstance.OnEnterSlide() -
                // the current SkiaSharp render pipeline (LivePane/ProjectorWindow) no longer uses
                // XTransitioningContentControl, which used to drive ISlideRender.OnEnterSlide/OnLeaveSlide.
                videoSlide.OnEnterSlide();
            }
        }
    }
}