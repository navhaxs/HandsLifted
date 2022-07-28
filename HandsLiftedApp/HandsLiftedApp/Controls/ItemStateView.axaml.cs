using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Controls
{
    public partial class ItemStateView : UserControl
    {
        public ItemStateView()
        {
            InitializeComponent();

            var livePreview = this.FindControl<ListBox>("List");
            livePreview.PointerReleased += LivePreview_PointerReleased; ;

            this.PointerPressed += ItemStateView_PointerPressed;
        }

        private void ItemStateView_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // disable clicking white space to change selected item index
            var x = sender;
            e.Handled = true;
        }

        private void LivePreview_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            var x = sender;
        }

        private void LivePreview_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            var x = sender;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
