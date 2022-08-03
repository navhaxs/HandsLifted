using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
using ReactiveUI;
using System;
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

            song = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>()
            {
                Title = "Rock Of Ages",
                Copyright = @"“Hallelujah” words and music by John Doe
© 2018 Good Music Co.
Used by permission. CCLI Licence #12345"
            };

            var v1Guid = Guid.NewGuid();
            song.Stanzas.Add(new SongStanza(v1Guid, "Verse 1", @"In the darkness we were waiting
Without hope without light
Till from Heaven You came running
There was mercy in Your eyes

To fulfil the law and prophets
To a virgin came the Word
From a throne of endless glory
To a cradle in the dirt"));

            var cGuid = Guid.NewGuid();
            song.Stanzas.Add(new SongStanza(cGuid, "Chorus", @"Praise the Father
Praise the Son
Praise the Spirit three in one

God of Glory
Majesty
Praise forever to the King of kings"));

            var v2Guid = Guid.NewGuid();
            song.Stanzas.Add(new SongStanza(v2Guid, "Verse 2", @"To reveal the kingdom coming
And to reconcile the lost
To redeem the whole creation
You did not despise the cross

For even in Your suffering
You saw to the other side
Knowing this was our salvation
Jesus for our sake You died"));

            var v3Guid = Guid.NewGuid();
            song.Stanzas.Add(new SongStanza(v3Guid, "Verse 3", @"And the morning that You rose
All of heaven held its breath
Till that stone was moved for good
For the Lamb had conquered death

And the dead rose from their tombs
And the angels stood in awe
For the souls of all who'd come
To the Father are restored"));

            var v4Guid = Guid.NewGuid();
            song.Stanzas.Add(new SongStanza(v4Guid, "Verse 4", @"And the Church of Christ was born
Then the Spirit lit the flame
Now this Gospel truth of old
Shall not kneel shall not faint

By His blood and in His Name
In His freedom I am free
For the love of Jesus Christ
Who has resurrected me"));

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
