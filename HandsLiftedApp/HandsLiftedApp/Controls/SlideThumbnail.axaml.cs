using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Controls
{
    public partial class SlideThumbnail : UserControl
    {
        public SlideThumbnail()
        {
            InitializeComponent();
            this.PointerPressed += SlideThumbnail_PointerPressed;
        }

        private void SlideThumbnail_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            //e.Handled = false;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
