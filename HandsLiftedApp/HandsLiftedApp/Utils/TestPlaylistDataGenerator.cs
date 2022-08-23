using HandsLiftedApp.Comparer;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
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
            return playlist;
            var rnd = new Random();
            var songs = Directory.GetFiles(@"C:\VisionScreens\TestSongs", "*.*", SearchOption.AllDirectories)
                .Where(s => s.ToLower().EndsWith(".txt"))
                .OrderBy(x => rnd.Next()).Take(4)
                ;
            foreach (var f in songs)
            {
                playlist.Items.Add(SongImporter.ImportSongFromTxt(f));
            }

            SlidesGroup<ItemStateImpl> slidesGroup = new SlidesGroup<ItemStateImpl>();
            try
            {
                var images = Directory.GetFiles(@"C:\VisionScreens\TestImages", "*.*", SearchOption.AllDirectories)
                                .Where(s => s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".mp4"))
                                .OrderBy(x => x, new NaturalSortStringComparer(StringComparison.Ordinal));
                int i = 0;
                foreach (var f in images)
                {
                    if (f.EndsWith(".mp4"))
                    {
                        slidesGroup._Slides.Add(new VideoSlide<VideoSlideStateImpl>(f) { Index = i });
                    }
                    else
                    {
                        slidesGroup._Slides.Add(new ImageSlide<ImageSlideStateImpl>(f) { Index = i });
                    }
                    i++;
                }
            }
            catch { }
            playlist.Items.Add(slidesGroup);

            slidesGroup.Title = "Announcements";

            return playlist;
        }
    }
}
