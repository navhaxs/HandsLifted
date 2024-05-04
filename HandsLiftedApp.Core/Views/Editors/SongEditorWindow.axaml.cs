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
            DataContextChanged += (sender, args) =>
            {
                if (DataContext is SongEditorViewModel songEditorViewModel)
                {
                    EditorTab.WhenAnyValue(et => et.SelectedIndex).Subscribe(index =>
                    {
                        switch (index)
                        {
                            case 0:
                                songEditorViewModel.LyricEntryMode = SongEditorViewModel.LyricEntryModeType.Stanza;
                                break;
                            case 1:
                                songEditorViewModel.LyricEntryMode = SongEditorViewModel.LyricEntryModeType.FreeText;
                                break;
                        }
                    });

                    songEditorViewModel.FreeTextEntryField = SongImporter.songItemToFreeText(songEditorViewModel.Song);

                    songEditorViewModel.WhenAnyValue(x => x.Song.Slides).Subscribe((slides) =>
                    {
                        songEditorViewModel.FreeTextEntryField =
                            SongImporter.songItemToFreeText(songEditorViewModel.Song);
                    });
                }

            };
        }

        public void OnAddPartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (SongStanza)((Control)sender).DataContext;
            ((SongEditorViewModel)this.DataContext).Song.Arrangement.Add(clickedStanza.Id);
        }

        public void OnRepeatPartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (ArrangementRef)((Control)sender).DataContext;
            var lastIndex =
                ((SongEditorViewModel)this.DataContext).Song.Arrangement.IndexOf(clickedStanza.SongStanza.Id);
            ((SongEditorViewModel)this.DataContext).Song.Arrangement.Insert(lastIndex + 1, clickedStanza.SongStanza.Id);
        }

        public void OnRemovePartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (ArrangementRef)((Control)sender).DataContext;
            var lastIndex =
                ((SongEditorViewModel)this.DataContext).Song.Arrangement.IndexOf(clickedStanza.SongStanza.Id);
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

        private void New_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongEditorViewModel songEditorViewModel)
            {
                // songEditorViewModel.Song = new SongItemInstance(songEditorViewModel.Playlist);
                songEditorViewModel.Song.Stanzas.Clear();
                songEditorViewModel.Song.ResetArrangement();
            }
        }

        private void LoadFromXml_OnClick(object? sender, RoutedEventArgs e)
        {
            LoadXml();
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
                    XmlSerializer serializer = new XmlSerializer(typeof(SongItem));
                    var loaded = (SongItem)serializer.Deserialize(stream);
                    // TODO: update source Item in Playlist
                    // var loadedSong = new SongItemInstance(Globals.MainViewModel.Playlist)
                    // {
                    //     UUID = loaded.UUID,
                    //     Title = loaded.Title,
                    //     Arrangement = loaded.Arrangement,
                    //     Arrangements = loaded.Arrangements,
                    //     SelectedArrangementId = loaded.SelectedArrangementId,
                    //     Stanzas = loaded.Stanzas,
                    //     Copyright = loaded.Copyright,
                    //     Design = loaded.Design,
                    //     StartOnTitleSlide = loaded.StartOnTitleSlide,
                    //     EndOnBlankSlide = loaded.EndOnBlankSlide
                    // };

                    songEditorViewModel.Song.UUID = loaded.UUID;
                    songEditorViewModel.Song.Title = loaded.Title;
                    songEditorViewModel.Song.Stanzas = loaded.Stanzas;
                    songEditorViewModel.Song.Arrangement = loaded.Arrangement;
                    songEditorViewModel.Song.Arrangements = loaded.Arrangements;
                    songEditorViewModel.Song.SelectedArrangementId = loaded.SelectedArrangementId;
                    songEditorViewModel.Song.Copyright = loaded.Copyright;
                    songEditorViewModel.Song.Design = loaded.Design;
                    songEditorViewModel.Song.StartOnTitleSlide = loaded.StartOnTitleSlide;
                    songEditorViewModel.Song.EndOnBlankSlide = loaded.EndOnBlankSlide;
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
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Text File",
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


        private void MoveUp_OnClick(object? sender, RoutedEventArgs e)
        {
            // 6 [0, 1, 2, 3, 4, 5]
            if (StanzaArrangementListBox.SelectedIndex > -1 && StanzaArrangementListBox.SelectedIndex > 0)
            {
                if (this.DataContext is SongEditorViewModel viewModel)
                {
                    viewModel.Song.Stanzas.Move(StanzaArrangementListBox.SelectedIndex,
                        StanzaArrangementListBox.SelectedIndex - 1);
                }
            }
        }

        private void MoveDown_OnClick(object? sender, RoutedEventArgs e)
        {
            // 6 [0, 1, 2, 3, 4, 5]
            if (StanzaArrangementListBox.SelectedIndex > -1 &&
                StanzaArrangementListBox.SelectedIndex < StanzaArrangementListBox.Items.Count - 1)
            {
                if (this.DataContext is SongEditorViewModel viewModel)
                {
                    viewModel.Song.Stanzas.Move(StanzaArrangementListBox.SelectedIndex,
                        StanzaArrangementListBox.SelectedIndex + 1);
                }
            }
        }

        private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
        }
    }
}