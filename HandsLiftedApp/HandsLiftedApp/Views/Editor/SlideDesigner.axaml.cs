using Avalonia.Controls;
using Avalonia.Media;
using System.Linq;

namespace HandsLiftedApp.Views.Editor
{
    public partial class SlideDesigner : UserControl
    {
        public SlideDesigner()
        {
            InitializeComponent();

            var fontComboBox = this.Find<ComboBox>("fontComboBox");
            fontComboBox.Items = FontManager.Current.GetInstalledFontFamilyNames().Select(x => new FontFamily(x));
            fontComboBox.SelectedIndex = 0;
        }
    }
}
