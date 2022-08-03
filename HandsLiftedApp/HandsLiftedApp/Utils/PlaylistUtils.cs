﻿using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static HandsLiftedApp.Importer.PowerPoint.Main;

namespace HandsLiftedApp.Utils
{
    internal static class PlaylistUtils
    {

        public async static Task<ImportStats?> AddPowerPointToPlaylist(ImportTask importTask)
        {
            ImportStats? value = null;
            Exception? threadEx = null;

            await Task.Factory.StartNew(() =>
            {
                var progress = new Progress<ImportStats>();
                //progress.ProgressChanged += Progress_ProgressChanged;
                var outDir = GetTempDirPath();

                Thread staThread = new Thread(
                    delegate ()
                    {
                        try
                        {
                            value = RunPowerPointImportTask(progress, importTask);

                        }
                        catch (Exception ex)
                        {
                            threadEx = ex;
                        }
                    });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
            });

            return value;
        }
        //private void Progress_ProgressChanged(object? sender, PowerpointImportProgress e)
        //{
        //    Debug.Print(e.ToString());
        //}

        public static SlidesGroup<ItemStateImpl> CreateSlidesGroup(string directory)
        {
            SlidesGroup<ItemStateImpl> slidesGroup = new SlidesGroup<ItemStateImpl>();
            UpdateSlidesGroup(slidesGroup, directory);
            return slidesGroup;
        }

        public static SlidesGroup<ItemStateImpl> UpdateSlidesGroup(SlidesGroup<ItemStateImpl> slidesGroup, string directory)
        {
            try
            {
                var images = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                                .Where(s => s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".mp4"));
                foreach (var f in images)
                {
                    if (f.EndsWith(".mp4"))
                    {
                        slidesGroup._Slides.Add(new VideoSlide<VideoSlideStateImpl>(f));
                    }
                    else
                    {
                        slidesGroup._Slides.Add(new ImageSlide<ImageSlideStateImpl>(f));
                    }
                }
            }
            catch { }

            return slidesGroup;
        }


        public static SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> CreateSong()
        {
            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> song = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>()
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

            return song;
        }


    }
}
