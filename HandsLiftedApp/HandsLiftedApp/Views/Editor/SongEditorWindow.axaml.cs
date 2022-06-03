using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Documents;
using HandsLiftedApp.Utils;
using HandsLiftedApp.ViewModels.Editor;
using System.Threading.Tasks;

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
                ((SongEditorViewModel) this.DataContext).song = SongImporter.ImportSongFromTxt(result[0]);
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
                //_wavFileSplitter.GetWavHeader(result, text => _textOutput.Text = text);
                XmlSerialization.WriteToXmlFile<Song>(result, ((SongEditorViewModel)this.DataContext).song);
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
                //_wavFileSplitter.GetWavHeader(result, text => _textOutput.Text = text);
                ((SongEditorViewModel)this.DataContext).song = XmlSerialization.ReadFromXmlFile<Song>(result[0]);
            }
        }
    }
}
