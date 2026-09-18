using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace HandsLiftedApp.Importer.OnlineSongLyrics
{
    public class BrowserWindowViewModel : ReactiveObject
    {
        private string _selectedClipboardData = (Design.IsDesignMode) ? BrowserWindow.TEST_DATA : string.Empty;

        public string SelectedClipboardData { get => _selectedClipboardData; set => this.RaiseAndSetIfChanged(ref _selectedClipboardData, value); }

        private string _currentUrl = string.Empty;

        public string CurrentUrl { get => _currentUrl; set => this.RaiseAndSetIfChanged(ref _currentUrl, value); }

        private ObservableAsPropertyHelper<string> _selectedClipboardDataTitle;

        public string SelectedClipboardDataTitle
        {
            get => _selectedClipboardDataTitle?.Value;
        }

        private ObservableAsPropertyHelper<bool> _isSongPage;

        public bool IsSongPage => _isSongPage?.Value ?? false;

        public BrowserWindowViewModel()
        {
            if (Design.IsDesignMode)
            {
                return;
            }
            
            _selectedClipboardDataTitle = this.WhenAnyValue(
                    x => x.SelectedClipboardData,
                    text => text.Split('\n')[0].Trim())
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.SelectedClipboardDataTitle);

            _isSongPage = this.WhenAnyValue(
                    x => x.CurrentUrl,
                    url => url.StartsWith("https://songselect.ccli.com/songs/", StringComparison.OrdinalIgnoreCase))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.IsSongPage);
        }
    }
}