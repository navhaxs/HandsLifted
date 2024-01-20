using HandsLiftedApp.Controls.Messages;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.ViewModels;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;

namespace HandsLiftedApp.Core.ViewModels;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
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
                    default:
                        Debug.Print($"Unknown AddItemType: [${addItemMessage.Type}]");
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
    }

    public Interaction<Unit, string[]?> ShowOpenFileDialog { get; }

#pragma warning disable CA1822 // Mark members as static
    public string Greeting => "Welcome to the VisionScreens Web Editor! :D";

    public PlaylistInstance CurrentPlaylist { get; set; } = new PlaylistInstance();
#pragma warning restore CA1822 // Mark members as static
}