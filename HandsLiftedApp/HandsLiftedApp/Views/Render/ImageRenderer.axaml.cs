using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.XTransitioningContentControl;
using NetOffice.PowerPointApi;
using System.IO;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views.Render
{
    public partial class ImageRenderer : UserControl
    {
        public ImageRenderer()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
