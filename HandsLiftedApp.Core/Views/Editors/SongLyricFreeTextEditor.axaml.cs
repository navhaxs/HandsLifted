using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DebounceThrottle;
using DynamicData;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Views.Editors;

public partial class SongLyricFreeTextEditor : UserControl
{
    public SongLyricFreeTextEditor()
    {
        InitializeComponent();
    }

    private DebounceDispatcher debounceDispatcher = new(600);

    // private void SyncButton_OnClick(object? sender, RoutedEventArgs e)
    // {
    //     if (DataContext is SongEditorViewModel songEditorViewModel)
    //     {
    //         var imported = SongImporter.CreateSongItemFromStringData(songEditorViewModel.FreeTextEntryField);
    //         imported.UUID = songEditorViewModel.Song.UUID;
    //         imported.Design = songEditorViewModel.Song.Design;
    //         songEditorViewModel.Song.ReplaceWith(imported);
    //     }
    // }

    private bool FirstLoad = true;

    private void TextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var raw = FreeTextBox.Text;

        if (raw.Length > 0)
        {
            if (FirstLoad)
            {
                FirstLoad = false;
                return;
            }

            debounceDispatcher.Debounce(() => ParseAndLoadFreeText(raw));
        }
    }

    private void ParseAndLoadFreeText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is SongEditorViewModel songEditorViewModel)
            {
                // TODO refactor to just return Stanzas
                var songItemFromStringData = SongImporter.CreateSongItemFromStringData(text);
                List<string> matchingStanzas = new();
                foreach (var newStanza in songItemFromStringData.Stanzas)
                {
                    matchingStanzas.Add(newStanza.Name);
                    var firstOrDefault =
                        songEditorViewModel.Song.Stanzas.FirstOrDefault(existingStanza =>
                            existingStanza.Name == newStanza.Name);

                    if (firstOrDefault != null)
                    {
                        firstOrDefault.Lyrics = newStanza.Lyrics;
                    }
                    else
                    {
                        songEditorViewModel.Song.Stanzas.Add(newStanza);
                    }
                }

                var removedSongStanzas =
                    songEditorViewModel.Song.Stanzas.Where(existingStanza =>
                        !matchingStanzas.Contains(existingStanza.Name));
                foreach (var removedSongStanza in removedSongStanzas)
                {
                    songEditorViewModel.Song.Stanzas.Remove(removedSongStanza);
                    while (songEditorViewModel.Song.Arrangement.Contains(removedSongStanza.Id))
                    {
                        songEditorViewModel.Song.Arrangement.Remove(removedSongStanza.Id);
                    }
                }
            }
        });
    }
}