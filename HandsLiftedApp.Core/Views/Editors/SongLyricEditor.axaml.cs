using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core;

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
}