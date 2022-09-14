using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.PDF;
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
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
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

        private ObservableAsPropertyHelper<Slide> _previousSlide;

        public Slide PreviousSlide { get => _previousSlide.Value; }

        private ObservableAsPropertyHelper<Slide> _nextSlide;

        public Slide NextSlide { get => _nextSlide.Value; }

        private ObservableAsPropertyHelper<Slide> _nextSlideWithinItem;

        public Slide NextSlideWithinItem { get => _nextSlideWithinItem.Value; }

        private ObservableAsPropertyHelper<IPageTransition?> _activeItemPageTransition;
        public IPageTransition? ActiveItemPageTransition { get => _activeItemPageTransition.Value; }

        public void LoadDemoSchedule()
        {
            Playlist = TestPlaylistDataGenerator.Generate();
        }
        public MainWindowViewModel()
        {
            if (Design.IsDesignMode)
            {
                Playlist = new Playlist<PlaylistStateImpl, ItemStateImpl>();
                var song = PlaylistUtils.CreateSong();
                Playlist.Items.Add(song);
                return;
            }

            _activeSlide = this.WhenAnyValue(x => x.Playlist.State.ActiveSlide, x => x.Playlist.State.IsLogo, x => x.LogoSlideInstance,
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


                    // TODO Implement nextSlide and previousSlide properly
                    // which will load slides from NEXT and PREV *item* into consideration!!
                    // HACK
                    var selectedItem = this.Playlist.State.SelectedItem;
                    if (selectedItem != null)
                    {
                        var selectedIndex = this.Playlist.State.SelectedItem.State.SelectedSlideIndex;

                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (selectedItem.Slides.ElementAtOrDefault(selectedIndex + 1) != null)
                                selectedItem.Slides[selectedIndex + 1].OnPreloadSlide();
                            if (selectedItem.Slides.ElementAtOrDefault(selectedIndex - 1) != null)
                                selectedItem.Slides[selectedIndex - 1].OnPreloadSlide();
                        });
                    }

                    return active;
                })
                .ToProperty(this, c => c.ActiveSlide)
            ;

            _previousSlide = this.WhenAnyValue(x => x.Playlist.State.PreviousSlide, x => x.Playlist.State.IsLogo, x => x.LogoSlideInstance,
                x => x.Playlist.State.IsBlank, x => x.BlankSlideInstance,
               (previous, isLogo, logoSlideInstance, isBlank, blankSlideInstance) =>
               {
                   return previous;
               })
               .ToProperty(this, c => c.PreviousSlide)
           ;

            _nextSlide = this.WhenAnyValue(x => x.Playlist.State.NextSlide, x => x.Playlist.State.IsLogo, x => x.LogoSlideInstance,
                x => x.Playlist.State.IsBlank, x => x.BlankSlideInstance,
               (next, isLogo, logoSlideInstance, isBlank, blankSlideInstance) =>
               {
                   return next;
               })
               .ToProperty(this, c => c.NextSlide)
           ;

            _nextSlideWithinItem = this.WhenAnyValue(x => x.Playlist.State.NextSlideWithinItem, x => x.Playlist.State.IsLogo, x => x.LogoSlideInstance,
                x => x.Playlist.State.IsBlank, x => x.BlankSlideInstance,
               (next, isLogo, logoSlideInstance, isBlank, blankSlideInstance) =>
               {
                   return next;
               })
               .ToProperty(this, c => c.NextSlideWithinItem)
           ;

            _activeItemPageTransition = this.WhenAnyValue(
                x => x.Playlist.State.SelectedItem.State.PageTransition,
                (IPageTransition? pageTransition) =>
                {
                    if (pageTransition != null)
                        return pageTransition;

                    return null;
                })
                .ObserveOn(RxApp.MainThreadScheduler)
                .ToProperty(this, c => c.ActiveItemPageTransition);

            // The OpenFile command is bound to a button/menu item in the UI.
            AddPresentationCommand = ReactiveCommand.CreateFromTask(OpenPresentationFileAsync);
            AddGroupCommand = ReactiveCommand.CreateFromTask(OpenGroupAsync);
            AddTestEmptyGroupCommand = ReactiveCommand.CreateFromTask(OpenTestEmptyGroupAsync);
            AddGoogleSlidesCommand = ReactiveCommand.CreateFromTask(OpenGoogleSlidesAsync);
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
            ShowOpenFolderDialog = new Interaction<Unit, string?>();

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

                    // HACK run me from different thread. gives time for UI to update first
                    new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        Thread.Sleep(100);
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = x.DestinationIndex });
                        });
                    }).Start();
                });


            // TODO initialise at the right place (tm)
            var ws = new HandsLiftedWebServer();
            ws.Start();
        }

        public void OnProjectorClickCommand()
        {
            StageDisplayWindow s = new StageDisplayWindow();
            s.DataContext = this;
            s.Show();
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
        public ReactiveCommand<Unit, Unit> AddGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> AddTestEmptyGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> AddGoogleSlidesCommand { get; }
        public ReactiveCommand<Unit, Unit> AddSongCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveServiceCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadServiceCommand { get; }
        public ReactiveCommand<object, Unit> MoveUpItemCommand { get; }
        public ReactiveCommand<object, Unit> MoveDownItemCommand { get; }
        public ReactiveCommand<object, Unit> RemoveItemCommand { get; }
        public ReactiveCommand<Unit, Unit> OnAboutWindowCommand { get; }

        public Interaction<Unit, string?> ShowOpenFileDialog { get; }
        public Interaction<Unit, string?> ShowOpenFolderDialog { get; }
        private async Task OpenGoogleSlidesAsync()
        {
            string SourceGooglePresentationId = "1-EGlDIgKK8cnAD_L77JI_hFNL_RZqHPAkR-rvnezmz0";
            //Importer.GoogleSlides.Program.Result result = Importer.GoogleSlides.Program.Run("https://docs.google.com/presentation/d/1AjkGrL1NzOR5gVWeJ_YLlPtRd19OEaT4ZnW7Y2qkNPM/edit?usp=sharing", Playlist.State.PlaylistWorkingDirectory);

            //ImportFromFile(result.OutputFullFilePath, Playlist.State.PlaylistWorkingDirectory);


            try
            {
                //var fullFilePath = await ShowOpenFileDialog.Handle(Unit.Default);

                //if (fullFilePath != null && fullFilePath is string)
                //{

                DateTime now = DateTime.Now;
                string fileName = SourceGooglePresentationId; // Path.GetFileName(fullFilePath);

                string targetDirectory = Path.Join(Playlist.State.PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                Directory.CreateDirectory(targetDirectory);

                GoogleSlidesGroupItem<ItemStateImpl, GoogleSlidesGroupItemStateImpl> slidesGroup = new GoogleSlidesGroupItem<ItemStateImpl, GoogleSlidesGroupItemStateImpl>() { Title = fileName, SourceGooglePresentationId = SourceGooglePresentationId };

                Playlist.Items.Add(slidesGroup);

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // wait for UI to update...
                    Dispatcher.UIThread.RunJobs();
                    // and now we can jump to view
                    var count = Playlist.Items.Count;
                    MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = count - 1 });
                });

                slidesGroup.SyncState.SyncCommand();
                //}
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
            //////////
        }
        private async Task OpenPresentationFileAsync()
        {
            try
            {
                var fullFilePath = await ShowOpenFileDialog.Handle(Unit.Default);

                if (fullFilePath != null && fullFilePath is string)
                {

                    DateTime now = DateTime.Now;
                    string fileName = Path.GetFileName(fullFilePath);

                    string targetDirectory = Path.Join(Playlist.State.PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                    Directory.CreateDirectory(targetDirectory);

                    //PowerPointSlidesGroupItem<PowerPointSlidesGroupItemStateImpl> slidesGroup = new PowerPointSlidesGroupItem<PowerPointSlidesGroupItemStateImpl>() { Title = fileName };
                    PowerPointSlidesGroupItem<ItemStateImpl, PowerPointSlidesGroupItemStateImpl> slidesGroup = new PowerPointSlidesGroupItem<ItemStateImpl, PowerPointSlidesGroupItemStateImpl>() { Title = fileName, SourcePresentationFile = fullFilePath };


                    Playlist.Items.Add(slidesGroup);

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // wait for UI to update...
                        Dispatcher.UIThread.RunJobs();
                        // and now we can jump to view
                        var count = Playlist.Items.Count;
                        MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = count - 1 });
                    });

                    slidesGroup.SyncState.SyncCommand();
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
        }
        private async Task OpenGroupAsync()
        {
            try
            {
                var fullPath = await ShowOpenFolderDialog.Handle(Unit.Default);

                if (fullPath != null && fullPath is string)
                {

                    DateTime now = DateTime.Now;
                    string folderName = Path.GetDirectoryName(fullPath);

                    SlidesGroupItem<ItemStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl>() { Title = folderName };

                    Playlist.Items.Add(slidesGroup);

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // wait for UI to update...
                        Dispatcher.UIThread.RunJobs();
                        // and now we can jump to view
                        var count = Playlist.Items.Count;
                        MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = count - 1 });
                    });

                    //slidesGroup.SyncState.SyncCommand();
                    PlaylistUtils.UpdateSlidesGroup(ref slidesGroup, fullPath);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
        }

        private async Task OpenTestEmptyGroupAsync()
        {
            try
            {
                SlidesGroupItem<ItemStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl>() { Title = "(Blank)" };
                Playlist.Items.Add(slidesGroup);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
        }

        private void ImportFromFile(string fullFilePath, string playlistWorkingDirectory)
        {
            if (fullFilePath != null && fullFilePath is string)
            {

                DateTime now = DateTime.Now;
                string fileName = Path.GetFileName(fullFilePath);

                string targetDirectory = Path.Join(playlistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                Directory.CreateDirectory(targetDirectory);

                SlidesGroupItem<ItemStateImpl> slidesGroup = PlaylistUtils.CreateSlidesGroup(targetDirectory);
                slidesGroup.Title = fileName;

                Dispatcher.UIThread.InvokeAsync(() => Playlist.Items.Add(slidesGroup));

                if (fileName.EndsWith(".pptx"))
                {
                    // kick off
                    ImportTask importTask = new ImportTask() { PPTXFilePath = (string)fullFilePath, OutputDirectory = targetDirectory };
                    PlaylistUtils.AddPowerPointToPlaylist(importTask, (_) => { })
                        .ContinueWith((s) =>
                        {
                            PlaylistUtils.UpdateSlidesGroup(ref slidesGroup, targetDirectory);
                        });
                }
                else if (fileName.EndsWith(".pdf"))
                {
                    ConvertPDF.Convert(fullFilePath, targetDirectory);
                    PlaylistUtils.UpdateSlidesGroup(ref slidesGroup, targetDirectory);
                }

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
