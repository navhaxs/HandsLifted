using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.ItemState;
using Serilog;
using System;
using System.IO;
using System.Linq;

namespace HandsLiftedApp.Utils
{
    internal static class TestPlaylistDataGenerator
    {
        public static Playlist<PlaylistStateImpl, ItemStateImpl> Generate()
        {
            Playlist<PlaylistStateImpl, ItemStateImpl> playlist = new Playlist<PlaylistStateImpl, ItemStateImpl>();

            playlist.Meta.Add("Title", "My Generated Playlist");
            playlist.Meta.Add("Date", DateTimeOffset.Now);

            //return playlist;
            var rnd = new Random();


            if (Directory.Exists(@"C:\VisionScreens\Announcements"))
            {
                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>();
                try
                {
                    //var images = Directory.GetFiles(@"C:\VisionScreens\Announcements", "*.*", SearchOption.AllDirectories)
                    //                .Where(s => s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".mp4"))
                    //                .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));

                    //ObservableCollection<Slide> slidesGroupItems = new ObservableCollection<Slide>();
                    //int i = 0;
                    //foreach (var f in images)
                    //{
                    //    if (f.EndsWith(".mp4"))
                    //    {
                    //        slidesGroupItems.Add(new VideoSlide<VideoSlideStateImpl>(f) { Index = i });
                    //    }
                    //    else
                    //    {
                    //        slidesGroupItems.Add(new ImageSlide<ImageSlideStateImpl>(f) { Index = i });
                    //    }
                    //    i++;
                    //}
                    //slidesGroup._Slides = slidesGroupItems;
                    slidesGroup = PlaylistUtils.CreateSlidesGroup(@"C:\VisionScreens\Announcements");
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
                slidesGroup.Title = "Announcements";

                playlist.Items.Add(slidesGroup);
            }


            if (Directory.Exists(@"C:\VisionScreens\Songs"))
            {
                var songs = Directory.GetFiles(@"C:\VisionScreens\Songs", "*.*", SearchOption.AllDirectories)
                .Where(s => s.ToLower().EndsWith(".txt"))
                .OrderBy(x => rnd.Next()).Take(4)
                ;
                foreach (var f in songs)
                {
                    playlist.Items.Add(SongImporter.createSongItemFromTxtFile(f));
                }


            }

            return playlist;
        }
    }
}
