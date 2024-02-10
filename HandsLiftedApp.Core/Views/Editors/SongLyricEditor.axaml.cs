using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;
using SongStanza = HandsLiftedApp.Core.Models.RuntimeData.Items.SongStanza;

namespace HandsLiftedApp.Core.Views.Editors;

public partial class SongLyricEditor : UserControl
{
    public SongLyricEditor()
    {
        InitializeComponent();
    }
    public void DeleteThisPartClick(object? sender, RoutedEventArgs args)
    {
        SongStanza stanza = (SongStanza)((Control)sender).DataContext;
        ((SongItemInstance)this.DataContext).Stanzas.Remove(stanza);
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.DataContext is SongItemInstance song)
        {
            song.Stanzas.Add(new SongStanza(Guid.NewGuid(), "", ""));
        }
    }


    // private void EntryModeToggleButton_OnClick(object? sender, RoutedEventArgs e)
    // {
    //     DataEntryCarousel.SelectedIndex = (EntryModeToggleButton.IsChecked == true) ? 1 : 0;
    // }

    private void SyncButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.DataContext is SongEditorViewModel songEditorViewModel)
        {
            var imported = SongImporter.createSongItemFromStringData(songEditorViewModel.FreeTextEntryField);
            imported.UUID = songEditorViewModel.Song.UUID;
            songEditorViewModel.Song = imported;
        }
    }
}