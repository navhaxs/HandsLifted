using ReactiveUI;

namespace HandsLiftedApp.ViewModels
{
    public class StageDisplayViewModel : ReactiveObject
    {
        private int _SelectedIndex = 1;
        public int SelectedIndex
        {
            get => _SelectedIndex;
            set => this.RaiseAndSetIfChanged(ref _SelectedIndex, value);
        }
    }
}
