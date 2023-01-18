using Avalonia.Controls;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models.SlideState;

namespace HandsLiftedApp.Views.Editor
{
    public partial class SlideInfoWindow : Window
    {
        public SlideInfoWindow()
        {
            InitializeComponent();

            CancelButton.Click += (s, e) => Close();
        }
    }
}
