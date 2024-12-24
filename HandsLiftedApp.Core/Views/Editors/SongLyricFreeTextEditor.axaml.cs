using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.ViewModels.Editor;

namespace HandsLiftedApp.Core.Views.Editors;

public partial class SongLyricFreeTextEditor : UserControl
{
    public SongLyricFreeTextEditor()
    {
        InitializeComponent();
    }
    
        private void ParseAndLoadFromText_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                var text = songEditorViewModel.FreeTextEntryField;
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
                        !matchingStanzas.Contains(existingStanza.Name)).ToList();
                foreach (var removedSongStanza in removedSongStanzas)
                {
                    songEditorViewModel.Song.Stanzas.Remove(removedSongStanza);
                    while (songEditorViewModel.Song.Arrangement.Contains(removedSongStanza.Id))
                    {
                        songEditorViewModel.Song.Arrangement.Remove(removedSongStanza.Id);
                    }
                }

                songEditorViewModel.Song.Title = songItemFromStringData.Title;
                songEditorViewModel.Song.Copyright = songItemFromStringData.Copyright;

                if (songEditorViewModel.Song.Arrangement.Count == 0)
                {
                    songEditorViewModel.Song.ResetArrangement();
                }

                // songEditorViewModel.LyricEntryMode = false;
            }
        }

}