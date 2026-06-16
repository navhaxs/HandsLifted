using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using HandsLiftedApp.Core.Views.Confirmation;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Importer.OnlineSongLyrics;
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
            SongEditorControl.ImportFromFile();
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
                Log.Error(e, "Error showing open file dialog");
                interaction.SetOutput(null);
            }
        }

        private void ImportFromOnline_OnClick(object? sender, RoutedEventArgs e)
        {
            SongEditorControl.ImportFromOnline();
        }

        private bool _closeConfirmed = false;

        private void Discard_OnClick(object? sender, RoutedEventArgs e)
        {
            _closeConfirmed = true;
            Close();
        }

        private void Save_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SongEditorViewModel vm)
                DoSaveToLibrary(vm);
        }

        private void DoSaveToLibrary(SongEditorViewModel vm)
        {
            var dir = vm.SongLibrary?.Config.Directory;
            if (dir == null) return;

            var title = vm.Song.Title;
            if (string.IsNullOrWhiteSpace(title)) title = "Untitled";

            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = string.Concat(title.Select(c => invalidChars.Contains(c) ? '_' : c));
            var path = Path.Combine(dir, safeName + ".xml");

            using var memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            var settings = new XmlWriterSettings
            {
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                Indent = true,
            };
            using (var xmlWriter = XmlWriter.Create(writer, settings))
            {
                var serializer = new XmlSerializer(typeof(SongItem));
                SongItemInstance existing = vm.Song;
                var x = new SongItem
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

            File.WriteAllBytes(path, memoryStream.ToArray());
            vm.SongLibrary!.TriggerRefresh();

            _closeConfirmed = true;
            Close();
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (_closeConfirmed) return;

            if (DataContext is SongEditorViewModel { IsNewSongMode: true } vm)
            {
                bool hasEdits = !string.IsNullOrEmpty(vm.Song.Title) || vm.Song.Stanzas.Count > 0;
                if (!hasEdits) return;

                e.Cancel = true;

                var dialog = new NewSongUnsavedConfirmationWindow();
                await dialog.ShowDialog(this);

                switch (dialog.Result)
                {
                    case NewSongUnsavedConfirmationWindow.DialogResult.Save:
                        DoSaveToLibrary(vm);
                        break;
                    case NewSongUnsavedConfirmationWindow.DialogResult.Discard:
                        _closeConfirmed = true;
                        Close();
                        break;
                    // Cancel → window stays open
                }
            }
        }
    }
}