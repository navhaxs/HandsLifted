using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ByteSizeLib;
using Config.Net;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.ViewModels.AddItem;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Core.Views.Confirmation;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using Serilog;
using Item = HandsLiftedApp.Data.Models.Items.Item;

namespace HandsLiftedApp.Core.ViewModels;

public class MainViewModel : ViewModelBase
{
    private const int MAX_RECENT_PLAYLISTS = 5;

    public IMySettings settings;

    public LibraryViewModel LibraryViewModel { get; init; }

    public AddItemViewModel AddItemViewModel { get; init; }

    public MainViewModel()
    {
        LibraryViewModel = new LibraryViewModel();
        AddItemViewModel = new AddItemViewModel(LibraryViewModel);

        if (Design.IsDesignMode)
        {
            Playlist = new PlaylistInstance();
            Playlist.Items.Add(new SectionHeadingItem());
            Playlist.Items.Add(new LogoItemInstance(Playlist));
            Playlist.Items.Add(new SongItemInstance(Playlist));
            Playlist.Items.Add(new LogoItemInstance(Playlist));
            Playlist.Items.Add(new SectionHeadingItem());
            Playlist.Items.Add(new SongItemInstance(Playlist));
            Playlist.Items.Add(new SectionHeadingItem());
            Playlist.Items.Add(new MediaGroupItemInstance(Playlist));
            Playlist.Designs.Add(Globals.Instance.AppPreferences?.DefaultTheme ?? new BaseSlideTheme() { Name = "Default" });

            LibraryViewModel.ReloadLibraries();

            return;
        }

        settings = new ConfigurationBuilder<IMySettings>()
            .UseJsonFile("HandsLiftedApp.UserConfig.json")
            .Build();

        // The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
        ShowOpenFileDialog = new Interaction<FilePickerOpenOptions?, IReadOnlyList<IStorageFile>?>();

        EditSlideInfoCommand = ReactiveCommand.CreateFromTask<object>(async (object x) =>
        {
            if (x is CustomAxamlSlideInstance c)
            {
                SlideInfoEditorWindow window = new SlideInfoEditorWindow() { DataContext = c.parentMediaItem.Meta };
                window.Show();
            }
        });

        SlideSplitFromHere = ReactiveCommand.CreateFromTask<object>(async (object x) =>
        {
            if (x is ReadOnlyCollection<object> parameters)
            {
                try
                {
                    // todo edge cases: active selected index?

                    Slide slide = (Slide)parameters.ElementAt(0);
                    MediaGroupItemInstance currentItem = (MediaGroupItemInstance)parameters.ElementAt(1);

                    MediaGroupItemInstance newSlidesGroupItem = currentItem.slice(currentItem._Slides.IndexOf(slide));

                    if (newSlidesGroupItem != null)
                        Playlist.Items.Insert(Playlist.Items.IndexOf(currentItem) + 1, newSlidesGroupItem);
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            }
        });
        
        // AutoSave trigger
        Observable.FromEventPattern(
                handler => Playlist.Changed += handler,
                handler => Playlist.Changed -= handler)
            .Throttle(TimeSpan.FromSeconds(30))
            .Where(_ => Playlist.IsDirty)
            .Subscribe(_ => PlaylistDocumentService.AutoSaveDocument(Playlist));
        
        MessageBus.Current.Listen<LoadPlaylistAction>().Subscribe(async (msg) =>
        {
            try
            {
                // check for unsaved changes to current playlist
                if (!msg.IsStartupLoad && Playlist.IsDirty)
                {
                    var confirmation = new ShowUnsavedChangesConfirmationAction();
                    MessageBus.Current.SendMessage(confirmation);
                    var result = await confirmation.TaskCompletionSource.Task;
                    if (result == UnsavedChangesConfirmationWindow.DialogResult.Save)
                    {
                        PlaylistDocumentService.SaveDocument(Playlist);
                    }
                    else if (result == UnsavedChangesConfirmationWindow.DialogResult.Cancel)
                    {
                        return;
                    }
                    // else discard, proceed
                }
                
                // delete autosaves when loading/reloading playlist
                PlaylistDocumentService.DeleteAutoSave(msg.FilePath);
                
                string loadFilePath = msg.FilePath;
                
                if (PlaylistDocumentService.IsAutoSaveNewer(msg.FilePath))
                {
                    var confirmation = new ShowRestoreAutosaveConfirmationAction();
                    MessageBus.Current.SendMessage(confirmation);
                    bool restore = await confirmation.TaskCompletionSource.Task;
                    if (restore)
                    {
                        loadFilePath = PlaylistDocumentService.GetAutoSavePlaylistFilePath(msg.FilePath);
                        Log.Information("Restoring autosave from {FilePath}", loadFilePath);
                    }
                }

                Playlist.IsPlaylistLoading = true;

                var x = await Task.Run(() => HandsLiftedDocXmlSerializer.DeserializePlaylist(loadFilePath));
                
                string? playlistDirectoryPath = Path.GetDirectoryName(msg.FilePath);

                if (playlistDirectoryPath == null)
                {
                    throw new Exception("Could not get directory path from file path.");
                }
                
                Playlist.SelectedItemIndex = -1;

                // do not create new PlaylistInstance
                // Map properties from PlaylistSerialized to Playlist

                Playlist.Title = x.Title;
                Playlist.SlideTransitionDurationMs = x.SlideTransitionDurationMs;
                Playlist.Meta = x.Meta;
                
                Playlist.LogoGraphicFile =
                    RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                        x.LogoGraphicFile);
                
                var loadedDesigns = x.Designs.Select(design =>
                {
                    if (design.BackgroundGraphicFilePath != null &&
                        !design.BackgroundGraphicFilePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                    {
                        design.BackgroundGraphicFilePath =
                            RelativeFilePathResolver.ToAbsolutePath(playlistDirectoryPath,
                                design.BackgroundGraphicFilePath);
                    }
                    return design;
                });
                var defaultTheme = Globals.Instance.AppPreferences?.DefaultTheme;
                var designsWithDefault = defaultTheme != null
                    ? new[] { defaultTheme }.Concat(loadedDesigns)
                    : loadedDesigns;
                Playlist.Designs = new ObservableCollection<BaseSlideTheme>(designsWithDefault.ToList());
                Playlist.PlaylistFilePath = msg.FilePath;
                Playlist.PlaylistWorkingDirectory = playlistDirectoryPath;
                
                Log.Debug("Playlist load: PlaylistWorkingDirectory={PlaylistWorkingDirectory}", playlistDirectoryPath);

                var builtItems = new List<Item>();
                foreach (var deserializedItem in x.Items)
                {
                    builtItems.Add(await Dispatcher.UIThread.InvokeAsync(
                        () => ItemInstanceFactory.ToItemInstance(deserializedItem, Playlist),
                        DispatcherPriority.Background));
                }
                // Assign empty collection first so ItemsControl starts with zero items,
                // then add each item at Background priority. Template creation spreads
                // across N dispatcher yields instead of one synchronous batch.
                Playlist.Items = new PlaylistItemInstanceCollection<Item>();
                foreach (var item in builtItems)
                {
                    await Dispatcher.UIThread.InvokeAsync(
                        () => Playlist.Items.Add(item),
                        DispatcherPriority.Background);
                }
                Playlist.IsPlaylistLoading = false;

                Playlist.LastSaved = new DateTime();
                Playlist.ActiveItemInsertIndex = null;
                Playlist.QuickShowItem = null;
                Playlist.PresentationState = PlaylistInstance.PresentationStateEnum.Slides;

                // Defer the dirty reset to Background priority so deferred OAPH initial-value
                // emissions from BaseSlideTheme (via ObserveOn(MainThreadScheduler)) fire first.
                bool finalIsDirty = loadFilePath != msg.FilePath;
                await Dispatcher.UIThread.InvokeAsync(() => Playlist.IsDirty = finalIsDirty, DispatcherPriority.Background);
                // update MRU list
                MessageBus.Current.SendMessage(new UpdateLastOpenedPlaylistAction() {FilePath = msg.FilePath});
            }
            catch (Exception ex)
            {
                Playlist.IsPlaylistLoading = false;
                MessageBus.Current.SendMessage(new MessageWindowViewModel()
                    { Title = "Playlist failed to load :(", Content = $"{ex.Message}" });
                Log.Error(ex, "[DOC] Failed to parse playlist XML");
            }
        });

        MessageBus.Current.Listen<UpdateLastOpenedPlaylistAction>().Subscribe((msg) =>
        {
            var tempList = settings.RecentPlaylistFullPathsList?.ToList() ?? new List<string>();
            
            if (tempList.Contains(msg.FilePath))
            {
                tempList.Remove(msg.FilePath);
            }
            tempList.Insert(0, msg.FilePath);
            
            settings.RecentPlaylistFullPathsList = tempList
                .Take(MAX_RECENT_PLAYLISTS)
                .ToArray();
        });

        MessageBus.Current.Listen<AddItemMessage>()
            .Subscribe(async addItemMessage =>
            {
                Item? itemToInsert = null;
                switch (addItemMessage.Type)
                {
                    case AddItemMessage.AddItemType.GoogleSlides:
                        itemToInsert = new GoogleSlidesGroupItemInstance(Playlist)
                        {
                            SourceGooglePresentationId = addItemMessage.CreateInfo
                        };
                        break;
                    case AddItemMessage.AddItemType.Presentation:
                        var filePaths = await ShowOpenFileDialog.Handle(new FilePickerOpenOptions()
                        {
                            AllowMultiple = false,
                            Title = "Select Presentation File",
                            FileTypeFilter = new List<FilePickerFileType>()
                            {
                                new FilePickerFileType("Presentation Files")
                                {
                                    Patterns = new List<string>()
                                    {
                                        "*.ppt",
                                        "*.pptx",
                                        "*.odt",
                                        "*.pdf",
                                    }
                                },
                                new FilePickerFileType("All Files")
                                {
                                    Patterns = new List<string>()
                                    {
                                        "*.*"
                                    }
                                }
                            }
                        });
                        if (filePaths.Count > 0)
                        {
                            itemToInsert = CreateItem.OpenPresentationFile(filePaths[0].TryGetLocalPath(), Playlist);
                        }

                        break;
                    case AddItemMessage.AddItemType.Logo:
                        itemToInsert = new LogoItemInstance(Playlist);
                        break;
                    case AddItemMessage.AddItemType.SectionHeading:
                        itemToInsert = new SectionHeadingItem();
                        break;
                    case AddItemMessage.AddItemType.BlankGroup:
                        itemToInsert = new MediaGroupItemInstance(Playlist);
                        break;
                    case AddItemMessage.AddItemType.NewSong:
                        // var song = new SongItemInstance(Playlist);
                        // itemToInsert = song;
                        // SongEditorViewModel vm = new SongEditorViewModel(song, Playlist);
                        // SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
                        // seq.Show();
                        
                        
                        
                        break;
                    case AddItemMessage.AddItemType.MediaGroup:
                        filePaths = await ShowOpenFileDialog.Handle(new FilePickerOpenOptions()
                        {
                            AllowMultiple = false,
                            Title = "Select Media File",
                            FileTypeFilter = new List<FilePickerFileType>()
                            {
                                new FilePickerFileType("Media File")
                                {
                                    Patterns = Constants.SUPPORTED_VIDEO.Select(ext => $"*.{ext}")
                                        .Concat(Constants.SUPPORTED_IMAGE.Select(ext => $"*.{ext}")).ToList()
                                },
                                new FilePickerFileType("All Files")
                                {
                                    Patterns = new List<string>()
                                    {
                                        "*.*"
                                    }
                                }
                            }
                        });

                        if (filePaths.Count == 0)
                        {
                            return;
                        }
                        
                        MediaGroupItemInstance mediaGroupItem = new MediaGroupItemInstance(Playlist)
                            { Title = "New media group" };

                        foreach (var filePath in filePaths)
                        {
                            if (filePath != null && filePath is string)
                            {
                                DateTime now = DateTime.Now;
                                string fileName = Path.GetFileName(filePath.TryGetLocalPath());
                                string folderName = Path.GetDirectoryName(filePath.TryGetLocalPath());
                                mediaGroupItem.Items.Add(new MediaGroupItem.MediaItem()
                                    { SourceMediaFilePath = filePath.TryGetLocalPath() });
                            }
                        }

                        mediaGroupItem.GenerateSlides();

                        itemToInsert = mediaGroupItem;

                        break;
                    case AddItemMessage.AddItemType.Comment:
                        itemToInsert = new CommentItem();
                        break;
                    // case AddItemMessage.AddItemType.BibleReadingSlideGroup:
                    //     filePaths = await ShowOpenFileDialog.Handle(Unit.Default); // TODO pass accepted file types list
                    //     MediaGroupItemInstance mediaGroupItem2 = new MediaGroupItemInstance(Playlist)
                    //         { Title = "New media group" };
                    //
                    //     foreach (var filePath in filePaths)
                    //     {
                    //         if (filePath != null && filePath is string)
                    //         {
                    //             DateTime now = DateTime.Now;
                    //             string fileName = Path.GetFileName(filePath);
                    //             string folderName = Path.GetDirectoryName(filePath);
                    //             mediaGroupItem2.Items.Add(new MediaGroupItem.MediaItem()
                    //                 { SourceMediaFilePath = filePath });
                    //         }
                    //     }
                    //
                    //     mediaGroupItem2.GenerateSlides();
                    //
                    //     itemToInsert = mediaGroupItem2;
                    //     break;
                    default:
                        Log.Warning("Unknown AddItemType: {AddItemType}", addItemMessage.Type);
                        break;
                }

                if (itemToInsert != null)
                {
                    var currentSelectedItem = Playlist.SelectedItem;

                    if (addItemMessage.InsertIndex != null)
                    {
                        Playlist.Items.Insert(addItemMessage.InsertIndex.Value, itemToInsert);
                    }
                    else if (addItemMessage.ItemToInsertAfter != null)
                    {
                        var indexOf = Playlist.Items.IndexOf(addItemMessage.ItemToInsertAfter);
                        Playlist.Items.Insert(indexOf + 1, itemToInsert);
                    }
                    else
                    {
                        Playlist.Items.Add(itemToInsert);
                    }

                    if (currentSelectedItem != null)
                    {
                        Playlist.SelectedItemIndex = Playlist.Items.IndexOf(currentSelectedItem);
                    }
                }
            });

        MessageBus.Current.Listen<MoveItemCommand>()
            .Subscribe((moveItemCommand) =>
            {
                var theSelectedIndex = Playlist.Items.IndexOf(moveItemCommand.SourceItem);

                var currentSelectedItem = Playlist.SelectedItem;

                switch (moveItemCommand.Direction)
                {
                    case MoveItemCommand.DirectionValue.UP:
                        if (theSelectedIndex > 0)
                        {
                            Playlist.Items.Move(theSelectedIndex, theSelectedIndex - 1);
                        }

                        break;
                    case MoveItemCommand.DirectionValue.DOWN:
                        if (theSelectedIndex + 1 < Playlist.Items.Count)
                        {
                            Playlist.Items.Move(theSelectedIndex, theSelectedIndex + 1);
                        }

                        break;
                    case MoveItemCommand.DirectionValue.REMOVE:
                        Playlist.Items.RemoveAt(theSelectedIndex);
                        break;
                    case MoveItemCommand.DirectionValue.DUPLICATE:
                        if (theSelectedIndex != -1)
                        {
                            var itemToDuplicate = Playlist.Items[theSelectedIndex];
                            var serializedItem = HandsLiftedDocXmlSerializer.SerializeItem(itemToDuplicate, Playlist.PlaylistWorkingDirectory);
                            var clonedItem = serializedItem.Clone();
                            var newItemInstance = ItemInstanceFactory.ToItemInstance(clonedItem, Playlist);
                            Playlist.Items.Insert(theSelectedIndex + 1, newItemInstance);
                        }
                        break;
                }

                if (currentSelectedItem != null)
                {
                    Playlist.SelectedItemIndex = Playlist.Items.IndexOf(currentSelectedItem);
                }
            });

        MessageBus.Current.Listen<MoveSlideCommand>()
            .Subscribe((moveSlideCommand) =>
            {
                // rules: source and dest items must both be MEDIA GROUP ITEM
                var sourceItem = Playlist.Items.FirstOrDefault(item => item.UUID == moveSlideCommand.SourceItemUUID);
                var destItem = Playlist.Items.FirstOrDefault(item => item.UUID == moveSlideCommand.DestItemUUID);

                if (sourceItem is MediaGroupItem sourceItemAsGroup and IItemInstance sourceItemInstance &&
                    destItem is MediaGroupItem destItemAsGroup and IItemInstance destItemInstance)
                {
                    if (sourceItemInstance == destItemInstance)
                    {
                        var calcDestSlideIndex =
                            Math.Min(moveSlideCommand.DestSlideIndex, sourceItemAsGroup.Items.Count - 1);
                        if (moveSlideCommand.SourceSlideIndex == calcDestSlideIndex)
                        {
                            // do nothing
                            return;
                        }

                        var lastSelectedSlide =
                            sourceItemInstance.Slides.ElementAtOrDefault(sourceItemInstance.SelectedSlideIndex);

                        sourceItemAsGroup.Items.Move(moveSlideCommand.SourceSlideIndex, calcDestSlideIndex);

                        RegerenateSlides(sourceItem);

                        // hack - restore the last selected slide, as re-ordering slides should not affect the selected slide (however this currently causes a brief flicker)
                        if (lastSelectedSlide != null)
                        {
                            var x = sourceItemInstance.Slides.IndexOf(lastSelectedSlide);
                            sourceItemInstance.SelectedSlideIndex = x;
                        }
                    }
                    else
                    {
                        var itemToMove = sourceItemAsGroup.Items.ElementAtOrDefault(moveSlideCommand.SourceSlideIndex);
                        if (itemToMove == null)
                            return;

                        bool isMovingActiveSlide = Playlist.SelectedItem == sourceItemInstance &&
                                                   sourceItemInstance.SelectedSlideIndex ==
                                                   moveSlideCommand.SourceSlideIndex;
                        var sourceItemPreviousSelectedSlide =
                            sourceItemInstance.Slides.ElementAtOrDefault(sourceItemInstance.SelectedSlideIndex);
                        var destItemPreviousSelectedSlide =
                            destItemInstance.Slides.ElementAtOrDefault(destItemInstance.SelectedSlideIndex);

                        destItemAsGroup.Items.Insert(moveSlideCommand.DestSlideIndex, itemToMove);
                        sourceItemAsGroup.Items.RemoveAt(moveSlideCommand.SourceSlideIndex);

                        RegerenateSlides(destItemInstance);
                        RegerenateSlides(sourceItemInstance);

                        if (sourceItemPreviousSelectedSlide != null)
                        {
                            // recalculate selectedSlideIndex (may become -1)
                            sourceItemInstance.SelectedSlideIndex =
                                sourceItemInstance.Slides.IndexOf(sourceItemPreviousSelectedSlide);
                        }

                        if (destItemPreviousSelectedSlide != null && !isMovingActiveSlide)
                        {
                            // recalculate selectedSlideIndex
                            destItemInstance.SelectedSlideIndex =
                                destItemInstance.Slides.IndexOf(destItemPreviousSelectedSlide);
                        }

                        // bring focus to new item if we just moved the 'active slide'
                        if (isMovingActiveSlide)
                        {
                            destItemInstance.SelectedSlideIndex = moveSlideCommand.DestSlideIndex;
                            Playlist.SelectedItemIndex = destItemAsGroup.Index;
                        }
                    }
                }
            });

        MessageBus.Current.Listen<AddFilesToGroupItemCommand>()
            .Subscribe(command =>
            {
                var destItem = Playlist.Items.FirstOrDefault(item => item.UUID == command.DestItemUUID);
                if (destItem is MediaGroupItemInstance destItemInstance)
                {
                    foreach (var item in command.SourceFiles)
                    {
                        if (item is IStorageFile file)
                        {
                            destItemInstance.Items.Insert(command.DestSlideIndex, new MediaGroupItem.MediaItem()
                                { SourceMediaFilePath = file.Path.LocalPath });
                        }
                        // else if (item is IStorageFolder folder)
                        // {
                        // }
                    }

                    destItemInstance.GenerateSlides();
                }
            });

        MessageBus.Current.Listen<MoveItemMessage>()
            .Subscribe((moveItemMessage) =>
            {
                var currentSelectedItem = Playlist.SelectedItem;

                Playlist.Items.Move(moveItemMessage.SourceIndex,
                    Math.Min(Playlist.Items.Count - 1, moveItemMessage.DestinationIndex));

                if (currentSelectedItem != null)
                {
                    Playlist.SelectedItemIndex = Playlist.Items.IndexOf(currentSelectedItem);
                }

                Log.Debug("Moving playlist item {SourceIndex} to {DestinationIndex}", moveItemMessage.SourceIndex, moveItemMessage.DestinationIndex);
            });

        _ = Update(); // calling an async function we do not want to await
    }

    private void RegerenateSlides(object item)
    {
        // TODO GenerateSlides() should be a generic interface shared across all implementations below:
        if (item is MediaGroupItemInstance mediaGroupItemInstance)
        {
            mediaGroupItemInstance.GenerateSlides();
        }
        else if (item is PowerPointPresentationItemInstance pointPresentationItemInstance)
        {
            pointPresentationItemInstance.GenerateSlides();
        }
        else if (item is PDFSlidesGroupItemInstance pdfSlidesGroupItemInstance)
        {
            pdfSlidesGroupItemInstance.GenerateSlides();
        }
    }

    public Interaction<FilePickerOpenOptions?, IReadOnlyList<IStorageFile>?> ShowOpenFileDialog { get; }
    public ReactiveCommand<object, Unit> EditSlideInfoCommand { get; }
    public ReactiveCommand<object, Unit> SlideSplitFromHere { get; }

    private PlaylistInstance _playlist = new();

    public PlaylistInstance Playlist
    {
        get => _playlist;
        set => this.RaiseAndSetIfChanged(ref _playlist, value);
    }

    public void OnNextSlideClickCommand()
    {
        Playlist.NavigateNextSlide();
        MessageBus.Current.SendMessage(new FocusSelectedItem());
    }

    public void OnPrevSlideClickCommand()
    {
        Playlist.NavigatePreviousSlide();
        MessageBus.Current.SendMessage(new FocusSelectedItem());
    }

    private bool _IsDisplayDebugInfo = false;

    public bool IsDisplayDebugInfo
    {
        get => _IsDisplayDebugInfo;
        set => this.RaiseAndSetIfChanged(ref _IsDisplayDebugInfo, value);
    }

    private string _debugInfoText = string.Empty;

    public string DebugInfoText
    {
        get => _debugInfoText;
        set => this.RaiseAndSetIfChanged(ref _debugInfoText, value);
    }

    public DateTime CurrentTime
    {
        get => DateTime.Now;
    }

    private bool _isRunning = true;

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

                    DebugInfoText =
                        $"{ByteSize.FromBytes(memory).ToString("#.#")} {ByteSize.FromBytes(memoryUsed).ToString("#.#")}";
                }
            }
        }
    }

    private ProjectorWindow? _projectorWindow;

    public ProjectorWindow? ProjectorWindow
    {
        get => _projectorWindow;
        set => this.RaiseAndSetIfChanged(ref _projectorWindow, value);
    }

    public void OnMainWindowOpened()
    {
        // Apply OnStartup* AppPreferences:
        Playlist.IsLogo = Globals.Instance.AppPreferences.OnStartupShowLogo;

        var lastOpened = settings.RecentPlaylistFullPathsList?.FirstOrDefault();
        if (lastOpened != null && File.Exists(lastOpened))
        {
            MessageBus.Current.SendMessage(new LoadPlaylistAction() { FilePath = lastOpened, IsStartupLoad = true });
        }

        // TODO broken 
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            if (ProjectorWindow == null)
            {
                ProjectorWindow = new ProjectorWindow() { DataContext = this };
            }

            ToggleProjectorWindow(Globals.Instance.AppPreferences.OnStartupShowOutput);

            if (StageDisplayWindow == null)
            {
                StageDisplayWindow = new StageDisplayWindow();
                StageDisplayWindow.DataContext = this;
            }

            ToggleStageDisplayWindow(Globals.Instance.AppPreferences.OnStartupShowStage);
        });
    }

    public void OnProjectorClickCommand()
    {
        ToggleProjectorWindow(null, true);
    }

    public void
        ToggleProjectorWindow(bool? shouldShow = null, bool? forceShow = null) // TODO both forceShow and designShow
    {
        shouldShow = shouldShow ?? (ProjectorWindow == null || !ProjectorWindow.IsVisible);
        if (shouldShow == true)
        {
            if (ProjectorWindow == null)
            {
                ProjectorWindow = new ProjectorWindow();
                ProjectorWindow.DataContext = this;
            }

            WindowUtils.ShowAndRestoreWindowBounds(ProjectorWindow, Globals.Instance.AppPreferences.OutputDisplayBounds,
                forceShow);
        }
        else
        {
            if (ProjectorWindow != null)
                ProjectorWindow.Hide();
        }
    }

    private StageDisplayWindow? _stageDisplayWindow;

    public StageDisplayWindow? StageDisplayWindow
    {
        get => _stageDisplayWindow;
        set => this.RaiseAndSetIfChanged(ref _stageDisplayWindow, value);
    }

    public void OnStageDisplayClickCommand()
    {
        ToggleStageDisplayWindow(null, true);
    }

    public void ToggleStageDisplayWindow(bool? shouldShow = null, bool? forceShow = null)
    {
        shouldShow = shouldShow ?? (StageDisplayWindow == null || !StageDisplayWindow.IsVisible);
        if (shouldShow == true)
        {
            StageDisplayWindow = new StageDisplayWindow() { DataContext = this };
            WindowUtils.ShowAndRestoreWindowBounds(StageDisplayWindow,
                Globals.Instance.AppPreferences.StageDisplayBounds, forceShow);
        }
        else
        {
            if (StageDisplayWindow != null)
                StageDisplayWindow.Hide();
        }
    }

    #region UI

    private int _bottomLeftPanelSelectedTabIndex = 0;

    public int BottomLeftPanelSelectedTabIndex
    {
        get => _bottomLeftPanelSelectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _bottomLeftPanelSelectedTabIndex, value);
    }

    public int ItemHeight
    {
        get => 200;
    }

    public int ItemWidth
    {
        get => 290;
    }

    #endregion
}