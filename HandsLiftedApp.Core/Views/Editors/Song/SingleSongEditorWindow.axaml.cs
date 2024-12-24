using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.Views.Editors.Song
{
    public partial class SingleSongEditorWindow : Window
    {
        public SingleSongEditorWindow()
        {
            InitializeComponent();
        }
        
        public void OnAddPartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (SongStanza)((Control)sender).DataContext;
            ((SongEditorViewModel)this.DataContext).Song.Arrangement.Add(clickedStanza.Id);
        }


    }
}