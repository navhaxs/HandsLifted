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
            var fontFamilies = FontManager.Current.SystemFonts.ToList();
            //fontFamilies.Sort();
            fontComboBox.ItemsSource = fontFamilies;
            fontComboBox.SelectedIndex = 0;
        }
    }
}
