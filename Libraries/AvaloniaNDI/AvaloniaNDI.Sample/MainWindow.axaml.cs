using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace AvaloniaNDI.Sample
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            //TestButton.Click += TestButton_Click;
        }

        private void TestButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            using (var rtb = new RenderTargetBitmap(new PixelSize(1920, 1080), new Vector(96, 96)))
            {
                rtb.Render(NdiSendContainer.Child);
                rtb.Save("out.png");
            }
        }

        public bool IsContentHighResCheckFunc(NDISendContainer sendContainer)
        {
            /* e.g.
             
            if (((MainWindowViewModel)this.DataContext).ActiveSlide is VideoView)
                return true;

            return false;

            */

            return true;
        }
    }
}
