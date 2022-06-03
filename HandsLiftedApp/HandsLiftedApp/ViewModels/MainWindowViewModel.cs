using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.Render;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HandsLiftedApp.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{

		public DateTime CurrentTime
		{
			get => DateTime.Now;
		}
		private async Task Update()
		{
			while (true)
			{
				await Task.Delay(100);
				OnPropertyChanged(nameof(CurrentTime));
			}
		}

		private ObservableCollection<SlideState>? _slides = new ObservableCollection<SlideState>();
		public ObservableCollection<SlideState>? Slides
		{
			get => _slides;
			private set => this.RaiseAndSetIfChanged(ref _slides, value);
		}

		public PlaylistState _playlistState;

		public PlaylistState PlaylistState
		{
			get => _playlistState;
			set
			{
				this.RaiseAndSetIfChanged(ref _playlistState, value);
				OnPropertyChanged();
			}
		}

		public int _slidesSelectedIndex;
		public int SlidesSelectedIndex
		{
			get => _slidesSelectedIndex;
			set
			{
				this.RaiseAndSetIfChanged(ref _slidesSelectedIndex, value);
				OnPropertyChanged();
			}
		}

		SlideState _slidesSelectedItem;

		//public SlideState SlidesSelectedItem
		//{
		//	get => _slidesSelectedItem;
		//	set {
		//		this.RaiseAndSetIfChanged(ref _slidesSelectedItem, value);
		//		this.RaisePropertyChanged("Text");
		//		OnPropertyChanged("Text");

		//		OnPropertyChanged("NextSlide");
		//		OnPropertyChanged("SlidesNextItem");
		//	}
		//}

		public SlideState SlidesSelectedItem
		{
			get => _playlistState?.SelectedItem?.SelectedItem;
		}
	

        SlideState? _nextSlide;

		public SlideState? NextSlide
		{
			get => _nextSlide;
			set {
				this.RaiseAndSetIfChanged(ref _nextSlide, value);
			}
		}

		public SlideState SlidesNextItem
		{
			get => _slidesSelectedItem;
		}

		public string Text
		{
			get
			{
				return SlidesSelectedItem is SongSlideState ? ((SongSlideState)SlidesSelectedItem).Data.SlideText : "No slide selected yet";
			}
		}

		public OverlayState OverlayState { get; set; }

		public Boolean IsFrozen { get; set; }

		public SongSlide Slide { get; set; } = new SongSlide() { Text = "Path=slide from View Model" };

		//public String Slide { get; set; } = "hello this is my slide text set from the ViewModel";

		public void LoadDemoSchedule()
		{
			// = await Task.Run(() =>
			//{

			var playlist = TestPlaylistDataGenerator.Generate();

			PlaylistState = new PlaylistState(playlist);

				var m = new ObservableCollection<Slide>();

				var xxx = m.Select(s => convertDataToState(s)).ToList();
				Slides =  new ObservableCollection<SlideState>(xxx);

			//});
		}
 	
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		// CONSTRUCTOR
		public MainWindowViewModel()
		{

			_ = Update(); // calling an async function we do not want to await

			this.WhenAnyValue(t => t.SlidesSelectedItem).Subscribe(s => {
				this.RaisePropertyChanged("Text");
				OnPropertyChanged("Text");
				this.RaisePropertyChanged("SlidesSelectedItem");
				OnPropertyChanged("SlidesSelectedItem");

				var nextSlideState = Slides != null && SlidesSelectedIndex + 1 < Slides.Count ? Slides[SlidesSelectedIndex + 1] : null;
				if (nextSlideState != null)
					NextSlide = nextSlideState;
			});

			MessageBus.Current.Listen<Class1>()
				.Subscribe(x => {
					_playlistState.SelectedIndex = x.SourceItemStateIndex;
				
				});

			

			LoadDemoSchedule();

			this.WhenAnyValue(t => t._playlistState.SelectedItem).Subscribe(s =>
			{
				this.WhenAnyValue(tt => tt._playlistState.SelectedItem.SelectedItem).Subscribe(ss =>
				{
                    this.RaisePropertyChanged("SlidesSelectedItem");
                    OnPropertyChanged("SlidesSelectedItem");
				});
			});
		}

		SlideState convertDataToState(Slide slide)
		{
			switch (slide)
			{
				case SongSlide d:
					return new SongSlideState(d);
				case VideoSlide d:
					return new VideoSlideState(d);
				case ImageSlide d:
					return new ImageSlideState(d);
				default:
					throw new Exception("error");
					break;
			}
		}

		public void OnProjectorClickCommand()
		{
			ProjectorWindow p = new ProjectorWindow();
            p.DataContext = this;
            p.Show();
		}
		
		public void OnNextSlideClickCommand()
		{
			SlidesSelectedIndex += 1;
		}
		
		public void OnPrevSlideClickCommand()
		{
			SlidesSelectedIndex -= 1;
		}
	}
}
