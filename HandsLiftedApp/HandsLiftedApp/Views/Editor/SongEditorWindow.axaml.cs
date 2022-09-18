using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.SlideState;
using HandsLiftedApp.Utils;
using HandsLiftedApp.ViewModels.Editor;
using System.Threading.Tasks;
using ReactiveUI;
using Avalonia.Interactivity;
using HandsLiftedApp.Models.ItemState;

namespace HandsLiftedApp.Views.Editor
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

            _btnGetWavHeader = this.FindControl<MenuItem>("btnGetWavHeader");
            _btnGetWavHeader.Click += async (sender, e) => await GetWavHeader();
            _btnFileLoad = this.FindControl<MenuItem>("btnFileLoad");
            _btnFileLoad.Click += async (sender, e) => await LoadFromXML();
            _btnFileSave = this.FindControl<MenuItem>("btnFileSave");
            _btnFileSave.Click += async (sender, e) => await SaveToXML();

            // TODO skip caret between textbox
            //TextBox tb = new TextBox();
            //tb.WhenAny
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
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
                ((SongEditorViewModel)this.DataContext).song = SongImporter.ImportSongFromTxt(result[0]);
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
                XmlSerialization.WriteToXmlFile<SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>>(result, ((SongEditorViewModel)this.DataContext).song);
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
                ((SongEditorViewModel)this.DataContext).song = XmlSerialization.ReadFromXmlFile<SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>>(result[0]);
            }
        }

        public void DeleteThisPartClick(object? sender, RoutedEventArgs args)
        {
            SongStanza stanza = (SongStanza)((Control)sender).DataContext;
            ((SongEditorViewModel)this.DataContext).song.Stanzas.Remove(stanza);
        }

    }
}
