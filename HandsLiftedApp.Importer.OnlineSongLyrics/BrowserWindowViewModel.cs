using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace HandsLiftedApp.Importer.OnlineSongLyrics
{
    public class BrowserWindowViewModel : ReactiveObject
    {
        private string _selectedClipboardData = (Design.IsDesignMode) ? BrowserWindow.TEST_DATA : string.Empty;
        
        public string SelectedClipboardData { get => _selectedClipboardData; set => this.RaiseAndSetIfChanged(ref _selectedClipboardData, value); }
        
        private ObservableAsPropertyHelper<string> _selectedClipboardDataTitle;

        public string SelectedClipboardDataTitle
        {
            get => _selectedClipboardDataTitle?.Value;
        }

        public BrowserWindowViewModel()
        {
            _selectedClipboardDataTitle = this.WhenAnyValue(
                    x => x.SelectedClipboardData,
                    text => text.Split('\n')[0].Trim())
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedClipboardDataTitle);
        }
    }
}