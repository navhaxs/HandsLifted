using HandsLiftedApp.Models;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.PropertyGridControl;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;
using System.Reactive.Linq;
using static HandsLiftedApp.Importer.PowerPoint.Main;
using HandsLiftedApp.Data.Models;
using System.Diagnostics;
using DynamicData;

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
				this.RaisePropertyChanged(nameof(CurrentTime));
			}
		}

		public PlaylistState _playlistState;

		public PlaylistState PlaylistState
		{
			get => _playlistState;
			set
			{
				this.RaiseAndSetIfChanged(ref _playlistState, value);
			}
		}

		public SlideStateBase SlidesSelectedItem
		{
			get => _playlistState?.SelectedItem?.SelectedItem;
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

		public void LoadDemoSchedule()
		{
			var playlist = TestPlaylistDataGenerator.Generate();

			PlaylistState = new PlaylistState(playlist);
		}
 		public MainWindowViewModel()
		{

			// The OpenFile command is bound to a button/menu item in the UI.
			AddPresentationCommand = ReactiveCommand.CreateFromTask(OpenPPTXFileAsync);
			AddSongCommand = ReactiveCommand.CreateFromTask(AddSongAsync);
			SaveServiceCommand = ReactiveCommand.Create(OnSaveService);
			LoadServiceCommand = ReactiveCommand.Create(OnLoadService);
			MoveUpItemCommand = ReactiveCommand.Create<object>(OnMoveUpItemCommand);
			MoveDownItemCommand = ReactiveCommand.Create<object>(OnMoveDownItemCommand);
			RemoveItemCommand = ReactiveCommand.Create<object>(OnRemoveItemCommand);

			// The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
			ShowOpenFileDialog = new Interaction<Unit, string?>();

			_ = Update(); // calling an async function we do not want to await

			this.WhenAnyValue(t => t.SlidesSelectedItem).Subscribe(s => {
				this.RaisePropertyChanged("Text");
				this.RaisePropertyChanged("SlidesSelectedItem");

				//var nextSlideState = Slides != null && SlidesSelectedIndex + 1 < Slides.Count ? Slides[SlidesSelectedIndex + 1] : null;
				//if (nextSlideState != null)
				//	NextSlide = nextSlideState;
			});

			MessageBus.Current.Listen<Class1>()
				.Subscribe(x => {
					var lastSelectedIndex = _playlistState.SelectedIndex;
					
					// update the selected item in the playlist
					_playlistState.SelectedIndex = x.SourceItemStateIndex;

                    // notify the last deselected item in the playlist
                    if (lastSelectedIndex > -1 && x.SourceItemStateIndex != lastSelectedIndex && _playlistState.ItemStates[lastSelectedIndex] != null)
                    {
						_playlistState.ItemStates[lastSelectedIndex].SelectedIndex = -1;
					}
				});
			

			LoadDemoSchedule();

			this.WhenAnyValue(t => t._playlistState.SelectedItem).Subscribe(s =>
			{
				this.WhenAnyValue(tt => tt._playlistState.SelectedItem.SelectedItem).Subscribe(ss =>
				{
                    this.RaisePropertyChanged("SlidesSelectedItem");
				});
			});
		}

		//SlideState convertDataToState(Slide slide)
		//{
		//	switch (slide)
		//	{
		//		case SongSlide d:
		//			return new SongSlideState(d);
		//		case VideoSlide d:
		//			return new VideoSlideState(d);
		//		case ImageSlide d:
		//			return new ImageSlideState(d);
		//		default:
		//			throw new Exception("error");
		//			break;
		//	}
		//}

		public void OnProjectorClickCommand()
		{
			ProjectorWindow p = new ProjectorWindow();
            p.DataContext = this;
            p.Show();
		}
		
		public void OnDebugClickCommand()
		{
			ObjectInspectorWindow p = new ObjectInspectorWindow() { DataContext = this };
            p.Show();
		}
		
		public void OnNextSlideClickCommand()
		{
			//SlidesSelectedIndex += 1;
		}
		
		public void OnPrevSlideClickCommand()
		{
			//SlidesSelectedIndex -= 1;
		}

		public ReactiveCommand<Unit, Unit> AddPresentationCommand { get; }
		public ReactiveCommand<Unit, Unit> AddSongCommand { get; }
		public ReactiveCommand<Unit, Unit> SaveServiceCommand { get; }
		public ReactiveCommand<Unit, Unit> LoadServiceCommand { get; }
		public ReactiveCommand<object, Unit> MoveUpItemCommand { get; }
		public ReactiveCommand<object, Unit> MoveDownItemCommand { get; }
		public ReactiveCommand<object, Unit> RemoveItemCommand { get; }

		public Interaction<Unit, string?> ShowOpenFileDialog { get; }
		private async Task OpenPPTXFileAsync()
		{
            try
            {
				var fileName = await ShowOpenFileDialog.Handle(Unit.Default);

				if (fileName != null && fileName is string)
				{
                    ImportStats result = await PlaylistUtils.AddPowerPointToPlaylist((string) fileName);
                    Data.Models.Items.SlidesGroup slidesGroup = PlaylistUtils.CreateSlidesGroup(result.Task.OutputDirectory);
					slidesGroup.Title = result.FileName;
					PlaylistState.Playlist.Items.Add(slidesGroup);
                }

			}
			catch (Exception e)
            {
				System.Diagnostics.Debug.Print(e.Message);
            }
		}

		public Interaction<Unit, string?> ShowSongOpenFileDialog { get; }

		private async Task AddSongAsync()
        {
			try
			{
				var fileName = await ShowOpenFileDialog.Handle(Unit.Default);

				if (fileName != null && fileName is string)
				{
					//var x = PlaylistUtils.CreateSong();
					var songItem = SongImporter.ImportSongFromTxt((string) fileName);
					PlaylistState.Playlist.Items.Add(songItem);
				}

			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.Print(e.Message);
			}
		}

		const string TEST_SERVICE_FILE_PATH = @"C:\VisionScreens\service.xml";
		void OnSaveService()
		{
			XmlSerialization.WriteToXmlFile<Playlist>(TEST_SERVICE_FILE_PATH, PlaylistState.Playlist);
		}
		void OnLoadService()
		{
			PlaylistState.Playlist = XmlSerialization.ReadFromXmlFile<Playlist>(TEST_SERVICE_FILE_PATH);
		}
		void OnMoveUpItemCommand(object? itemState)
        {
			// get the index of itemState
			// move the source "item" position (in the "item source")
			// update the rest

			int v = PlaylistState.ItemStates.IndexOf(itemState);

			if (v > 0)
            {
                PlaylistState.Playlist.Items.Move(v, v - 1);
            }
			Debug.Print("up");
        }
		void OnMoveDownItemCommand(object? itemState)
        {
			int v = PlaylistState.ItemStates.IndexOf(itemState);

			if (v + 1 < PlaylistState.Playlist.Items.Count)
			{
				PlaylistState.Playlist.Items.Move(v, v + 1);
			}

			Debug.Print("down");
        }
		
		void OnRemoveItemCommand(object? itemState)
        {
			// TODO confirm dialog?
			int v = PlaylistState.ItemStates.IndexOf(itemState);
			PlaylistState.Playlist.Items.RemoveAt(v);
			Debug.Print("remove");
        }

	}
}
