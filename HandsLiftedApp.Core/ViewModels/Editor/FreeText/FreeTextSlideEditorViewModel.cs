using HandsLiftedApp.Data.Data.Models.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels.Editor.FreeText
{
    public class FreeTextSlideEditorViewModel : ReactiveObject
    {
        private CustomSlide _slide; 
        public CustomSlide Slide
        {
            get => _slide; set => this.RaiseAndSetIfChanged(ref _slide, value);
        }
    }
}