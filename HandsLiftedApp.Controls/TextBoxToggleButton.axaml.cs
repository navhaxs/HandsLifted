using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace HandsLiftedApp.Controls
{
    public partial class TextBoxToggleButton : UserControl
    {
        public TextBoxToggleButton()
        {
            InitializeComponent();
        }

        public static readonly DirectProperty<TextBoxToggleButton, Color> HoverBrushProperty =
            AvaloniaProperty.RegisterDirect<TextBoxToggleButton, Color>(
                nameof(HoverBrush),
                o => o.HoverBrush,
                (o, v) => o.HoverBrush = v,
                Color.Parse("#5C3AB6")
            );

        private Color _hoverBrush = Color.Parse("#5C3AB6");

        public Color HoverBrush
        {
            get { return _hoverBrush; }
            set { SetAndRaise(HoverBrushProperty, ref _hoverBrush, value); }
        }

        public static readonly DirectProperty<TextBoxToggleButton, string> TextProperty =
            AvaloniaProperty.RegisterDirect<TextBoxToggleButton, string>(
                nameof(Text),
                o => o.Text,
                (o, v) => o.Text = v,
                "",
                BindingMode.TwoWay
            );

        private string _text = "";

        public string Text
        {
            get { return _text; }
            set { SetAndRaise(TextProperty, ref _text, value); }
        }

        public static readonly DirectProperty<TextBoxToggleButton, string> WatermarkProperty =
            AvaloniaProperty.RegisterDirect<TextBoxToggleButton, string>(
                nameof(Watermark),
                o => o.Watermark,
                (o, v) => o.Watermark = v);

        private string _watermark = "";

        public string Watermark
        {
            get { return _watermark; }
            set { SetAndRaise(WatermarkProperty, ref _watermark, value); }
        }

        private void EditButton_OnClick(object? sender, RoutedEventArgs e)
        {
            EntryTextBox.Text = Text;
            Carousel.SelectedIndex = 1;
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Dispatcher.UIThread.RunJobs();
                EntryTextBox.Focus();
            });
        }

        private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Carousel.SelectedIndex = 0;
                    Text = EntryTextBox.Text ?? string.Empty;
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Carousel.SelectedIndex = 0;
                    e.Handled = true;
                    break;
            }
        }

        private void SubmitButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Carousel.SelectedIndex = 0;
            Text = EntryTextBox.Text ?? string.Empty;
            e.Handled = true;
        }

        private void EntryTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
        {
            Carousel.SelectedIndex = 0;
            e.Handled = true;
        }
    }
}