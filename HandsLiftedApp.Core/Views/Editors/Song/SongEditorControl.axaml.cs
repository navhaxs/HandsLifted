using System;
using System.IO;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Importer.OnlineSongLyrics;
using Serilog;

namespace HandsLiftedApp.Core.Views.Editors.Song
{
    public partial class SongEditorControl : UserControl
    {
        public SongEditorControl()
        {
            InitializeComponent();
            // TODO TabControl.SelectedIndex = 0; if empty, else = 1
        }
        
        BrowserWindow? window;
        public void ImportFromOnline()
        {
            window = new();
            window.TextSelected += (e, text) =>
            {
                if (this.DataContext is SongEditorViewModel songEditorViewModel)
                {
                    songEditorViewModel.FreeTextEntryField = text;
                }
            };
            window.Show();
            
        }
        
        public async void ImportFromFile()
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(this);

            // Start async operation to open the dialog.
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Text File",
                AllowMultiple = false
            });

            if (files.Count >= 1)
            {
                // Open reading stream from the first file.
                await using var stream = await files[0].OpenReadAsync();
                // Open reading stream from the first file.
                if (this.DataContext is SongEditorViewModel songEditorViewModel)
                {
                    SongItem? loaded = null;
                    try
                    {
                        if (files[0].Name.ToLower().EndsWith(".xml"))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(SongItem));
                            loaded = (SongItem)serializer.Deserialize(stream);
                        }
                        else if (files[0].Name.ToLower().EndsWith(".txt"))
                        {
                            StreamReader reader = new StreamReader(stream);
                            string txt = reader.ReadToEnd();
                            loaded = SongImporter.CreateSongItemFromStringData(txt);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Failed to parse file: [{Name}]", files[0].Name);
                    }

                    if (loaded == null)
                    {
                        return;
                    }

                    songEditorViewModel.Song.UUID = loaded.UUID;
                    songEditorViewModel.Song.Title = loaded.Title;
                    songEditorViewModel.Song.Stanzas = loaded.Stanzas;
                    songEditorViewModel.Song.SelectedArrangementId = loaded.SelectedArrangementId;
                    songEditorViewModel.Song.Arrangements = loaded.Arrangements;
                    songEditorViewModel.Song.Arrangement = loaded.Arrangement;
                    songEditorViewModel.Song.Copyright = loaded.Copyright;
                    songEditorViewModel.Song.Design = loaded.Design;
                    songEditorViewModel.Song.StartOnTitleSlide = loaded.StartOnTitleSlide;
                    songEditorViewModel.Song.EndOnBlankSlide = loaded.EndOnBlankSlide;

                    // songEditorViewModel.Song.ResetArrangement();
                    songEditorViewModel.Song.GenerateSlides();
                }
            }
        }

        private void ImportFromOnlineButton_OnClick(object? sender, RoutedEventArgs e)
        {
            ImportFromOnline();
        }

        private void ImportFromFileButton_OnClick(object? sender, RoutedEventArgs e)
        {
            ImportFromFile();
        }
    }
}