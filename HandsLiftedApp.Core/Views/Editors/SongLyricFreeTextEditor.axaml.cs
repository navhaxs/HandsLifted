using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Views.Editors;

public partial class SongLyricFreeTextEditor : UserControl
{
    public SongLyricFreeTextEditor()
    {
        InitializeComponent();
    }

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