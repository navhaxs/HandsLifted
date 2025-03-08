using Avalonia.Controls;
using HandsLiftedApp.Core.ViewModels.Editor.FreeText;
using HandsLiftedApp.Data.Data.Models.Slides;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class SlideEditorWindow : Window
    {
        public SlideEditorWindow()
        {
            InitializeComponent();
            // if (this.DataContext == null)
            // {
            //     this.DataContext = new FreeTextSlideEditorViewModel() { Slide = new CustomSlide() };
            // }
            // this.DataContext = new SlideEditorViewModel() { Slides = new() {new CustomSlide()}};
        }
    }
}