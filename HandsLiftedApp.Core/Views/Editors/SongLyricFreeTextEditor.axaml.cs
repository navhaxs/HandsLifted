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

    private void SyncButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SongEditorViewModel songEditorViewModel)
        {
            var imported = SongImporter.createSongItemFromStringData(songEditorViewModel.FreeTextEntryField);
            imported.UUID = songEditorViewModel.Song.UUID;
            imported.Design = songEditorViewModel.Song.Design;
            songEditorViewModel.Song.ReplaceWith(imported);
        }
    }
}