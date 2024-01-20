using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Utils;
using System;
using System.Threading.Tasks;

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

            ImportPasteHereTextBox.TextChanged += ImportPasteHereTextBox_TextChanged;
        }

        public void OnAddPartClick(object? sender, RoutedEventArgs args)
        {
            var stanza = (SongStanza)((Control)sender).DataContext;
            var m = new SongItem.Ref<Data.Models.Items.SongStanza>() { Value = stanza };
            ((SongEditorViewModel)this.DataContext).song.Arrangement.Add(m);
        }

        public void OnRepeatPartClick(object? sender, RoutedEventArgs args)
        {
            SongItem.Ref<SongStanza> stanza = (SongItem.Ref<SongStanza>)((Control)sender).DataContext;

            SongItem.Ref<SongStanza> clonedStanza = new SongItem.Ref<SongStanza> { Value = stanza.Value };

            var lastIndex = ((SongEditorViewModel)this.DataContext).song.Arrangement.IndexOf(stanza);

            ((SongEditorViewModel)this.DataContext).song.Arrangement.Insert(lastIndex + 1, clonedStanza);
        }

        public void OnRemovePartClick(object? sender, RoutedEventArgs args)
        {
            SongItem.Ref<SongStanza> stanza = (SongItem.Ref<SongStanza>)((Control)sender).DataContext;

            ((SongEditorViewModel)this.DataContext).song.Arrangement.Remove(stanza);
        }

        private void ImportPasteHereTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                SongItem songItem = SongImporter.createSongItemFromStringData(ImportPasteHereTextBox.Text);

                ((SongEditorViewModel)this.DataContext).song.ReplaceWith(songItem);
            }
            catch (Exception ex)
            {

            }
        }

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
                ((SongEditorViewModel)this.DataContext).song = SongImporter.createSongItemFromTxtFile(result[0]);
            }
        }

        private async Task SaveToXML()
        {
            var dlg = new SaveFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "XML Files", Extensions = { "xml" } });
            dlg.Filters.Add(new FileDialogFilter() { Name = "All Files", Extensions = { "*" } });

            var window = this.VisualRoot as Window;
            if (window is null)
            {
                return;
            }

            var result = await dlg.ShowAsync(window);
            if (result != null)
            {
                XmlSerialization.WriteToXmlFile<SongItem>(result, ((SongEditorViewModel)this.DataContext).song);
            }
        }

        private async Task LoadFromXML()
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "XML Files", Extensions = { "xml" } });
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
                ((SongEditorViewModel)this.DataContext).song = XmlSerialization.ReadFromXmlFile<SongItem>(result[0]);
            }
        }

        public void DeleteThisPartClick(object? sender, RoutedEventArgs args)
        {
            SongStanza stanza = (SongStanza)((Control)sender).DataContext;
            ((SongEditorViewModel)this.DataContext).song.Stanzas.Remove(stanza);
        }

    }
}
