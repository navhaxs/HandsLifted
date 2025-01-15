using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    public class SongEditorViewModel : ViewModelBase
    {
        private SongItemInstance _song;
        private ObservableAsPropertyHelper<string> _songEditorWindowTitle;
        public string SongEditorWindowTitle => _songEditorWindowTitle.Value;

        public SongItemInstance Song
        {
            get => _song;
            set
            {
                if (_song != null)
                    _song.PropertyChanged -= _song_PropertyChanged;

                this.RaiseAndSetIfChanged(ref _song, value);
                _song.PropertyChanged += _song_PropertyChanged;

                if (Globals.Instance.MainViewModel != null)
                {
                    this.RaiseAndSetIfChanged(ref _selectedSlideTheme, Globals.Instance.MainViewModel.Playlist.Designs.FirstOrDefault(d => d.Id == _song.Design, null), nameof(SelectedSlideTheme));
                }

            }
        }

        #region "add new song"
        
        private int? _itemInsertIndex = null;
        /// <summary>
        /// null == editing an existing item
        /// </summary>
        public int? ItemInsertIndex
        {
            get => _itemInsertIndex; set => this.RaiseAndSetIfChanged(ref _itemInsertIndex, value);
        }
        
        private bool _itemInserted = false;
        public bool ItemInserted
        {
            get => _itemInserted; set => this.RaiseAndSetIfChanged(ref _itemInserted, value);
        }
        #endregion        

        private void _song_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // OnSongDataUpdateCommand();
            FreeTextEntryField = SongImporter.songItemToFreeText(Song);
        }

        public SongEditorViewModel(SongItemInstance song, PlaylistInstance playlistInstance)
        {
            OnClickCommand = ReactiveCommand.Create(RunTheThing);
            // SongDataUpdateCommand = ReactiveCommand.Create(OnSongDataUpdateCommand);

            // Playlist = (Design.IsDesignMode) ? new PlaylistInstance() : Globals.Instance.MainViewModel?.Playlist;
            Song = song;
            Playlist = playlistInstance; // song.ParentPlaylist;

            this.WhenAnyValue(x => x.SelectedSlideTheme)
                .Subscribe((BaseSlideTheme theme) =>
                {
                    if (theme != null)
                    {
                        Song.Design = theme.Id;
                    }
                });
            
            _songEditorWindowTitle = this.WhenAnyValue(x => x.ItemInsertIndex,
                (int? idx) => idx == null ? "Song Editor" : "Add new song")
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, x => x.SongEditorWindowTitle);
        }

        public ReactiveCommand<Unit, Unit> OnClickCommand { get; }

        void RunTheThing()
        {
            Song.Stanzas.Add(new SongStanza(Guid.NewGuid(), "", ""));
        }

        private string _freeTextEntryField = "";

        public string FreeTextEntryField
        {
            get => _freeTextEntryField;
            set => this.RaiseAndSetIfChanged(ref _freeTextEntryField, value);
        }

        private BaseSlideTheme? _selectedSlideTheme;

        public BaseSlideTheme? SelectedSlideTheme
        {
            get => _selectedSlideTheme;
            set => this.RaiseAndSetIfChanged(ref _selectedSlideTheme, value);
        }

        public PlaylistInstance Playlist { get; init; }
    }
}