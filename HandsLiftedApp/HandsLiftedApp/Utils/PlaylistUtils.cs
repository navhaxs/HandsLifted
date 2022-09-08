using Avalonia.Threading;
using DynamicData;
using HandsLiftedApp.Comparer;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.PDF;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static HandsLiftedApp.Importer.PowerPoint.Main;

namespace HandsLiftedApp.Utils
{
    internal static class PlaylistUtils
    {
        public async static Task<ImportStats?> AddPowerPointToPlaylist(ImportTask importTaskAction, Action<ImportStats> onProgressUpdate)
        {
            ImportStats? value = null;
            Exception? threadEx = null;

            await Task.Factory.StartNew(() =>
            {
                var progress = new Progress<ImportStats>();
                progress.ProgressChanged += (object? sender, ImportStats e) =>
                    {
                        Debug.Print(e.JobPercentage.ToString());
                        onProgressUpdate(e);
                    };
                var outDir = GetTempDirPath();

                Thread staThread = new Thread(
                    delegate ()
                    {
                        try
                        {
                            value = RunPowerPointImportTask(progress, importTaskAction);

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

        public async static Task<Importer.GoogleSlides.Main.ImportStats?> AddGoogleSlidesToPlaylist(Importer.GoogleSlides.Main.ImportTask importTaskAction, Action<Importer.GoogleSlides.Main.ImportStats> onProgressUpdate)
        {
            Importer.GoogleSlides.Main.ImportStats? value = null;
            Exception? threadEx = null;

            await Task.Factory.StartNew(() =>
            {
                var progress = new Progress<Importer.GoogleSlides.Main.ImportStats>();
                progress.ProgressChanged += (object? sender, Importer.GoogleSlides.Main.ImportStats e) =>
                {
                    Debug.Print(e.JobPercentage.ToString());
                    onProgressUpdate(e);
                };
                var outDir = GetTempDirPath();

                Thread staThread = new Thread(
                    delegate ()
                    {
                        try
                        {
                            value = Importer.GoogleSlides.Main.RunGoogleSlidesImportTask(progress, importTaskAction);
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

        private static void Progress_ProgressChanged1(object? sender, ImportStats e)
        {
            Debug.Print(e.JobPercentage.ToString());
        }
        public static SlidesGroupItem<ItemStateImpl> CreateSlidesGroup(string directory)
        {
            SlidesGroupItem<ItemStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl>();
            UpdateSlidesGroup(ref slidesGroup, directory);
            return slidesGroup;
        }

        public static void UpdateSlidesGroup<X>(ref X slidesGroup, string directory) where X : SlidesGroupItem<ItemStateImpl>
        {
            int i = 0;
            var prevCount = slidesGroup._Slides.Count;


            slidesGroup.State.LockSelectionIndex = true;
            var lastSelectedIndex = slidesGroup.State.SelectedIndex;

            try
            {

                var images = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                                .Where(s => s.ToLower().EndsWith(".bmp") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".mp4"))
                        .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));

                foreach (var f in images)
                {

                    Slide x = (f.EndsWith(".mp4")) ? new VideoSlide<VideoSlideStateImpl>(f) { Index = i } : new ImageSlide<ImageSlideStateImpl>(f) { Index = i };

                    if (slidesGroup._Slides.ElementAtOrDefault(i) == null)
                    {
                        slidesGroup._Slides.Add(x);
                    }
                    else
                    {
                        slidesGroup._Slides[i] = x;
                    }

                    i++;
                }


                while (i < prevCount)
                {
                    // keep removing last slide
                    slidesGroup._Slides.RemoveAt(slidesGroup._Slides.Count - 1);
                    i++;
                }


                Thread.Sleep(500);

                slidesGroup.State.LockSelectionIndex = false;
                //slidesGroup.State.SelectedIndex = lastSelectedIndex;
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }
            //return slidesGroup;
        }

        public static SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> CreateSong()
        {
            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> song = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>()
            {
                Title = "Rock Of Ages, A Really Long Title For An Item",
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
