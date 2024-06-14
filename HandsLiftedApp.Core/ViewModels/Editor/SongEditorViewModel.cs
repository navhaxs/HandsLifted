using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    public class SongEditorViewModel : ViewModelBase
    {
        private bool _lyricEntryMode = false;
        public bool LyricEntryMode { get => _lyricEntryMode; set => this.RaiseAndSetIfChanged(ref _lyricEntryMode, value); }
        
        // public event EventHandler SongDataUpdated;

        private SongItemInstance _song;

        public SongItemInstance Song
        {
            get => _song;
            set
            {
                if (_song != null)
                    _song.PropertyChanged -= _song_PropertyChanged;

                this.RaiseAndSetIfChanged(ref _song, value);
                _song.PropertyChanged += _song_PropertyChanged;

                if (Globals.MainViewModel != null)
                {
                    this.RaiseAndSetIfChanged(ref _selectedSlideTheme, Globals.MainViewModel.Playlist.Designs.FirstOrDefault(d => d.Id == _song.Design, null), nameof(SelectedSlideTheme));
                }

            }
        }

        private void _song_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // OnSongDataUpdateCommand();
            FreeTextEntryField = SongImporter.songItemToFreeText(Song);
        }

        public SongEditorViewModel(SongItemInstance song, PlaylistInstance playlistInstance)
        {
            OnClickCommand = ReactiveCommand.Create(RunTheThing);
            // SongDataUpdateCommand = ReactiveCommand.Create(OnSongDataUpdateCommand);

            // Playlist = (Design.IsDesignMode) ? new PlaylistInstance() : Globals.MainViewModel?.Playlist;
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
        }

        public ReactiveCommand<Unit, Unit> OnClickCommand { get; }

        void RunTheThing()
        {
            Song.Stanzas.Add(new SongStanza(Guid.NewGuid(), "", ""));
        }

            // MessageBus.Current.SendMessage(new PlaylistInstance.UpdateEditedItemMessage { Item = Song });

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