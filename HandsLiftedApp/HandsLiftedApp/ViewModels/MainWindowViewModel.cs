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
using System.IO;
using Avalonia.Threading;

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

		public Playlist<PlaylistStateImpl, ItemStateImpl> _playlist;

		public Playlist<PlaylistStateImpl, ItemStateImpl> Playlist { get => _playlist; set => this.RaiseAndSetIfChanged(ref _playlist, value); }

		// TODO
		// TODO
		// TODO
		// TODO
		// TODO
		//public SlideStateBase SlidesSelectedItem
		//{
		//	get => _playlist?.State?.SelectedItem?.SelectedItem;
		//}
	
		//public string Text
		//{
		//	get
		//	{
		//		return SlidesSelectedItem is SongSlideState ? ((SongSlideState)SlidesSelectedItem).Data.SlideText : "No slide selected yet";
		//	}
		//}

		public OverlayState OverlayState { get; set; }

		public Boolean IsFrozen { get; set; }

		public void LoadDemoSchedule()
		{
			Playlist = TestPlaylistDataGenerator.Generate();
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

            // TODO
            // TODO
            // TODO
            // TODO
            //this.WhenAnyValue(t => t.SlidesSelectedItem).Subscribe(s => {
            //	this.RaisePropertyChanged("Text");
            //	this.RaisePropertyChanged("SlidesSelectedItem");

            //	//var nextSlideState = Slides != null && SlidesSelectedIndex + 1 < Slides.Count ? Slides[SlidesSelectedIndex + 1] : null;
            //	//if (nextSlideState != null)
            //	//	NextSlide = nextSlideState;
            //});

            LoadDemoSchedule();

			//this.WhenAnyValue(t => t._playlist.State.SelectedItem).Subscribe(s =>
			//{
			//	this.WhenAnyValue(tt => tt._playlist.State.SelectedItem.SelectedItem).Subscribe(ss =>
			//	{
   //                 this.RaisePropertyChanged("SlidesSelectedItem");
			//	});
			//});
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
				var fullFilePath = await ShowOpenFileDialog.Handle(Unit.Default);

				if (fullFilePath != null && fullFilePath is string)
				{
					DateTime now = DateTime.Now;
					string pptxFileName = Path.GetFileName(fullFilePath);
					string targetDirectory = Path.Join(Playlist.State.PlaylistWorkingDirectory, pptxFileName, now.ToString("yyyy-MM-dd-HH-mm-ss"));
					Directory.CreateDirectory(targetDirectory);

					Data.Models.Items.SlidesGroup<ItemStateImpl> slidesGroup = PlaylistUtils.CreateSlidesGroup(targetDirectory);
					slidesGroup.Title = pptxFileName;

					//PlaylistState.Playlist.Items.Add(slidesGroup);


					// kick off
					ImportTask importTask = new ImportTask() { PPTXFilePath = (string) fullFilePath, OutputDirectory = targetDirectory };
					PlaylistUtils.AddPowerPointToPlaylist(importTask)
						.ContinueWith((s) =>
                        {
							PlaylistUtils.UpdateSlidesGroup(slidesGroup, targetDirectory);
							Dispatcher.UIThread.Post(() => Playlist.Items.Add(slidesGroup));
						});

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
					Playlist.Items.Add(songItem);
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
			XmlSerialization.WriteToXmlFile<Playlist<PlaylistStateImpl, ItemStateImpl>>(TEST_SERVICE_FILE_PATH, Playlist);
		}
		void OnLoadService()
		{
			Playlist = XmlSerialization.ReadFromXmlFile<Playlist<PlaylistStateImpl, ItemStateImpl>>(TEST_SERVICE_FILE_PATH);
		}
		void OnMoveUpItemCommand(object? itemState)
        {
			// get the index of itemState
			// move the source "item" position (in the "item source")
			// update the rest

			int v = Playlist.Items.IndexOf(itemState);

			if (v > 0)
            {
                Playlist.Items.Move(v, v - 1);
            }
        }
		void OnMoveDownItemCommand(object? itemState)
        {
			int v = Playlist.Items.IndexOf(itemState);

			if (v + 1 < Playlist.Items.Count)
			{
				Playlist.Items.Move(v, v + 1);
			}
        }
		
		void OnRemoveItemCommand(object? itemState)
        {
			// TODO confirm dialog?
			int v = Playlist.Items.IndexOf(itemState);
			Playlist.Items.RemoveAt(v);
        }

	}
}
