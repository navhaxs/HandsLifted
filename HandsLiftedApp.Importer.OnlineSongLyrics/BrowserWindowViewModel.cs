using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace HandsLiftedApp.Importer.OnlineSongLyrics
{
    public enum BrowserState
    {
        Start,
        Search,
        ReadyToPick,
        SelectedSong
    }

    public class BrowserWindowViewModel : ReactiveObject
    {
        private string _selectedClipboardData = (Design.IsDesignMode) ? BrowserWindow.TEST_DATA : string.Empty;

        public string SelectedClipboardData { get => _selectedClipboardData; set => this.RaiseAndSetIfChanged(ref _selectedClipboardData, value); }

        private string _currentUrl = string.Empty;

        public string CurrentUrl { get => _currentUrl; set => this.RaiseAndSetIfChanged(ref _currentUrl, value); }

        private bool _hasStarted;

        public bool HasStarted { get => _hasStarted; set => this.RaiseAndSetIfChanged(ref _hasStarted, value); }

        private ObservableAsPropertyHelper<string> _selectedClipboardDataTitle;

        public string SelectedClipboardDataTitle
        {
            get => _selectedClipboardDataTitle?.Value;
        }

        private ObservableAsPropertyHelper<bool> _isSongPage;

        public bool IsSongPage => _isSongPage?.Value ?? false;

        private ObservableAsPropertyHelper<BrowserState> _state;

        public BrowserState State => _state?.Value ?? BrowserState.Start;

        private ObservableAsPropertyHelper<bool> _isStartState;
        public bool IsStartState => _isStartState?.Value ?? true;

        private ObservableAsPropertyHelper<bool> _isSearchState;
        public bool IsSearchState => _isSearchState?.Value ?? false;

        private ObservableAsPropertyHelper<bool> _isReadyToPickState;
        public bool IsReadyToPickState => _isReadyToPickState?.Value ?? false;

        private ObservableAsPropertyHelper<bool> _isSelectedSongState;
        public bool IsSelectedSongState => _isSelectedSongState?.Value ?? false;

        public ReactiveCommand<Unit, Unit> ChooseAnotherSongCommand { get; }

        public BrowserWindowViewModel()
        {
            ChooseAnotherSongCommand = ReactiveCommand.Create(() => { SelectedClipboardData = string.Empty; });
            
            _state = this.WhenAnyValue(
                    x => x.HasStarted,
                    x => x.IsSongPage,
                    x => x.SelectedClipboardData,
                    (hasStarted, isSongPage, clipboardData) =>
                    {
                        if (Design.IsDesignMode) return BrowserState.Search;
                        if (!hasStarted) return BrowserState.Start;
                        if (!string.IsNullOrEmpty(clipboardData)) return BrowserState.SelectedSong;
                        return isSongPage ? BrowserState.ReadyToPick : BrowserState.Search;
                    })
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.State);

            _isStartState = this.WhenAnyValue(x => x.State, s => s == BrowserState.Start)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.IsStartState);

            _isSearchState = this.WhenAnyValue(x => x.State, s => s == BrowserState.Search)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.IsSearchState);

            _isReadyToPickState = this.WhenAnyValue(x => x.State, s => s == BrowserState.ReadyToPick)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.IsReadyToPickState);

            _isSelectedSongState = this.WhenAnyValue(x => x.State, s => s == BrowserState.SelectedSong)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.IsSelectedSongState);

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
