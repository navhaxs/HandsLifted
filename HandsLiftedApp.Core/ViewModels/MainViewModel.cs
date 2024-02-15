using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.ViewModels;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using ByteSizeLib;
using Config.Net;
using DynamicData;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Utils;
using Serilog;

namespace HandsLiftedApp.Core.ViewModels;

public class MainViewModel : ViewModelBase
{
    public IMySettings settings;
    public ReactiveCommand<object, Unit> SlideClickCommand { get; }

    public MainViewModel()
    {

        if (Design.IsDesignMode)
        {
            Playlist = new PlaylistInstance();
            Playlist.Items.Add(new SongItemInstance());
            Playlist.Items.Add(new LogoItemInstance());
            Playlist.Designs.Add(new BaseSlideTheme() {Name = "Default"});

            return;
        }
        
        settings = new ConfigurationBuilder<IMySettings>()
            .UseJsonFile("HandsLiftedApp.UserConfig.json")
            .Build();

        SlideClickCommand = ReactiveCommand.CreateFromTask<object>(OnSlideClickCommand);
        
        // The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
        ShowOpenFileDialog = new Interaction<Unit, string[]?>();

        MessageBus.Current.Listen<AddItemMessage>()
            .Subscribe(async addItemMessage =>
            {
                switch (addItemMessage.Type)
                {
                    case AddItemMessage.AddItemType.Presentation:
                        var filePaths = await ShowOpenFileDialog.Handle(Unit.Default);
                        foreach (var path in filePaths)
                        {
                            await CreateItem.OpenPresentationFileAsync(path, Playlist);
                        }

                        break;
                    case AddItemMessage.AddItemType.Logo:
                        Playlist.Items.Add(new LogoItemInstance());
                        break;
                    case AddItemMessage.AddItemType.SectionHeading:
                        Playlist.Items.Add(new SectionHeadingItem());
                        break;
                    case AddItemMessage.AddItemType.BlankGroup:
                        Playlist.Items.Add(new SlidesGroupItem());
                        break;
                    case AddItemMessage.AddItemType.NewSong:
                        var song = new SongItemInstance();
                        Playlist.Items.Add(song);
                        SongEditorViewModel vm = new SongEditorViewModel() { Song = song };
                        SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
                        seq.Show();
                        break;
                    case AddItemMessage.AddItemType.MediaGroup:
                        filePaths = await ShowOpenFileDialog.Handle(Unit.Default); // TODO pass accepted file types list
                        SlidesGroupItem slidesGroup = new SlidesGroupItem() { Title = "New media group" };

                        Playlist.Items.Add(slidesGroup);
                        foreach (var filePath in filePaths)
                        {
                            if (filePath != null && filePath is string)
                            {

                                DateTime now = DateTime.Now;
                                string fileName = Path.GetFileName(filePath);
                                string folderName = Path.GetDirectoryName(filePath);
                                //slidesGroup.Items.Add(PlaylistUtils.GenerateMediaContentSlide(filePath));
                            }
                        }
                        break;
                    default:
                        Debug.Print($"Unknown AddItemType: [${addItemMessage.Type}]");
                        break;
                }

            });

        MessageBus.Current.Listen<MoveItemCommand>()
            .Subscribe((moveItemCommand) =>
            {
                var theSelectedIndex = Playlist.Items.IndexOf(moveItemCommand.SourceItem);

                switch (moveItemCommand.Direction)
                {
                    case MoveItemCommand.DirectionValue.UP:
                        Playlist.Items.Move(theSelectedIndex, theSelectedIndex-1);
                        break;
                    case MoveItemCommand.DirectionValue.DOWN:
                        Playlist.Items.Move(theSelectedIndex, theSelectedIndex+1);
                        break;
                    case MoveItemCommand.DirectionValue.REMOVE:
                        Playlist.Items.RemoveAt(theSelectedIndex);
                        break;
                }

            });

        MessageBus.Current.Listen<MoveItemMessage>()
            .Subscribe((moveItemMessage) =>
            {

                // var theSelectedItem = CurrentPlaylist.State.SelectedItem;

                Playlist.Items.Move(moveItemMessage.SourceIndex, moveItemMessage.DestinationIndex);

                // Is this working??
                // var theSelectedIndex = CurrentPlaylist.Items.IndexOf(theSelectedItem);

                Debug.Print($"Moving playlist item {moveItemMessage.SourceIndex} to {moveItemMessage.DestinationIndex}");

                // TODO we MUST update SelectedItemIndex
                //
                // if (x.SourceIndex == Playlist.State.SelectedItemIndex)
                // {
                //     Debug.Print($"Updating the SelectedItemIndex from {Playlist.State.SelectedItemIndex} to {x.DestinationIndex}");
                //     Playlist.State.SelectedItemIndex = x.DestinationIndex;
                // }
                // else
                // {
                //     Debug.Print($"Updating the SelectedItemIndex to {theSelectedIndex}");
                //
                //     // the selected item index is now incorrect because the list has been shuffled.
                //     Playlist.State.SelectedItemIndex = theSelectedIndex;
                // }
                //
                // // HACK run me from different thread. gives time for UI to update first
                // new Thread(() =>
                // {
                //     Thread.CurrentThread.IsBackground = true;
                //     Thread.Sleep(100);
                //     Dispatcher.UIThread.InvokeAsync(() =>
                //     {
                //         MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = x.DestinationIndex });
                //     });
                // }).Start();
            });

        // Open reading stream from the first file.
        if (settings.LastOpenedPlaylistFullPath != null)
        {
            try
            {
                var x = XmlSerializerForDummies.DeserializePlaylist(settings.LastOpenedPlaylistFullPath);
                Playlist = x;
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

    private PlaylistInstance _playlist = new PlaylistInstance();
    public PlaylistInstance Playlist { get => _playlist; set => this.RaiseAndSetIfChanged(ref _playlist, value); }
    
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
    public bool IsDisplayDebugInfo { get => _IsDisplayDebugInfo; set => this.RaiseAndSetIfChanged(ref _IsDisplayDebugInfo, value); }
    private string _debugInfoText = string.Empty;
    public string DebugInfoText { get => _debugInfoText; set => this.RaiseAndSetIfChanged(ref _debugInfoText, value); }
    
    private async Task OnSlideClickCommand(object? ac)
    {
        ReadOnlyCollection<object> args = ac as ReadOnlyCollection<object>;
        Slide slide = (Slide)args[0];
        Item item = (Item)args[1];

        int itemIndex = Playlist.Items.IndexOf(item);

        if (Playlist.PresentationState != PlaylistInstance.PresentationStateEnum.Slides)
        {
            Playlist.PresentationState = PlaylistInstance.PresentationStateEnum.Slides;
        }

        int SlideIndex = item.Slides.IndexOf(slide);
        Log.Information($"OnSlideClickCommand item=[{item.Title}] slide=[{SlideIndex}]");

        Playlist.NavigateToReference(new SlideReference() { SlideIndex = SlideIndex, ItemIndex = itemIndex, Slide = slide });
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

                    DebugInfoText = $"{ByteSize.FromBytes(memory).ToString("#.#")} {ByteSize.FromBytes(memoryUsed).ToString("#.#")}";
                }
            }
        }
    }
    
    private ProjectorWindow _projectorWindow;
    public ProjectorWindow ProjectorWindow { get => _projectorWindow; set => this.RaiseAndSetIfChanged(ref _projectorWindow, value); }
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
        }
        else
        {
            if (ProjectorWindow != null)
                ProjectorWindow.Hide();
        }
    }
    
    private StageDisplayWindow _stageDisplayWindow;
    public StageDisplayWindow StageDisplayWindow { get => _stageDisplayWindow; set => this.RaiseAndSetIfChanged(ref _stageDisplayWindow, value); }
    
    public void OnStageDisplayClickCommand()
    {
        ToggleStageDisplayWindow();
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
                StageDisplayWindow.Hide();
        }
    }
}