using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Utils;
using System;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
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
            var stanza = (SongStanza)((Control)sender).DataContext;
            var m = new SongItemInstance.Ref<SongStanza>() { Value = stanza };
            ((SongEditorViewModel)this.DataContext).Song.Arrangement.Add(m);
        }

        public void OnRepeatPartClick(object? sender, RoutedEventArgs args)
        {
            SongItemInstance.Ref<SongStanza> stanza = (SongItemInstance.Ref<SongStanza>)((Control)sender).DataContext;

            SongItemInstance.Ref<SongStanza> clonedStanza = new SongItemInstance.Ref<SongStanza> { Value = stanza.Value };

            var lastIndex = ((SongEditorViewModel)this.DataContext).Song.Arrangement.IndexOf(stanza);

            ((SongEditorViewModel)this.DataContext).Song.Arrangement.Insert(lastIndex + 1, clonedStanza);
        }

        public void OnRemovePartClick(object? sender, RoutedEventArgs args)
        {
            SongItemInstance.Ref<SongStanza> stanza = (SongItemInstance.Ref<SongStanza>)((Control)sender).DataContext;

            ((SongEditorViewModel)this.DataContext).Song.Arrangement.Remove(stanza);
        }

        public void DeleteThisPartClick(object? sender, RoutedEventArgs args)
        {
            SongStanza stanza = (SongStanza)((Control)sender).DataContext;
            ((SongEditorViewModel)this.DataContext).Song.Stanzas.Remove(stanza);
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
