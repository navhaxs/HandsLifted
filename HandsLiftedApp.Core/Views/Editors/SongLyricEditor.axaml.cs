using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Data.Models.Items;

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
        ((SongItem)this.DataContext).Stanzas.Remove(stanza);
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.DataContext is SongItem song)
        {
            song.Stanzas.Add(new SongStanza(Guid.NewGuid(), "", ""));
        }
    }
}