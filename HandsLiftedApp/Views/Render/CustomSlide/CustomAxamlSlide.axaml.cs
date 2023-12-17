using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.Render.CustomSlide
{
    public partial class CustomAxamlSlide : UserControl
    {
        public CustomAxamlSlide()
        {
            InitializeComponent();

            // TODO CustomAxamlSlide.axaml
            var xaml = @"<ContentControl xmlns='https://github.com/avaloniaui' Background='#006eff' Content='My XAML loaded data'/>";

            var target = AvaloniaRuntimeXamlLoader.Parse<ContentControl>(xaml);
            this.Content = target;
        }
    }
}
