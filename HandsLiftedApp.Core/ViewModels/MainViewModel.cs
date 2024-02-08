using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.ViewModels;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using Config.Net;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.Core.ViewModels;

public class MainViewModel : ViewModelBase
{
    public IMySettings settings;

    public MainViewModel()
    {
        settings = new ConfigurationBuilder<IMySettings>()
            .UseJsonFile("HandsLiftedApp.UserConfig.json")
            .Build();

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
                            await CreateItem.OpenPresentationFileAsync(path, CurrentPlaylist);
                        }

                        break;
                    case AddItemMessage.AddItemType.Logo:
                        CurrentPlaylist.Playlist.Items.Add(new LogoItem());
                        break;
                    case AddItemMessage.AddItemType.SectionHeading:
                        CurrentPlaylist.Playlist.Items.Add(new SectionHeadingItem());
                        break;
                    case AddItemMessage.AddItemType.BlankGroup:
                        CurrentPlaylist.Playlist.Items.Add(new SlidesGroupItem());
                        break;
                    case AddItemMessage.AddItemType.NewSong:
                        var song = new SongItem();
                        CurrentPlaylist.Playlist.Items.Add(song);
                        SongEditorViewModel vm = new SongEditorViewModel() { song = song };
                        SongEditorWindow seq = new SongEditorWindow() { DataContext = vm };
                        seq.Show();
                        break;
                    case AddItemMessage.AddItemType.MediaGroup:
                        filePaths = await ShowOpenFileDialog.Handle(Unit.Default);
                        SlidesGroupItem slidesGroup = new SlidesGroupItem() { Title = "New media group" };

                        CurrentPlaylist.Playlist.Items.Add(slidesGroup);
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
                var theSelectedIndex = CurrentPlaylist.Playlist.Items.IndexOf(moveItemCommand.SourceItem);

                switch (moveItemCommand.Direction)
                {
                    case MoveItemCommand.DirectionValue.UP:
                        CurrentPlaylist.Playlist.Items.Move(theSelectedIndex, theSelectedIndex-1);
                        break;
                    case MoveItemCommand.DirectionValue.DOWN:
                        CurrentPlaylist.Playlist.Items.Move(theSelectedIndex, theSelectedIndex+1);
                        break;
                    case MoveItemCommand.DirectionValue.REMOVE:
                        CurrentPlaylist.Playlist.Items.RemoveAt(theSelectedIndex);
                        break;
                }

            });

        MessageBus.Current.Listen<MoveItemMessage>()
            .Subscribe((moveItemMessage) =>
            {

                // var theSelectedItem = CurrentPlaylist.Playlist.State.SelectedItem;

                CurrentPlaylist.Playlist.Items.Move(moveItemMessage.SourceIndex, moveItemMessage.DestinationIndex);

                // Is this working??
                // var theSelectedIndex = CurrentPlaylist.Playlist.Items.IndexOf(theSelectedItem);

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
                using var stream = File.Open(settings.LastOpenedPlaylistFullPath, FileMode.Open);
                CurrentPlaylist.Playlist = XmlSerialization.ReadFromXmlFile<Playlist>(stream);
            }
            catch (Exception e)
            {
                // ignored
            }
        }

    }

    public Interaction<Unit, string[]?> ShowOpenFileDialog { get; }

#pragma warning disable CA1822 // Mark members as static
    public string Greeting => "Welcome to the VisionScreens Web Editor! :D";

    public PlaylistInstance CurrentPlaylist { get; set; } = new PlaylistInstance();
#pragma warning restore CA1822 // Mark members as static
}