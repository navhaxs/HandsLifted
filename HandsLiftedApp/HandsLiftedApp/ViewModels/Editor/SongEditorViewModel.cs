using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;

namespace HandsLiftedApp.ViewModels.Editor
{
    public class SongEditorViewModel : ViewModelBase
    {
        public event EventHandler SongDataUpdated;

        private SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>? _song;
        public SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>? song
        {
            get => _song;
            set
            {
                this.RaiseAndSetIfChanged(ref _song, value);

                _song.PropertyChanged += _song_PropertyChanged;
                //OnPropertyChanged();
            }
        }

        private void _song_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnSongDataUpdateCommand();
        }

        public SongEditorViewModel()
        {
            OnClickCommand = ReactiveCommand.Create(RunTheThing);
            SongDataUpdateCommand = ReactiveCommand.Create(OnSongDataUpdateCommand);

            song = new ExampleSongViewModel();
        }

        public ReactiveCommand<Unit, Unit> OnClickCommand { get; }

        void RunTheThing()
        {
            song.Stanzas.Add(new SongStanza(Guid.NewGuid(), "", ""));
        }

        public ReactiveCommand<Unit, Unit> SongDataUpdateCommand { get; }

        void OnSongDataUpdateCommand()
        {
            SongDataUpdated?.Invoke(this, new ThresholdReachedEventArgs() { UpdatedSongData = song });
        }

        public class ThresholdReachedEventArgs : EventArgs
        {
            public SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> UpdatedSongData { get; set; }
        }
    }
}
