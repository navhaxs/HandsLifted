using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace HandsLiftedApp.Core.Views
{
    public partial class LivePane : UserControl
    {
        private IDisposable? _slideSubscription;
        private MainViewModel? _vm;

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

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _slideSubscription?.Dispose();

            if (DataContext is not MainViewModel vm) return;
            _vm = vm;

            _slideSubscription = vm.Playlist
                .WhenAnyValue(p => p.ActiveSlide)
                .Subscribe(OnActiveSlideChanged);
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _slideSubscription?.Dispose();
        }

        private void OnActiveSlideChanged(Slide? slide)
        {
            var logoPath = _vm?.Playlist.LogoGraphicFile;
            Log.Debug("[LivePane] OnActiveSlideChanged: {SlideType}, ImagePath={Path}, LogoPath={Logo}",
                slide?.GetType().Name ?? "null",
                (slide as ImageSlideInstance)?.SourceMediaFilePath ?? "-",
                logoPath ?? "-");

            SlideRenderSpec? spec = slide switch
            {
                SongSlideInstance s      => SongSlideSpecBuilder.Build(s),
                SongTitleSlideInstance t => SongTitleSlideSpecBuilder.Build(t),
                ImageSlideInstance img   => string.IsNullOrWhiteSpace(img.SourceMediaFilePath)
                    ? null
                    : new SlideRenderSpec(new ImageBackground(img.SourceMediaFilePath), Array.Empty<RenderElement>()),
                LogoSlide                => string.IsNullOrWhiteSpace(logoPath)
                    ? null
                    : new SlideRenderSpec(new ImageBackground(logoPath), Array.Empty<RenderElement>()),
                // Other slide types (custom AXAML, blank) — canvas shows blank.
                _                        => null,
            };
            LivePreviewCanvas.Transition(spec, TimeSpan.FromMilliseconds(120));
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