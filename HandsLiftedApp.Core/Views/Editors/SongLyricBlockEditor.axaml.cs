using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Views.Editors;

public partial class SongLyricBlockEditor : UserControl
{
    public SongLyricBlockEditor()
    {
        InitializeComponent();
    }
    public void DeleteThisPartClick(object? sender, RoutedEventArgs args)
    {
        SongStanza stanza = (SongStanza)((Control)sender).DataContext;
        if (this.DataContext is SongEditorViewModel viewModel)
        {
            viewModel.Song.Stanzas.Remove(stanza);
        }
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.DataContext is SongEditorViewModel viewModel)
        {
            viewModel.Song.Stanzas.Add(new SongStanza(Guid.NewGuid(), "", ""));
        }
    }
    
    
    private void MoveUp_OnClick(object? sender, RoutedEventArgs e)
    {
        // 6 [0, 1, 2, 3, 4, 5]
        if (StanzaArrangementListBox.SelectedIndex > -1 && StanzaArrangementListBox.SelectedIndex > 0)
        {
            if (this.DataContext is SongEditorViewModel viewModel)
            {
                viewModel.Song.Stanzas.Move(StanzaArrangementListBox.SelectedIndex,
                    StanzaArrangementListBox.SelectedIndex - 1);
            }
        }
    }

    private void MoveDown_OnClick(object? sender, RoutedEventArgs e)
    {
        // 6 [0, 1, 2, 3, 4, 5]
        if (StanzaArrangementListBox.SelectedIndex > -1 &&
            StanzaArrangementListBox.SelectedIndex < StanzaArrangementListBox.Items.Count - 1)
        {
            if (this.DataContext is SongEditorViewModel viewModel)
            {
                viewModel.Song.Stanzas.Move(StanzaArrangementListBox.SelectedIndex,
                    StanzaArrangementListBox.SelectedIndex + 1);
            }
        }
    }

}