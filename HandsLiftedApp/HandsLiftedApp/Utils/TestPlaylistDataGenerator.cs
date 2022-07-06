using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Utils
{
    internal static class TestPlaylistDataGenerator
    {
        public static Playlist<PlaylistStateImpl, ItemStateImpl> Generate()
        {
			Playlist<PlaylistStateImpl, ItemStateImpl> playlist = new Playlist<PlaylistStateImpl, ItemStateImpl>();
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
								.Where(s => s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".mp4"));
				foreach (var f in images)
				{
					if (f.EndsWith(".mp4"))
					{
						slidesGroup._Slides.Add(new VideoSlide<VideoSlideStateImpl>(f));
						//playlist.Items.Add(new SlidesGroup() { _Slides = new List<Slide> { new VideoSlide(f) } });
					}
					else
					{
						slidesGroup._Slides.Add(new ImageSlide<ImageSlideStateImpl>(f));
						//playlist.Items.Add(new SlidesGroup() { _Slides = new List<Slide> { new ImageSlide(f) } });
					}
				}
			}
			catch { }
			playlist.Items.Add(slidesGroup);

			slidesGroup.Title = "Announcements";

			// is this okay?
			foreach (var (item, index) in playlist.Items.WithIndex())
			{
				item.State.ItemIndex = index;
            }

			return playlist;
		}
	}
}
