using Avalonia;
using Avalonia.Controls;
using Material.Icons;

namespace HandsLiftedApp.Core.Controls
{
    public class IconButton : Button
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<IconButton, string>(nameof(Text));

        public string Text
        {
            get { return GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }
        
        public static readonly StyledProperty<MaterialIconKind> KindProperty =
            AvaloniaProperty.Register<IconButton, MaterialIconKind>(nameof(Kind));

        public MaterialIconKind Kind
        {
            get { return GetValue(KindProperty); }
            set { SetValue(KindProperty, value); }
        }
    }
}