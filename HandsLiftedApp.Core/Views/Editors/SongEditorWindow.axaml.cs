using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Gdk;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using Serilog;
using Window = Avalonia.Controls.Window;

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
            DataContextChanged += (sender, args) =>
            {
                if (DataContext is SongEditorViewModel songEditorViewModel)
                {
                    songEditorViewModel.FreeTextEntryField = SongImporter.songItemToFreeText(songEditorViewModel.Song);

                    songEditorViewModel.WhenAnyValue(x => x.Song.Slides).Subscribe((slides) =>
                    {
                        songEditorViewModel.FreeTextEntryField =
                            SongImporter.songItemToFreeText(songEditorViewModel.Song);
                    });
                }
            };
        }

        public void AddToPlaylist_OnClick(object? sender, RoutedEventArgs args)
        {
            if (DataContext is SongEditorViewModel { ItemInsertIndex: not null, ItemInserted: false } songEditorViewModel)
            {
                Globals.Instance.MainViewModel.Playlist.Items.Insert(songEditorViewModel.ItemInsertIndex.Value, songEditorViewModel.Song);
                songEditorViewModel.ItemInserted = true;
                MessageBus.Current.SendMessage(new NavigateToItemMessage() { Index = songEditorViewModel.ItemInsertIndex.Value });
                Close();
            }
        }

        private void ResetArrangement_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                songEditorViewModel.Song.ResetArrangement();
            }
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
        
        private void New_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                // songEditorViewModel.Song = new SongItemInstance(songEditorViewModel.Playlist);
                songEditorViewModel.Song.Stanzas.Clear();
                songEditorViewModel.Song.Title = "";
                songEditorViewModel.Song.Copyright = "";
                songEditorViewModel.Song.ResetArrangement();
            }
        }

        private void LoadFromText_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                LoadXml();
                // songEditorViewModel.LyricEntryMode = true;
            }
        }
        
          private async void LoadXml()
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

        private void SaveAsXml_OnClick(object? sender, RoutedEventArgs e)
        {
            SaveXml();
        }

        private async void SaveXml()
        {
            // Get top level from the current control. Alternatively, you can use Window reference instead.
            var topLevel = TopLevel.GetTopLevel(this);

            // Start async operation to open the dialog.
            var xmlFileType = new FilePickerFileType("XML Document")
            {
                Patterns = new[] { "*.xml" },
                MimeTypes = new[] { "text/xml" }
            };
            
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Song XML File",
                FileTypeChoices = new[] { xmlFileType }

            });

            if (file != null)
            {
                if (this.DataContext is SongEditorViewModel songEditorViewModel)
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        TextWriter writer = new StreamWriter(memoryStream);

                        var settings = new XmlWriterSettings
                        {
                            NewLineChars = "\n",
                            NewLineHandling = NewLineHandling.Replace,
                            Indent = true,
                        };
                        using (XmlWriter xmlWriter = XmlWriter.Create(writer, settings))
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(SongItem));
                            SongItemInstance existing = (SongItemInstance)songEditorViewModel.Song;
                            // TODO: for *Instance classes implement an interface to convert to base class
                            SongItem x = new SongItem()
                            {
                                UUID = existing.UUID,
                                Title = existing.Title,
                                Arrangement = existing.Arrangement,
                                Arrangements = existing.Arrangements,
                                SelectedArrangementId = existing.SelectedArrangementId,
                                Stanzas = existing.Stanzas,
                                Copyright = existing.Copyright,
                                Design = existing.Design,
                                StartOnTitleSlide = existing.StartOnTitleSlide,
                                EndOnBlankSlide = existing.EndOnBlankSlide
                            };
                            serializer.Serialize(xmlWriter, x);
                        }

                        // serialization was successful - only now do we write to disk
                        await using var stream = await file.OpenWriteAsync();

                        memoryStream.WriteTo(stream);
                    }
                }
            }
        }

        private async Task ShowOpenFileDialog(InteractionContext<Unit, string[]?> interaction)
        {
            try
            {
                var dialog = new OpenFileDialog() { AllowMultiple = true };
                var fileNames = await dialog.ShowAsync(this);
                interaction.SetOutput(fileNames);
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                interaction.SetOutput(null);
            }
        }
    }
}