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
        private MenuItem _btnGetWavHeader;
        private MenuItem _btnFileLoad;
        private MenuItem _btnFileSave;
        public SongEditorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            //_btnGetWavHeader = this.FindControl<MenuItem>("btnGetWavHeader");
            //_btnGetWavHeader.Click += async (sender, e) => await GetWavHeader();
            //_btnFileLoad = this.FindControl<MenuItem>("btnFileLoad");
            //_btnFileLoad.Click += async (sender, e) => await LoadFromXML();
            //_btnFileSave = this.FindControl<MenuItem>("btnFileSave");
            //_btnFileSave.Click += async (sender, e) => await SaveToXML();

            // TODO skip caret between textbox
            //TextBox tb = new TextBox();
            //tb.WhenAny

            // ImportPasteHereTextBox.TextChanged += ImportPasteHereTextBox_TextChanged;
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

        // private void ImportPasteHereTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        // {
        //     try
        //     {
        //         SongItemInstance songItemInstance = SongImporter.createSongItemFromStringData(ImportPasteHereTextBox.Text);
        //
        //         ((SongEditorViewModel)this.DataContext).Song.ReplaceWith(songItemInstance);
        //     }
        //     catch (Exception ex)
        //     {
        //
        //     }
        // }

        private async Task GetWavHeader()
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "TXT Files", Extensions = { "txt" } });
            dlg.Filters.Add(new FileDialogFilter() { Name = "All Files", Extensions = { "*" } });
            dlg.AllowMultiple = false;

            var window = this.VisualRoot as Window;
            if (window is null)
            {
                return;
            }

            var result = await dlg.ShowAsync(window);
            if (result != null)
            {
                //_wavFileSplitter.GetWavHeader(result, text => _textOutput.Text = text);
                ((SongEditorViewModel)this.DataContext).Song = SongImporter.createSongItemFromTxtFile(result[0]);
            }
        }

        // private async Task SaveToXML()
        // {
        //     var dlg = new SaveFileDialog();
        //     dlg.Filters.Add(new FileDialogFilter() { Name = "XML Files", Extensions = { "xml" } });
        //     dlg.Filters.Add(new FileDialogFilter() { Name = "All Files", Extensions = { "*" } });
        //
        //     var window = this.VisualRoot as Window;
        //     if (window is null)
        //     {
        //         return;
        //     }
        //
        //     var result = await dlg.ShowAsync(window);
        //     if (result != null)
        //     {
        //         XmlSerialization.WriteToXmlFile<SongItemInstance>(result, ((SongEditorViewModel)this.DataContext).Song);
        //     }
        // }

        // private async Task LoadFromXML()
        // {
        //     var dlg = new OpenFileDialog();
        //     dlg.Filters.Add(new FileDialogFilter() { Name = "XML Files", Extensions = { "xml" } });
        //     dlg.Filters.Add(new FileDialogFilter() { Name = "All Files", Extensions = { "*" } });
        //     dlg.AllowMultiple = false;
        //
        //     var window = this.VisualRoot as Window;
        //     if (window is null)
        //     {
        //         return;
        //     }
        //
        //     var result = await dlg.ShowAsync(window);
        //     if (result != null)
        //     {
        //         ((SongEditorViewModel)this.DataContext).Song = XmlSerialization.ReadFromXmlFile<SongItemInstance>(result[0]);
        //     }
        // }

        public void DeleteThisPartClick(object? sender, RoutedEventArgs args)
        {
            SongStanza stanza = (SongStanza)((Control)sender).DataContext;
            ((SongEditorViewModel)this.DataContext).Song.Stanzas.Remove(stanza);
        }

        private void GenerateSlides_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                // songEditorViewModel.Song.GenerateSlides();
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
