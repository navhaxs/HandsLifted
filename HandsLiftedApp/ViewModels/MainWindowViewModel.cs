using Avalonia.Animation;
using Avalonia.Threading;
using ByteSizeLib;
using DynamicData;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Models.Slides;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.PDF;
using HandsLiftedApp.Logic;
using HandsLiftedApp.Models;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.LibraryModel;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Models.SlideDesigner;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Models.WebsocketsEngine;
using HandsLiftedApp.Models.WebsocketsV1;
using HandsLiftedApp.PropertyGridControl;
using HandsLiftedApp.Utils;
using HandsLiftedApp.ViewModels.Editor;
using HandsLiftedApp.Views;
using HandsLiftedApp.Views.App;
using HandsLiftedApp.Views.Editor;
using HandsLiftedApp.Views.Preferences;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Design = Avalonia.Controls.Design;
using Slide = HandsLiftedApp.Data.Slides.Slide;

namespace HandsLiftedApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; }

        private bool _isRunning = true;

        public DateTime CurrentTime
        {
            get => DateTime.Now;
        }
        private async Task Update()
        {
            while (_isRunning)
            {
                await Task.Delay(100);
                if (_isRunning)
                {
                    this.RaisePropertyChanged(nameof(CurrentTime));

                    if (Debugger.IsAttached && IsDisplayDebugInfo)
                    {
                        await Task.Delay(400);

                        // for debug stat #1
                        long memory = GC.GetTotalMemory(false);

                        // for debug stat #2
                        Process currentProc = Process.GetCurrentProcess();
                        long memoryUsed = currentProc.PrivateMemorySize64;

                        DebugInfoText = $"{ByteSize.FromBytes(memory).ToString("#.#")} {ByteSize.FromBytes(memoryUsed).ToString("#.#")}";
                    }
                }
            }
        }

        private string _debugInfoText = string.Empty;
        public string DebugInfoText { get => _debugInfoText; set => this.RaiseAndSetIfChanged(ref _debugInfoText, value); }

        public SlideDesignerViewModel SlideDesigner { get; } = new SlideDesignerViewModel();
        public Playlist<PlaylistStateImpl, ItemStateImpl> _playlist;

        public Playlist<PlaylistStateImpl, ItemStateImpl> Playlist
        {
            get => _playlist; set
            {
                this.RaiseAndSetIfChanged(ref _playlist, value);

                // TODO only once playlist has loaded
                if (Globals.Preferences != null && Globals.Preferences.OnStartupShowLogo)
                    Playlist.State.IsLogo = true;
            }
        }

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

        public Library Library { get; } = new Library();

        private bool _IsDisplayDebugInfo = false;
        public bool IsDisplayDebugInfo { get => _IsDisplayDebugInfo; set => this.RaiseAndSetIfChanged(ref _IsDisplayDebugInfo, value); }

        public void LoadDemoSchedule()
        {
            Log.Information("Loading demo schedule");
            Playlist = TestPlaylistDataGenerator.Generate();
            Log.Information("Loading demo schedule done");
        }
        public MainWindowViewModel()
        {
            Activator = new ViewModelActivator();
            this.WhenActivated(disposables =>
            {
                // Use WhenActivated to execute logic
                // when the view model gets deactivated.
                Disposable
                    .Create(() => this.HandleDeactivation())
                    .DisposeWith(disposables);
            });

            if (Design.IsDesignMode)
            {
                Playlist = new Playlist<PlaylistStateImpl, ItemStateImpl>();
                var song = PlaylistUtils.CreateSong();
                Playlist.Items.Add(new SectionHeadingItem<ItemStateImpl>());
                Playlist.Items.Add(song);
                Playlist.Items.Add(new SectionHeadingItem<ItemStateImpl>());
                Playlist.Items.Add(new SectionHeadingItem<ItemStateImpl>());
                Playlist.Items.Add(new SectionHeadingItem<ItemStateImpl>());
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

                        //Dispatcher.UIThread.InvokeAsync(() =>
                        //{
                        //    if (selectedItem.Slides.ElementAtOrDefault(selectedIndex + 1) != null)
                        //        selectedItem.Slides[selectedIndex + 1].OnPreloadSlide();
                        //    if (selectedItem.Slides.ElementAtOrDefault(selectedIndex - 1) != null)
                        //        selectedItem.Slides[selectedIndex - 1].OnPreloadSlide();
                        //});
                    }

                    return active;
                })
                .ToProperty(this, c => c.ActiveSlide)
            ;

            this.WhenAnyValue(x => x.ActiveSlide)
                .ObserveOn(RxApp.MainThreadScheduler) // TODO: what does this actually do?
                .Subscribe(slide =>
                {
                    // TODO: refactor this move to own file (service?)
                    var msg = new PublishStateMessage();
                    if (slide is SongSlide<SongSlideStateImpl>)
                    {
                        msg.NewPublish = new VisionScreensState() { CurrentSlide = new PublishedSongSlide() { Text = slide.SlideText } };
                    }
                    else if (slide is SongTitleSlide<SongTitleSlideStateImpl>)
                    {
                        msg.NewPublish = new VisionScreensState() { CurrentSlide = new PublishedSongTitleSlide() { Title = ((SongTitleSlide<SongTitleSlideStateImpl>)slide).Title, Copyright = ((SongTitleSlide<SongTitleSlideStateImpl>)slide).Copyright } };
                    }
                    else
                    {
                        msg.NewPublish = new VisionScreensState();
                    }

                    MessageBus.Current.SendMessage(msg);
                });

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

            // Commands
            SlideClickCommand = ReactiveCommand.CreateFromTask<object>(OnSlideClickCommand);
            AddPresentationCommand = ReactiveCommand.CreateFromTask(OpenPresentationFileAsync);
            AddGroupCommand = ReactiveCommand.CreateFromTask(OpenGroupAsync);
            AddVideoCommand = ReactiveCommand.CreateFromTask(AddVideoCommandAsync);
            AddGraphicCommand = ReactiveCommand.CreateFromTask(AddGraphicCommandAsync);
            AddLogoCommand = ReactiveCommand.Create<object>(AddLogoCommandAsync);
            AddSectionCommand = ReactiveCommand.Create<object>(AddSectionCommandAsync);
            AddTestEmptyGroupCommand = ReactiveCommand.CreateFromTask(OpenTestEmptyGroupAsync);
            AddCustomSlideCommand = ReactiveCommand.CreateFromTask(AddCustomSlideCommandAsync);
            AddGoogleSlidesCommand = ReactiveCommand.CreateFromTask(OpenGoogleSlidesAsync);
            AddPDFSlidesCommand = ReactiveCommand.CreateFromTask(OpenPresentationFileAsync);
            AddSongCommand = ReactiveCommand.CreateFromTask(AddSongAsync);
            AddNewSongCommand = ReactiveCommand.CreateFromTask(AddNewSongAsync);
            SaveServiceCommand = ReactiveCommand.Create(OnSaveService);
            NewServiceCommand = ReactiveCommand.Create(OnNewService);
            LoadServiceCommand = ReactiveCommand.Create(OnLoadService);
            EditPlaylistInfoCommand = ReactiveCommand.Create(() => MessageBus.Current.SendMessage(new MainWindowModalMessage(new PlaylistInfoEditorWindow())));
            MoveUpItemCommand = ReactiveCommand.Create<object>(OnMoveUpItemCommand);
            MoveDownItemCommand = ReactiveCommand.Create<object>(OnMoveDownItemCommand);
            EditSlideInfoCommand = ReactiveCommand.Create<object>((ac) =>
            {
                MessageBus.Current.SendMessage(new MainWindowModalMessage(new SlideInfoWindow(), false, ac));
            });

            SlideSplitFromHere = ReactiveCommand.Create<object>((ac) =>
            {
                // todo edge cases: active selected index?
                ReadOnlyCollection<object> args = ac as ReadOnlyCollection<object>;

                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> a = args[1] as SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>;

                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> newSlidesGroupItem = a.slice(a.Items.IndexOf(args[0]));

                if (newSlidesGroupItem != null)
                    Playlist.Items.Insert(Playlist.Items.IndexOf(a) + 1, newSlidesGroupItem);
            });
            SlideJoinBackwardsFromHere = ReactiveCommand.Create<object>((ac) =>
            {
                // todo edge cases: active selected index?
                ReadOnlyCollection<object> args = ac as ReadOnlyCollection<object>;

                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> thisItem = args[1] as SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>;
                var thisItemIdx = Playlist.Items.IndexOf(thisItem);

                if (thisItemIdx < 1)
                {
                    return;
                }

                var lastItem = Playlist.Items[thisItemIdx - 1];
                if (lastItem is SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> s)
                {
                    s.Items.AddRange(thisItem.Items);
                    Playlist.Items.RemoveAt(thisItemIdx);
                }
            });
            SlideJoinForwardsFromHere = ReactiveCommand.Create<object>((ac) =>
            {
                // todo edge cases: active selected index?
                ReadOnlyCollection<object> args = ac as ReadOnlyCollection<object>;

                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> thisItem = args[1] as SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>;
                var thisItemIdx = Playlist.Items.IndexOf(thisItem);

                var lastItem = Playlist.Items.ElementAtOrDefault(thisItemIdx + 1);
                if (lastItem is SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> s)
                {
                    thisItem.Items.AddRange(s.Items);
                    Playlist.Items.Remove(lastItem);
                }
            });
            RemoveItemCommand = ReactiveCommand.Create<object>(OnRemoveItemCommand);
            OnPreferencesWindowCommand = ReactiveCommand.Create(() => MessageBus.Current.SendMessage(new MainWindowModalMessage(new PreferencesWindow())));
            OnAboutWindowCommand = ReactiveCommand.Create(() => MessageBus.Current.SendMessage(new MainWindowModalMessage(new AboutWindow())));
            OnChangeLogoCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                try
                {
                    var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);
                    if (filePaths.Length == 0) return;
                    Playlist.LogoGraphicFile = filePaths.First();
                }
                catch (Exception e)
                {
                    Debug.Print(e.Message);
                }
            });
            // The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
            ShowOpenFileDialog = new Interaction<Unit, string[]?>();
            ShowOpenFolderDialog = new Interaction<Unit, string?>();

            _ = Update(); // calling an async function we do not want to await

            // TODO don't load until later...
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (File.Exists(TEST_SERVICE_FILE_PATH))
                {
                    try
                    {
                        LOAD_TEST_SERVICE();
                    }
                    catch (Exception _)
                    {
                        LoadDemoSchedule();
                    }
                }
                else
                {
                    LoadDemoSchedule();
                }
            });

            MessageBus.Current.Listen<ActionMessage>()
               .Subscribe(x =>
               {
                   switch (x.Action)
                   {
                       case ActionMessage.NavigateSlideAction.NextSlide:
                           Playlist.State.NavigateNextSlide();
                           break;
                       case ActionMessage.NavigateSlideAction.PreviousSlide:
                           Playlist.State.NavigatePreviousSlide();
                           break;
                   }
               });

            MessageBus.Current.Listen<AddItemToPlaylistMessage>()
                .Subscribe(x =>
                {
                    PlaylistUtils.AddItemFromFile(ref _playlist, x.filenames);
                });

            MessageBus.Current.Listen<MoveItemMessage>()
                .Subscribe(x =>
                {

                    var theSelectedItem = Playlist.State.SelectedItem;

                    Playlist.Items.Move(x.SourceIndex, x.DestinationIndex);

                    // Is this working??
                    var theSelectedIndex = Playlist.Items.IndexOf(theSelectedItem);

                    Debug.Print($"Moving playlist item {x.SourceIndex} to {x.DestinationIndex}");

                    // TODO we MUST update SelectedItemIndex

                    if (x.SourceIndex == Playlist.State.SelectedItemIndex)
                    {
                        Debug.Print($"Updating the SelectedItemIndex from {Playlist.State.SelectedItemIndex} to {x.DestinationIndex}");
                        Playlist.State.SelectedItemIndex = x.DestinationIndex;
                    }
                    else
                    {
                        Debug.Print($"Updating the SelectedItemIndex to {theSelectedIndex}");

                        // the selected item index is now incorrect because the list has been shuffled.
                        Playlist.State.SelectedItemIndex = theSelectedIndex;
                    }

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

            this
                .WhenAnyValue(x => x.ActiveSlide)
                //.ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x =>
                {
                    Globals.StageDisplay.SelectedIndex = (x is SongTitleSlide<SongTitleSlideStateImpl> || x is SongSlide<SongSlideStateImpl>)
                        ? 1
                        : 0;
                });

            // TODO initialise at the right place (tm)
            HandsLiftedWebServer.Start();

            ToggleProjectorWindow(true);

            if (Globals.Preferences.OnStartupShowStage)
                ToggleStageDisplayWindow(true);

        }

        private void HandleDeactivation()
        {
            Log.Information("MainWindowViewModel is deactivating");
            _isRunning = false;
        }

        private ProjectorWindow _projectorWindow;
        public ProjectorWindow ProjectorWindow { get => _projectorWindow; set => this.RaiseAndSetIfChanged(ref _projectorWindow, value); }
        public void OnProjectorClickCommand()
        {
            ToggleProjectorWindow();
        }

        private StageDisplayWindow _stageDisplayWindow;
        public StageDisplayWindow StageDisplayWindow { get => _stageDisplayWindow; set => this.RaiseAndSetIfChanged(ref _stageDisplayWindow, value); }

        public void OnStageDisplayClickCommand()
        {
            ToggleStageDisplayWindow();
        }

        public void ToggleProjectorWindow(bool? shouldShow = null)
        {
            shouldShow = shouldShow ?? (ProjectorWindow == null || !ProjectorWindow.IsVisible);
            if (shouldShow == true)
            {
                if (ProjectorWindow == null)
                {
                    ProjectorWindow = new ProjectorWindow();
                    ProjectorWindow.DataContext = this;
                }
                ProjectorWindow.Show();
                ProjectorWindow.UpdateWindowVisibility(true);
            }
            else
            {
                if (ProjectorWindow != null)
                    ProjectorWindow.Close();
            }
        }

        public void ToggleStageDisplayWindow(bool? shouldShow = null)
        {
            shouldShow = shouldShow ?? (StageDisplayWindow == null || !StageDisplayWindow.IsVisible);
            if (shouldShow == true)
            {
                StageDisplayWindow = new StageDisplayWindow();
                StageDisplayWindow.DataContext = this;
                StageDisplayWindow.Show();
            }
            else
            {
                if (StageDisplayWindow != null)
                    StageDisplayWindow.Close();
            }
        }

        private bool _IsSidebarOpen = true;
        public bool IsSidebarOpen { get => _IsSidebarOpen; set => this.RaiseAndSetIfChanged(ref _IsSidebarOpen, value); }

        private int _BottomLeftPanelSelectedTabIndex = 0;
        public int BottomLeftPanelSelectedTabIndex { get => _BottomLeftPanelSelectedTabIndex; set => this.RaiseAndSetIfChanged(ref _BottomLeftPanelSelectedTabIndex, value); }

        public void OnDebugClickCommand()
        {
            ObjectInspectorWindow p = new ObjectInspectorWindow() { DataContext = this };
            p.Show();
        }

        public void OnNextSlideClickCommand()
        {
            //SlidesSelectedIndex += 1;
            Playlist.State.NavigateNextSlide();
            MessageBus.Current.SendMessage(new FocusSelectedItem());
        }

        public void OnPrevSlideClickCommand()
        {
            //SlidesSelectedIndex -= 1;
            Playlist.State.NavigatePreviousSlide();
            MessageBus.Current.SendMessage(new FocusSelectedItem());
        }

        public ReactiveCommand<object, Unit> SlideClickCommand { get; }
        public ReactiveCommand<Unit, Unit> AddPresentationCommand { get; }
        public ReactiveCommand<Unit, Unit> AddGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> AddVideoCommand { get; }
        public ReactiveCommand<Unit, Unit> AddGraphicCommand { get; }
        public ReactiveCommand<object, Unit> AddLogoCommand { get; }
        public ReactiveCommand<object, Unit> AddSectionCommand { get; }
        public ReactiveCommand<Unit, Unit> AddTestEmptyGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> AddCustomSlideCommand { get; }
        public ReactiveCommand<Unit, Unit> AddGoogleSlidesCommand { get; }
        public ReactiveCommand<Unit, Unit> AddPDFSlidesCommand { get; }
        public ReactiveCommand<Unit, Unit> AddSongCommand { get; }
        public ReactiveCommand<Unit, Unit> AddNewSongCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveServiceCommand { get; }
        public ReactiveCommand<Unit, Unit> NewServiceCommand { get; }
        public ReactiveCommand<Unit, Unit> LoadServiceCommand { get; }
        public ReactiveCommand<Unit, Unit> EditPlaylistInfoCommand { get; }
        public ReactiveCommand<object, Unit> MoveUpItemCommand { get; }
        public ReactiveCommand<object, Unit> MoveDownItemCommand { get; }
        public ReactiveCommand<object, Unit> RemoveItemCommand { get; }
        public ReactiveCommand<object, Unit> EditSlideInfoCommand { get; }
        public ReactiveCommand<object, Unit> SlideSplitFromHere { get; }
        public ReactiveCommand<object, Unit> SlideJoinBackwardsFromHere { get; }
        public ReactiveCommand<object, Unit> SlideJoinForwardsFromHere { get; }
        public ReactiveCommand<Unit, Unit> OnAboutWindowCommand { get; }
        public ReactiveCommand<Unit, Unit> OnChangeLogoCommand { get; }
        public ReactiveCommand<Unit, Unit> OnPreferencesWindowCommand { get; }

        public Interaction<Unit, string[]?> ShowOpenFileDialog { get; }
        public Interaction<Unit, string?> ShowOpenFolderDialog { get; }

        private async Task OpenGoogleSlidesAsync()
        {
            string SourceGooglePresentationId = "1-EGlDIgKK8cnAD_L77JI_hFNL_RZqHPAkR-rvnezmz0";

            try
            {
                DateTime now = DateTime.Now;
                string fileName = SourceGooglePresentationId; // Path.GetFileName(fullFilePath);

                string targetDirectory = Path.Join(Playlist.State.PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                Directory.CreateDirectory(targetDirectory);

                GoogleSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, GoogleSlidesGroupItemStateImpl> slidesGroup = new GoogleSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, GoogleSlidesGroupItemStateImpl>() { Title = fileName, SourceGooglePresentationId = SourceGooglePresentationId };

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
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }
        }

        private async Task OpenPresentationFileAsync()
        {
            try
            {
                var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);

                foreach (var path in filePaths)
                {

                    if (path != null && path is string)
                    {

                        DateTime now = DateTime.Now;
                        string fileName = Path.GetFileName(path);

                        string targetDirectory = Path.Join(Playlist.State.PlaylistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                        Directory.CreateDirectory(targetDirectory);

                        if (fileName.EndsWith(".pptx"))
                        {
                            Log.Debug($"Importing PowerPoint file: {path}");
                            PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl> slidesGroup = new PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl>() { Title = fileName, SourcePresentationFile = path };

                            Playlist.Items.Add(slidesGroup);

                            slidesGroup.SyncState.SyncCommand();
                        }
                        else if (fileName.EndsWith(".pdf"))
                        {
                            Log.Debug($"Importing PDF file: {path}");
                            PDFSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = new PDFSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>() { Title = fileName, SourcePresentationFile = path };

                            Playlist.Items.Add(slidesGroup);

                            ConvertPDF.Convert(path, targetDirectory); // todo move into State as a SyncCommand
                            PlaylistUtils.UpdateSlidesGroup(ref slidesGroup, targetDirectory);
                        }
                        else
                        {
                            Log.Error($"Unsupported file type: {path}");
                            return;
                        }

                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            // wait for UI to update...
                            Dispatcher.UIThread.RunJobs();
                            // and now we can jump to view
                            var count = Playlist.Items.Count;
                            MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = count - 1 });
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }
        }

        private async Task OnSlideClickCommand(object? ac)
        {
            ReadOnlyCollection<object> args = ac as ReadOnlyCollection<object>;
            Slide slide = (Slide)args[0];
            Item<ItemStateImpl> item = (Item<ItemStateImpl>)args[1];

            int itemIndex = Playlist.Items.IndexOf(item);

            if (Playlist.State.IsLogo)
            {
                Playlist.State.IsLogo = false;
            }

            if (Playlist.State.IsBlank)
            {
                Playlist.State.IsBlank = false;
            }

            int SlideIndex = item.Slides.IndexOf(slide);
            Log.Information($"OnSlideClickCommand item=[{item.Title}] slide=[{SlideIndex}]");

            Playlist.State.NavigateToReference(new SlideReference() { SlideIndex = SlideIndex, ItemIndex = itemIndex, Slide = slide });
        }

        private async Task OpenGroupAsync()
        {

        }
        private async Task AddVideoCommandAsync()
        {
            //TODO insert index
            try
            {
                var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);

                foreach (var item in filePaths)
                {

                    if (item != null && item is string)
                    {

                        DateTime now = DateTime.Now;
                        string fileName = Path.GetFileName(item);
                        string folderName = Path.GetDirectoryName(item);

                        SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>() { Title = fileName };

                        Playlist.Items.Add(slidesGroup);

                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            // wait for UI to update...
                            Dispatcher.UIThread.RunJobs();
                            // and now we can jump to view
                            var count = Playlist.Items.Count;
                            MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = count - 1 });
                        });

                        slidesGroup.Items.Add(PlaylistUtils.GenerateMediaContentSlide(item));
                    }

                }


            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
        }
        private async Task AddGraphicCommandAsync()
        {
            try
            {
                var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);

                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>() { Title = "New media group" };

                Playlist.Items.Add(slidesGroup);
                foreach (var filePath in filePaths)
                {
                    if (filePath != null && filePath is string)
                    {

                        DateTime now = DateTime.Now;
                        string fileName = Path.GetFileName(filePath);
                        string folderName = Path.GetDirectoryName(filePath);
                        slidesGroup.Items.Add(PlaylistUtils.GenerateMediaContentSlide(filePath));
                    }
                }

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // wait for UI to update...
                    Dispatcher.UIThread.RunJobs();
                    // and now we can jump to view
                    var count = Playlist.Items.Count;
                    MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = count - 1 });
                });

            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
        }
        private void AddLogoCommandAsync(object insertAfterThisIndex)
        {
            AddItem(insertAfterThisIndex, new LogoItem<ItemStateImpl>());
        }

        private void AddItem(object insertAfterThisIndex, Item<ItemStateImpl> item)
        {
            int insertIdx = (insertAfterThisIndex != null && insertAfterThisIndex is int) ? ((int)insertAfterThisIndex + 1) : 0;// Playlist.Items.Count;

            Playlist.Items.Insert(insertIdx, item);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // wait for UI to update...
                Dispatcher.UIThread.RunJobs();
                // and now we can jump to view
                MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = insertIdx });
            });
        }

        private void AddSectionCommandAsync(object insertAfterThisIndex)
        {
            AddItem(insertAfterThisIndex, new SectionHeadingItem<ItemStateImpl>());
        }
        private async Task OpenTestEmptyGroupAsync()
        {
            try
            {
                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>() { Title = "(Blank)" };
                Playlist.Items.Add(slidesGroup);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
        }
        private async Task AddCustomSlideCommandAsync()
        {
            try
            {
                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = new SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>() { Title = "(Blank)" };
                slidesGroup.Slides.Add(new CustomSlide());
                Playlist.Items.Add(slidesGroup);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
        }


        // deprecated
        private void ImportFromFile(string fullFilePath, string playlistWorkingDirectory)
        {
            if (fullFilePath != null && fullFilePath is string)
            {

                DateTime now = DateTime.Now;
                string fileName = Path.GetFileName(fullFilePath);

                string targetDirectory = Path.Join(playlistWorkingDirectory, FilenameUtils.ReplaceInvalidChars(fileName) + "_" + now.ToString("yyyy-MM-dd-HH-mm-ss"));
                Directory.CreateDirectory(targetDirectory);

                SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroup = PlaylistUtils.CreateSlidesGroup(targetDirectory);
                slidesGroup.Title = fileName;

                Dispatcher.UIThread.InvokeAsync(() => Playlist.Items.Add(slidesGroup));

                if (fileName.EndsWith(".pptx"))
                {
                    // kick off
                    // ImportTask importTask = new ImportTask() { PPTXFilePath = (string)fullFilePath, OutputDirectory = targetDirectory };
                    // PlaylistUtils.AddPowerPointToPlaylist(importTask, (_) => { })
                    //     .ContinueWith((s) =>
                    //     {
                    //         PlaylistUtils.UpdateSlidesGroup(ref slidesGroup, targetDirectory);
                    //     });
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
                var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);

                foreach (var fileName in filePaths)
                {

                    if (fileName != null && fileName is string)
                    {
                        var songItem = SongImporter.createSongItemFromTxtFile((string)fileName);
                        Playlist.Items.Add(songItem);
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
            }
        }
        private async Task AddNewSongAsync()
        {
            try
            {
                SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> song = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>();
                Playlist.Items.Add(song);

                SongEditorViewModel vm = new SongEditorViewModel() { song = song };
                SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
                seq.Show();
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }
        }

        const string TEST_SERVICE_FILE_PATH = @"C:\VisionScreens\service.xml";
        void OnSaveService()
        {
            try
            {
                XmlSerialization.WriteToXmlFile<Playlist<PlaylistStateImpl, ItemStateImpl>>(TEST_SERVICE_FILE_PATH, Playlist);
                MessageBus.Current.SendMessage(new MainWindowModalMessage(new MessageWindow() { Title = "Playlist Saved" }, true, new MessageWindowAction() { Title = "Playlist Saved", Content = $"Written to {TEST_SERVICE_FILE_PATH}" }));
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to write XML");
                MessageBus.Current.SendMessage(new MainWindowModalMessage(new MessageWindow() { Title = "Playlist Saved" }, true, new MessageWindowAction() { Title = "Playlist Failed to Save :(", Content = $"Target was {TEST_SERVICE_FILE_PATH}\n${e.Message}" }));
            }
        }

        void OnNewService()
        {
            MessageBus.Current.SendMessage(new MainWindowModalMessage(new MessageWindow() { Title = "Playlist Saved" }, true, new MessageWindowAction() { Title = "New Playlist", Content = $"" }));

            Playlist = new Playlist<PlaylistStateImpl, ItemStateImpl>();
            Playlist = TestPlaylistDataGenerator.Generate();
        }

        void OnLoadService()
        {
            LOAD_TEST_SERVICE();
            MessageBus.Current.SendMessage(new MainWindowModalMessage(new MessageWindow(), true, new MessageWindowAction() { Title = "Loaded Playlist", Content = $"{TEST_SERVICE_FILE_PATH}" }));
        }

        void LOAD_TEST_SERVICE()
        {

            Log.Debug("Loading test service.xml");
            Playlist = XmlSerialization.ReadFromXmlFile<Playlist<PlaylistStateImpl, ItemStateImpl>>(TEST_SERVICE_FILE_PATH);
            Log.Debug("Loading test service.xml DONE");

            // TODO: workaround to fixup data structure after loading from XML
            foreach (var i in Playlist.Items)
            {
                if (i is SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> s)
                {
                    foreach (var a in s.Arrangement)
                    {
                        var mapped = s.Stanzas.First(stanza => stanza.Name == a.Value.Name);
                        a.Value = mapped;
                    }
                }
            }

            foreach (var i in Playlist.Items)
            {
                if (i.GetType() == Types.Items.GOOGLE_SLIDES)
                {
                    ((GoogleSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, GoogleSlidesGroupItemStateImpl>)i).SyncState.SyncCommand();
                }
                if (i.GetType() == Types.Items.POWERPOINT)
                {
                    ((PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl>)i).SyncState.SyncCommand();
                }
            }
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

            if (itemState == null || itemState.GetType().IsSubclassOf(typeof(Item)) || itemState.GetType() == typeof(Item))
                return;

            int v = Playlist.Items.IndexOf(itemState);

            if (v > -1)
                Playlist.Items.RemoveAt(v);

        }

    }
}
