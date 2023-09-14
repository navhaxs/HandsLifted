using DynamicData;
using HandsLiftedApp.Comparer;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
using System.Collections.Generic;
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
        public static readonly string[] SUPPORTED_SONG = { "txt", "xml" };
        public static readonly string[] SUPPORTED_POWERPOINT = { "ppt", "pptx", "odp" };
        public static readonly string[] SUPPORTED_VIDEO = { "mp4", "flv", "mov", "mkv", "avi", "wmv" };
        public static readonly string[] SUPPORTED_IMAGE = { "bmp", "png", "jpg", "jpeg" };

        public static void AddItemFromFile(ref Playlist<PlaylistStateImpl, ItemStateImpl> playlist, List<string> fileNames, int? insertAtIndex = null)
        {
            List<Item<ItemStateImpl>> addedItems = new List<Item<ItemStateImpl>>();

            foreach (string fileName in fileNames)
            {
                string ext = Path.GetExtension(fileName).ToLower();

                string extNoDot = Path.GetExtension(fileName).ToLower().Replace(".", "");

                if (SUPPORTED_SONG.Contains(extNoDot))
                {
                    var songItem = SongImporter.createSongItemFromTxtFile(fileName);

                    if (songItem != null)
                        addedItems.Add(songItem);
                }
                else if (SUPPORTED_POWERPOINT.Contains(extNoDot))
                {
                    PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl> powerPointSlidesGroupItem = createPresentationItem(fileName, playlist.State);

                    if (powerPointSlidesGroupItem != null)
                        addedItems.Add(powerPointSlidesGroupItem);
                }
                // TODO generate individual items, or a single group of items?
                else if (SUPPORTED_VIDEO.Contains(extNoDot) || SUPPORTED_IMAGE.Contains(extNoDot))
                {
                    SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroupItem = createMediaGroupItem(fileName);

                    if (slidesGroupItem != null)
                        addedItems.Add(slidesGroupItem);
                }
            }

            int idx = insertAtIndex ?? playlist.Items.Count - 1;

            playlist.Items.AddRange(addedItems, idx);

            // UI: navigate to the head of the newly-inserted items
            MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = idx + 1 });
        }

        private static SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> createMediaGroupItem(string fullPath)
        {
            DateTime now = DateTime.Now;
            string fileName = Path.GetFileName(fullPath);
            string folderName = Path.GetDirectoryName(fullPath);

            SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>() { Title = fileName };

            slidesGroup.Items.Add(PlaylistUtils.GenerateMediaContentSlide(fullPath));

            return slidesGroup;
        }
          private static PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl> createPresentationItem(string fullFilePath, PlaylistStateImpl state)
        {
            try
            {
                if (fullFilePath != null && fullFilePath is string)
                {

                    //DateTime now = DateTime.Now;
                    string fileName = Path.GetFileName(fullFilePath);

                    //string targetDirectory = Path.Join(state.PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                    //Directory.CreateDirectory(targetDirectory);

                    //PowerPointSlidesGroupItem<PowerPointSlidesGroupItemStateImpl> slidesGroup = new PowerPointSlidesGroupItem<PowerPointSlidesGroupItemStateImpl>() { Title = fileName };
                    PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl> slidesGroup = new PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl>() { Title = fileName, SourcePresentationFile = fullFilePath };

                    slidesGroup.SyncState.SyncCommand();

                    return slidesGroup;
                }
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }
            return null;
        }
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
        public static SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> CreateSlidesGroup(string directory)
        {
            SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>();
            UpdateSlidesGroup(ref slidesGroup, directory);
            return slidesGroup;
        }

        public static void UpdateSlidesGroup<X>(ref X slidesGroup, string directory) where X : SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>
        {
            int i = 0;
            var prevCount = slidesGroup.Items.Count;


            slidesGroup.State.LockSelectionIndex = true;
            var lastSelectedIndex = slidesGroup.State.SelectedSlideIndex;

            try
            {

                var images = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                                .Where(s => s.ToLower().EndsWith(".bmp") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".mp4"))
                        .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));

                foreach (var f in images)
                {

                    //Slide x = (f.EndsWith(".mp4")) ? new VideoSlide<VideoSlideStateImpl>(f) { Index = i } : new ImageSlide<ImageSlideStateImpl>(f) { Index = i };

                    var s = PlaylistUtils.GenerateMediaContentSlide(f);

                    if (slidesGroup.Items.ElementAtOrDefault(i) == null)
                    {
                        slidesGroup.Items.Add(s);
                    }
                    else
                    {
                        slidesGroup.Items[i] = s;
                    }

                    i++;
                }


                while (i < prevCount)
                {
                    // keep removing last slide
                    slidesGroup.Items.RemoveAt(slidesGroup.Items.Count - 1);
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

        public static Slide GenerateMediaContentSlide(string filename) //, int index)
        {
            string _filename = filename.ToLower();

            // TODO: make VideoSlide and ImageSlide both share common MediaSlide parent class
            if (SUPPORTED_VIDEO.Any(x => _filename.EndsWith(x)))
            {
                return new VideoSlide<VideoSlideStateImpl>(filename); // { Index = index };
            }

            return new ImageSlide<ImageSlideStateImpl>(filename); // { Index = index };
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

            song.ResetArrangement();

            return song;
        }


    }
}
