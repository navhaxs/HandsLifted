using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.PowerPoint;
using HandsLiftedApp.Logic;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.PropertyGridControl;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views;
using HandsLiftedApp.Views.App;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using static HandsLiftedApp.Importer.PowerPoint.Main;

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

        public OverlayState OverlayState { get; set; }

        public Slide LogoSlideInstance { get; } = new LogoSlide();
        public Slide BlankSlideInstance { get; } = new BlankSlide();
        public Boolean IsFrozen { get; set; }

        private ObservableAsPropertyHelper<Slide> _activeSlide;

        public Slide ActiveSlide { get => _activeSlide.Value; }

        public void LoadDemoSchedule()
        {
            Playlist = TestPlaylistDataGenerator.Generate();
        }
        public MainWindowViewModel()
        {
            if (Design.IsDesignMode)
            {
                Playlist = new Playlist<PlaylistStateImpl, ItemStateImpl>();
                Playlist.Items.Add(PlaylistUtils.CreateSong());
                return;
            }

            _activeSlide = this.WhenAnyValue(x => x.Playlist.State.ActiveItemSlide, x => x.Playlist.State.IsLogo, x => x.LogoSlideInstance,
                 x => x.Playlist.State.IsBlank, x => x.BlankSlideInstance,
                (active, isLogo, logoSlideInstance, isBlank, blankSlideInstance) =>
                {
                    if (isLogo)
                    {
                        return logoSlideInstance;
                    }

                    if (isBlank)
                    {
                        return blankSlideInstance;
                    }

                    return active;
                })
                .ToProperty(this, c => c.ActiveSlide)
            ;

            // The OpenFile command is bound to a button/menu item in the UI.
            AddPresentationCommand = ReactiveCommand.CreateFromTask(OpenPPTXFileAsync);
            AddSongCommand = ReactiveCommand.CreateFromTask(AddSongAsync);
            SaveServiceCommand = ReactiveCommand.Create(OnSaveService);
            LoadServiceCommand = ReactiveCommand.Create(OnLoadService);
            MoveUpItemCommand = ReactiveCommand.Create<object>(OnMoveUpItemCommand);
            MoveDownItemCommand = ReactiveCommand.Create<object>(OnMoveDownItemCommand);
            RemoveItemCommand = ReactiveCommand.Create<object>(OnRemoveItemCommand);
            OnAboutWindowCommand = ReactiveCommand.Create(() =>
            {
                AboutWindow p = new AboutWindow() { DataContext = this };
                // set parent window?
                p.Show();
            });

            // The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
            ShowOpenFileDialog = new Interaction<Unit, string?>();

            _ = Update(); // calling an async function we do not want to await

            LoadDemoSchedule();

            MessageBus.Current.Listen<ActionMessage>()
               .Subscribe(x =>
               {
                   switch (x.Action)
                   {
                       case ActionMessage.NavigateSlideAction.NextSlide:
                           OnNextSlideClickCommand();
                           break;
                       case ActionMessage.NavigateSlideAction.PreviousSlide:
                           OnPrevSlideClickCommand();
                           break;
                   }
               });

            MessageBus.Current.Listen<MoveItemMessage>()
                .Subscribe(x =>
                {
                    Playlist.Items.Move(x.SourceIndex, x.DestinationIndex);
                });


            // TODO initialise at the right place (tm)
            var ws = new HandsLiftedWebServer();
            ws.Start();
        }

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
            Playlist.State.NavigateNextSlide();
        }

        public void OnPrevSlideClickCommand()
        {
            //SlidesSelectedIndex -= 1;
            Playlist.State.NavigatePreviousSlide();
        }

        public ReactiveCommand<Unit, Unit> AddPresentationCommand { get; }
        public ReactiveCommand<Unit, Unit> AddSongCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveServiceCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadServiceCommand { get; }
        public ReactiveCommand<object, Unit> MoveUpItemCommand { get; }
        public ReactiveCommand<object, Unit> MoveDownItemCommand { get; }
        public ReactiveCommand<object, Unit> RemoveItemCommand { get; }
        public ReactiveCommand<Unit, Unit> OnAboutWindowCommand { get; }

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

                    Dispatcher.UIThread.Post(() => Playlist.Items.Add(slidesGroup));

                    // kick off
                    ImportTask importTask = new ImportTask() { PPTXFilePath = (string)fullFilePath, OutputDirectory = targetDirectory };
                    PlaylistUtils.AddPowerPointToPlaylist(importTask)
                        .ContinueWith((s) =>
                        {
                            PlaylistUtils.UpdateSlidesGroup(slidesGroup, targetDirectory);
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
                    var songItem = SongImporter.ImportSongFromTxt((string)fileName);
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
