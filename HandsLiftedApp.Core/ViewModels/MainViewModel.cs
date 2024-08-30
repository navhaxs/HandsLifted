using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using ByteSizeLib;
using Config.Net;
using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.ViewModels.AddItem;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views;
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
    public IMySettings settings;
    
    public LibraryViewModel LibraryViewModel { get; init; }

    public AddItemViewModel AddItemViewModel { get; init; }
    
    public ReactiveCommand<object, Unit> SlideClickCommand { get; }

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
            Playlist.Designs.Add(new BaseSlideTheme() { Name = "Default" });

            return;
        }

        settings = new ConfigurationBuilder<IMySettings>()
            .UseJsonFile("HandsLiftedApp.UserConfig.json")
            .Build();

        SlideClickCommand = ReactiveCommand.CreateFromTask<object>(OnSlideClickCommand);

        // The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
        ShowOpenFileDialog = new Interaction<Unit, string[]?>();
        
        EditSlideInfoCommand = ReactiveCommand.CreateFromTask<object>(async (object x) =>
        {
            if (x is CustomAxamlSlideInstance c)
            {
                SlideInfoEditorWindow window = new SlideInfoEditorWindow() { DataContext = c.parentMediaItem.Meta };
                window.Show();
            }
        });
        
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
        // TODO
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

        MessageBus.Current.Listen<AddItemMessage>()
            .Subscribe(async addItemMessage =>
            {
                Item? itemToInsert = null;
                switch (addItemMessage.Type)
                {
                    case AddItemMessage.AddItemType.Presentation:
                        var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);
                        if (filePaths.Length > 0)
                        {
                            itemToInsert = CreateItem.OpenPresentationFile(filePaths[0], Playlist);
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
                        var song = new SongItemInstance(Playlist);
                        itemToInsert = song;
                        SongEditorViewModel vm = new SongEditorViewModel(song, Playlist);
                        SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
                        seq.Show();
                        break;
                    case AddItemMessage.AddItemType.MediaGroup:
                        filePaths = await ShowOpenFileDialog.Handle(Unit.Default); // TODO pass accepted file types list
                        MediaGroupItemInstance mediaGroupItem = new MediaGroupItemInstance(Playlist)
                            { Title = "New media group" };

                        foreach (var filePath in filePaths)
                        {
                            if (filePath != null && filePath is string)
                            {
                                DateTime now = DateTime.Now;
                                string fileName = Path.GetFileName(filePath);
                                string folderName = Path.GetDirectoryName(filePath);
                                mediaGroupItem.Items.Add(new MediaGroupItem.MediaItem()
                                    { SourceMediaFilePath = filePath });
                            }
                        }
                        mediaGroupItem.GenerateSlides();

                        itemToInsert = mediaGroupItem;

                        break;
                    case AddItemMessage.AddItemType.Comment:
                        itemToInsert = new CommentItem();
                        break;
                    case AddItemMessage.AddItemType.BibleReadingSlideGroup:
                        filePaths = await ShowOpenFileDialog.Handle(Unit.Default); // TODO pass accepted file types list
                        MediaGroupItemInstance mediaGroupItem2 = new MediaGroupItemInstance(Playlist)
                            { Title = "New media group" };

                        foreach (var filePath in filePaths)
                        {
                            if (filePath != null && filePath is string)
                            {
                                DateTime now = DateTime.Now;
                                string fileName = Path.GetFileName(filePath);
                                string folderName = Path.GetDirectoryName(filePath);
                                mediaGroupItem2.Items.Add(new MediaGroupItem.MediaItem()
                                    { SourceMediaFilePath = filePath } );
                            }
                        }
                        mediaGroupItem2.GenerateSlides();

                        itemToInsert = mediaGroupItem2;
                        break;
                    default:
                        Debug.Print($"Unknown AddItemType: [${addItemMessage.Type}]");
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
                }

                if (currentSelectedItem != null)
                {
                    Playlist.SelectedItemIndex = Playlist.Items.IndexOf(currentSelectedItem);
                }
            });

        MessageBus.Current.Listen<MoveItemMessage>()
            .Subscribe((moveItemMessage) =>
            {
                var currentSelectedItem = Playlist.SelectedItem;

                Playlist.Items.Move(moveItemMessage.SourceIndex, moveItemMessage.DestinationIndex);
                
                if (currentSelectedItem != null)
                {
                    Playlist.SelectedItemIndex = Playlist.Items.IndexOf(currentSelectedItem);
                }
                Debug.Print(
                    $"Moving playlist item {moveItemMessage.SourceIndex} to {moveItemMessage.DestinationIndex}");
            });

        Playlist = new PlaylistInstance();

        // Open reading stream from the first file.
        if (settings.LastOpenedPlaylistFullPath != null)
        {
            try
            {
                var parsedPlaylist = HandsLiftedDocXmlSerializer.DeserializePlaylist(settings.LastOpenedPlaylistFullPath);
                Playlist.Dispose();
                Playlist = parsedPlaylist;
                Playlist.IsDirty = false;
            }
            catch (Exception e)
            {
                Log.Error($"[DOC] Failed to parse playlist XML: [{settings.LastOpenedPlaylistFullPath}]");
                Console.WriteLine(e);
                // ignored
            }
        }
        
        _ = Update(); // calling an async function we do not want to await
    }

    public Interaction<Unit, string[]?> ShowOpenFileDialog { get; }
    public ReactiveCommand<object, Unit> EditSlideInfoCommand { get; }
    public ReactiveCommand<object, Unit> SlideSplitFromHere { get; }

    private PlaylistInstance _playlist;
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

    private async Task OnSlideClickCommand(object? ac)
    {
        ReadOnlyCollection<object> args = ac as ReadOnlyCollection<object>;
        Slide slide = (Slide)args[0];
        Item item = (Item)args[1];

        int itemIndex = Playlist.Items.IndexOf(item);

        int SlideIndex = item.GetAsIItemInstance().Slides.IndexOf(slide);
        Log.Information($"OnSlideClickCommand item=[{item.Title}] slide=[{SlideIndex}]");

        Playlist.NavigateToReference(new SlideReference()
            { SlideIndex = SlideIndex, ItemIndex = itemIndex, Slide = slide });
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

        if (ProjectorWindow == null)
        {
            ProjectorWindow = new ProjectorWindow();
            ProjectorWindow.DataContext = this;
        }
        ToggleProjectorWindow(Globals.Instance.AppPreferences.OnStartupShowOutput);
        
        if (StageDisplayWindow == null)
        {
            StageDisplayWindow = new StageDisplayWindow();
            StageDisplayWindow.DataContext = this;
        }
        ToggleStageDisplayWindow(Globals.Instance.AppPreferences.OnStartupShowStage);
    }

    public void OnProjectorClickCommand()
    {
        ToggleProjectorWindow();
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
            WindowUtils.RestoreWindowBounds(ProjectorWindow, Globals.Instance.AppPreferences.OutputDisplayBounds);
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
        ToggleStageDisplayWindow();
    }

    public void ToggleStageDisplayWindow(bool? shouldShow = null)
    {
        shouldShow = shouldShow ?? (StageDisplayWindow == null || !StageDisplayWindow.IsVisible);
        if (shouldShow == true)
        {
            StageDisplayWindow = new StageDisplayWindow() { DataContext = this };
            StageDisplayWindow.Show();
            WindowUtils.RestoreWindowBounds(StageDisplayWindow, Globals.Instance.AppPreferences.StageDisplayBounds);
        }
        else
        {
            if (StageDisplayWindow != null)
                StageDisplayWindow.Hide();
        }
    }

    #region UI

    private int _BottomLeftPanelSelectedTabIndex = 0;
    public int BottomLeftPanelSelectedTabIndex { get => _BottomLeftPanelSelectedTabIndex; set => this.RaiseAndSetIfChanged(ref _BottomLeftPanelSelectedTabIndex, value); }

    #endregion
}