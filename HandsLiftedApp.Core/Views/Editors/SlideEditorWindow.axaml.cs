using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.ViewModels.SlideElementEditor;
using HandsLiftedApp.Data.Data.Models.Slides;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class SlideEditorWindow : Window
    {
        public SlideEditorWindow()
        {
            InitializeComponent();
            this.DataContext = new SlideEditorViewModel() { Slides = new() {new CustomSlide()}};
        }
    }
}