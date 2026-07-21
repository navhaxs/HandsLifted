using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace HandsLiftedApp.Controls
{
    public partial class NumericSliderBox : UserControl
    {
        public NumericSliderBox()
        {
            InitializeComponent();
        }

        public static readonly DirectProperty<NumericSliderBox, string> TextProperty =
            AvaloniaProperty.RegisterDirect<NumericSliderBox, string>(
                nameof(Text),
                o => o.Text,
                (o, v) => o.Text = v,
                "",
                BindingMode.TwoWay
            );

        private string _text = "";

        public string Text
        {
            get => _text;
            set => SetAndRaise(TextProperty, ref _text, value);
        }

        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<NumericSliderBox, double>(nameof(Minimum), 0);

        public double Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<NumericSliderBox, double>(nameof(Maximum), 100);

        public double Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly StyledProperty<double> TickFrequencyProperty =
            AvaloniaProperty.Register<NumericSliderBox, double>(nameof(TickFrequency), 1);

        public double TickFrequency
        {
            get => GetValue(TickFrequencyProperty);
            set => SetValue(TickFrequencyProperty, value);
        }

        public static readonly StyledProperty<double> TextBoxMinWidthProperty =
            AvaloniaProperty.Register<NumericSliderBox, double>(nameof(TextBoxMinWidth), 60);

        public double TextBoxMinWidth
        {
            get => GetValue(TextBoxMinWidthProperty);
            set => SetValue(TextBoxMinWidthProperty, value);
        }
    }
}
