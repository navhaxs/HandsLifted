using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.ViewModels;
using HandsLiftedApp.ViewModels.Editor;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Reactive;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    public class SongEditorViewModel : ViewModelBase
    {
        public event EventHandler SongDataUpdated;

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
                //OnPropertyChanged();
            }
        }

        private void _song_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // OnSongDataUpdateCommand();
            FreeTextEntryField = SongImporter.songItemToFreeText(Song);
        }

        public SongEditorViewModel()
        {
            OnClickCommand = ReactiveCommand.Create(RunTheThing);
            SongDataUpdateCommand = ReactiveCommand.Create(OnSongDataUpdateCommand);

            Song = new ExampleSongItemInstance();
        }

        public ReactiveCommand<Unit, Unit> OnClickCommand { get; }

        void RunTheThing()
        {
            Song.Stanzas.Add(new SongStanza(Guid.NewGuid(), "", ""));
        }

        public ReactiveCommand<Unit, Unit> SongDataUpdateCommand { get; }

        void OnSongDataUpdateCommand()
        {
            // TODO run only on 'save'
            // TODO run only on 'save'
            // TODO run only on 'save'
            // TODO run only on 'save'
            // TODO run only on 'save'
            // TODO run only on 'save'
            // TODO debounce this
            // TODO debounce this
            // TODO debounce this
            // TODO debounce this
            // TODO debounce this
            // TODO debounce this
            // TODO debounce this
            MessageBus.Current.SendMessage(new PlaylistInstance.UpdateEditedItemMessage { Item = Song });
            SongDataUpdated?.Invoke(this, new ThresholdReachedEventArgs() { UpdatedSongData = Song });
        }

        public class ThresholdReachedEventArgs : EventArgs
        {
            public SongItemInstance UpdatedSongData { get; set; }
        }

        private string _freeTextEntryField;
        public string FreeTextEntryField
        {
            get => _freeTextEntryField;
            set => this.RaiseAndSetIfChanged(ref _freeTextEntryField, value);
        }
        
    }
}