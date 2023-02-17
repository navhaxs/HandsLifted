using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;

namespace HandsLifted.FetchBible.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif


            //var _textEditor = this.FindControl<TextEditor>("Editor");
            //_textEditor.Document = new TextDocument("hello");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public async void x()
        {
            var x = "https://collection.fetch.bible/bibles/manifest.json";
            var y = "https://collection.fetch.bible/bibles/{id}/usx/{book}.usx";

            HttpClient httpClient = new HttpClient();
            //await httpClient.GetFromJsonAsync<IList<WeatherForecast>>(y);
        }
    }
}
