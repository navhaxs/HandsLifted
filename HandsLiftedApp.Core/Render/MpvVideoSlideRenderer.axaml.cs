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

        public MpvVideoSlideRenderer()
        {
            InitializeComponent();

            this.AttachedToVisualTree += VideoSlideRenderer_AttachedToVisualTree;
            this.DetachedFromVisualTree += MpvVideoSlideRenderer_DetachedFromVisualTree;
        }

        private void MpvVideoSlideRenderer_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_isMounted && VideoView.MpvContext != null)
            {
                //Globals.GlobalMpvContextInstance.SetPropertyFlag("pause", true);
            }

            VideoView.MpvContext = null;
            _isMounted = false;
        }

        private void VideoSlideRenderer_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (this.DataContext is VideoSlideInstance videoSlide)
            {
                Log.Debug("MpvVideoSlideRenderer Attached");
                _isMounted = true;
                VideoView.MpvContext = Globals.Instance.MpvContextInstance;
            }
        }
    }
}