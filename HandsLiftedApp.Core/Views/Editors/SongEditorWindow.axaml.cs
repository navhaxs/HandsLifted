using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class SongEditorWindow : Window
    {
        public SongEditorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                songEditorViewModel.FreeTextEntryField = SongImporter.songItemToFreeText(songEditorViewModel.Song);

                songEditorViewModel.WhenAnyValue(x => x.Song.Slides).Subscribe((slides) =>
                {
                    songEditorViewModel.FreeTextEntryField = SongImporter.songItemToFreeText(songEditorViewModel.Song);
                });
            }
            
        }

        public void OnAddPartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (SongStanza)((Control)sender).DataContext;
            ((SongEditorViewModel)this.DataContext).Song.Arrangement.Add(clickedStanza.Id);
        }

        public void OnRepeatPartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (SongItemInstance.ArrangementRef)((Control)sender).DataContext;
            var lastIndex = ((SongEditorViewModel)this.DataContext).Song.Arrangement.IndexOf(clickedStanza.SongStanza.Id);
            ((SongEditorViewModel)this.DataContext).Song.Arrangement.Insert(lastIndex + 1, clickedStanza.SongStanza.Id);
        }

        public void OnRemovePartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (SongItemInstance.ArrangementRef)((Control)sender).DataContext;
            var lastIndex = ((SongEditorViewModel)this.DataContext).Song.Arrangement.IndexOf(clickedStanza.SongStanza.Id);
            ((SongEditorViewModel)this.DataContext).Song.Arrangement.RemoveAt(lastIndex);
        }

        private void GenerateSlides_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                songEditorViewModel.Song.GenerateSlides();
            }
        }

        private void ReverseSyncButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                songEditorViewModel.FreeTextEntryField = SongImporter.songItemToFreeText(songEditorViewModel.Song);
            }
        }
    }
}
