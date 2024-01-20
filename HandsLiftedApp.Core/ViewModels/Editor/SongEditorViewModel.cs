using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.ViewModels;
using HandsLiftedApp.ViewModels.Editor;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Reactive;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    public class SongEditorViewModel : ViewModelBase
    {
        public event EventHandler SongDataUpdated;

        private SongItem? _song;
        public SongItem? song
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

            song = new ExampleSongItem();
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
            public SongItem UpdatedSongData { get; set; }
        }
    }
}
