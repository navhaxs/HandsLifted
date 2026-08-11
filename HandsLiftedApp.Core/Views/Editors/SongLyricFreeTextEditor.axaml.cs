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
            songEditorViewModel.ParseAndLoadFromFreeText();
        }
    }
}