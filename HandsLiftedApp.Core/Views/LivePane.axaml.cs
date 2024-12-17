using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace HandsLiftedApp.Core.Views
{
    public partial class LivePane : UserControl
    {
        public LivePane()
        {
            InitializeComponent();
            SetupDnd(
                "Files",
                async d =>
                {
                    if (Assembly.GetEntryAssembly()?.GetModules().FirstOrDefault()?.FullyQualifiedName is { } name &&
                        TopLevel.GetTopLevel(this) is { } topLevel &&
                        await topLevel.StorageProvider.TryGetFileFromPathAsync(name) is { } storageFile)
                    {
                        d.Set(DataFormats.Files, new[] { storageFile });
                    }
                },
                DragDropEffects.Copy);
        }

        private void SetupDnd(string suffix, Func<DataObject, Task> factory, DragDropEffects effects)
        {
            void DragOver(object? sender, DragEventArgs e)
            {
                if (e.Source is Control c && c.Name == "MoveTarget")
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Move);
                }
                else
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
                }

                // Only allow if the dragged data contains text or filenames.
                if (!e.Data.Contains(DataFormats.Text)
                    && !e.Data.Contains(DataFormats.Files))
                    e.DragEffects = DragDropEffects.None;
            }

            async void Drop(object? sender, DragEventArgs e)
            {
                Globals.Instance.MainViewModel.Playlist.QuickShowItem = null;
                if (e.Source is Control c && c.Name == "MoveTarget")
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Move);
                }
                else
                {
                    e.DragEffects = e.DragEffects & (DragDropEffects.Copy);
                }

                if (e.Data.Contains(DataFormats.Text))
                {
                    _dropState.Text = e.Data.GetText();
                }
                else if (e.Data.Contains(DataFormats.Files))
                {
                    var files = e.Data.GetFiles() ?? Array.Empty<IStorageItem>();
                    var contentStr = "";

                    foreach (var item in files)
                    {
                        if (item is IStorageFile file)
                        {
                            // var content = await DialogsPage.ReadTextFromFile(file, 500);
                            contentStr +=
                                $"File {item.Name}:{Environment.NewLine}{file.Name}{Environment.NewLine}{Environment.NewLine}";

                            var quickSlide = new ImageSlideInstance(file.Path.LocalPath, null);
                            quickSlide.OnPreloadSlide();
                            Globals.Instance.MainViewModel.Playlist.QuickShowItem = quickSlide; // TODO this doesnt let the slides XFADE between consecutive QuickShowItems. implement a slot A and slot B mechanism
                        }
                        else if (item is IStorageFolder folder)
                        {
                            var childrenCount = 0;
                            await foreach (var _ in folder.GetItemsAsync())
                            {
                                childrenCount++;
                            }

                            contentStr +=
                                $"Folder {item.Name}: items {childrenCount}{Environment.NewLine}{Environment.NewLine}";
                        }
                    }

                    _dropState.Text = contentStr;
                }
#pragma warning disable CS0618 // Type or member is obsolete
                else if (e.Data.Contains(DataFormats.FileNames))
                {
                    var files = e.Data.GetFileNames();
                    _dropState.Text = string.Join(Environment.NewLine, files ?? Array.Empty<string>());
                }
#pragma warning restore CS0618 // Type or member is obsolete


                Globals.Instance.MainViewModel.Playlist.PresentationState =
                    PlaylistInstance.PresentationStateEnum.QuickShow;
            }

            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
        }
    }
}