using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace HandsLiftedApp.Controls
{
    public partial class TextBoxToggleButton : UserControl
    {
        public TextBoxToggleButton()
        {
            InitializeComponent();

            this.KeyDown += (s, e) => { e.Handled = true; };
        }

        public static readonly DirectProperty<TextBoxToggleButton, string> TextProperty =
            AvaloniaProperty.RegisterDirect<TextBoxToggleButton, string>(
                nameof(Text),
                o => o.Text,
                (o, v) => o.Text = v);

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
                case Key.Escape:
                    Carousel.SelectedIndex = 0;
                    e.Handled = true;
                    break;
            }
        }

        private void SubmitButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Carousel.SelectedIndex = 0;
            e.Handled = true;
        }

        private void EntryTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
        {
            Carousel.SelectedIndex = 0;
            e.Handled = true;
        }
    }
}