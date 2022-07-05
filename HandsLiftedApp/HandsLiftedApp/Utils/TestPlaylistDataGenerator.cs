using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
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
        public static Playlist Generate()
        {
			Playlist playlist = new Playlist();
			var rnd = new Random();
			var songs = Directory.GetFiles(@"C:\VisionScreens\TestSongs", "*.*", SearchOption.AllDirectories)
				.Where(s => s.ToLower().EndsWith(".txt"))
				.OrderBy(x => rnd.Next()).Take(4)
				;
			foreach (var f in songs)
			{
				playlist.Items.Add(SongImporter.ImportSongFromTxt(f));
			}

            SlidesGroup slidesGroup = new SlidesGroup();
			try
			{
				var images = Directory.GetFiles(@"C:\VisionScreens\TestImages", "*.*", SearchOption.AllDirectories)
								.Where(s => s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".mp4"));
				foreach (var f in images)
				{
					if (f.EndsWith(".mp4"))
					{
						slidesGroup._Slides.Add(new VideoSlide(f));
						//playlist.Items.Add(new SlidesGroup() { _Slides = new List<Slide> { new VideoSlide(f) } });
					}
					else
					{
						slidesGroup._Slides.Add(new ImageSlide(f));
						//playlist.Items.Add(new SlidesGroup() { _Slides = new List<Slide> { new ImageSlide(f) } });
					}
				}
			}
			catch { }
			playlist.Items.Add(slidesGroup);

			return playlist;
		}
	}
}
