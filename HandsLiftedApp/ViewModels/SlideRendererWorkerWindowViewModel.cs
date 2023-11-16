using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;

namespace HandsLiftedApp.ViewModels
    {
    public class SlideRendererWorkerWindowViewModel : ReactiveObject
        {
        public BaseSlideTheme _slideTheme;
        public BaseSlideTheme SlideTheme { get => _slideTheme; set => this.RaiseAndSetIfChanged(ref _slideTheme, value); }

        }
    }
