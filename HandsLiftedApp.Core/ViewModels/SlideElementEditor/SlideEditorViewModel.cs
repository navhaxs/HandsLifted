using System.Collections.Generic;
using System.Collections.ObjectModel;
using HandsLiftedApp.Data.Data.Models.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels.SlideElementEditor
{
    public class SlideEditorViewModel : ReactiveObject
    {
        private ObservableCollection<CustomSlide> _slides = new();
  
        public ObservableCollection<CustomSlide> Slides
        {
            get => _slides; set => this.RaiseAndSetIfChanged(ref _slides, value);
        }
    }
}