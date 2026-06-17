using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using Serilog;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace HandsLiftedApp.Core.Views
{
    public partial class LivePane : UserControl
    {
        private IDisposable? _slideSubscription;
        private MainViewModel? _vm;
        private int _transitionGeneration;

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
                .WhenAnyValue(p => p.ActiveSlide, p => p.LogoGraphicFile, (slide, _) => slide)
                .Subscribe(OnActiveSlideChanged);
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _slideSubscription?.Dispose();
        }

        private async void OnActiveSlideChanged(Slide? slide)
        {
            // Generation token: if another slide change fires while we are preloading,
            // our token will be stale and we must not call Transition() — doing so would
            // override the newer slide with an out-of-order result.
            int myGeneration = System.Threading.Interlocked.Increment(ref _transitionGeneration);

            var logoPath = NormalizeMediaPath(_vm?.Playlist.LogoGraphicFile);
            Log.Debug("[LivePane] OnActiveSlideChanged: {SlideType}, ImagePath={Path}, LogoPath={Logo}",
                slide?.GetType().Name ?? "null",
                (slide as ImageSlideInstance)?.SourceMediaFilePath ?? "-",
                logoPath ?? "-");

            SlideRenderSpec? spec = slide switch
            {
                SongSlideInstance s      => SongSlideSpecBuilder.Build(s),
                SongTitleSlideInstance t => SongTitleSlideSpecBuilder.Build(t),
                ImageSlideInstance img   => IsValidMediaPath(img.SourceMediaFilePath)
                    ? new SlideRenderSpec(new ImageBackground(img.SourceMediaFilePath), Array.Empty<RenderElement>())
                    : null,
                LogoSlide                => IsValidMediaPath(logoPath)
                    ? new SlideRenderSpec(new ImageBackground(logoPath), Array.Empty<RenderElement>())
                    : null,
                HandsLiftedApp.Data.Data.Models.Slides.CustomSlide cs => CustomSlideSpecBuilder.Build(cs),
                _                        => null,
            };

            // Pre-warm bitmap cache on a background thread so the render thread never
            // has to decode a large image during the first transition frame.
            if (spec?.Background is ImageBackground)
                await Task.Run(() => SlideRenderer.Preload(spec));

            // Bail out if a newer slide change arrived while we were preloading.
            if (myGeneration != _transitionGeneration) return;

            // Yield so that ActiveItem bindings (MotionBackgroundLayer) have a chance to
            // fire and arm the cross-fade gate before we check it.
            await Task.Yield();
            if (myGeneration != _transitionGeneration) return;

            if (MotionBackgroundService.IsCurrentlyTransitioning)
            {
                // Phase 1: fade out old text in sync with the outgoing video.
                LivePreviewCanvas.Transition(null, MotionBackgroundService.CrossFadeOutDuration);

                // Wait until the new video begins its fade-in.
                await MotionBackgroundService.IsTransitioning
                    .Where(v => !v)
                    .FirstAsync()
                    .Timeout(TimeSpan.FromSeconds(10))
                    .Catch(Observable.Return(false));
                if (myGeneration != _transitionGeneration) return;

                // Phase 2: fade in new text in sync with the incoming video.
                LivePreviewCanvas.Transition(spec, MotionBackgroundService.CrossFadeInDuration);
            }
            else
            {
                // No video cross-fade — use the user's slide transition duration.
                LivePreviewCanvas.Transition(spec, TimeSpan.FromMilliseconds(_vm?.Playlist.SlideTransitionDurationMs ?? 120));
            }
        }

        /// <summary>
        /// Repairs paths where the serializer has mangled an avares:// URI into a Windows-style
        /// absolute path (e.g. "C:\...\avares:\Assembly\Assets\...").
        /// Returns the corrected avares:// URI, or the original path unchanged for normal files.
        /// </summary>
        private static string? NormalizeMediaPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            var idx = path.IndexOf("avares:", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                // Repair paths mangled by the serializer, e.g.:
                //   "C:\VisionScreens Data\avares:\Assembly\Assets\logo.png"
                // → "avares://Assembly/Assets/logo.png"
                var rest = path.Substring(idx + "avares:".Length)
                               .Replace('\\', '/')
                               .TrimStart('/');
                if (rest.Length == 0) return path; // malformed — return original unchanged
                return "avares://" + rest;
            }

            return path;
        }

        // Returns true when a media path is expected to resolve to real content.
        // avares:// URIs are always treated as valid (checked at render time).
        // File system paths are only valid when the file actually exists, so that
        // a missing file produces a null spec (smooth fade to black) rather than
        // an ImageBackground spec whose bitmap silently fails to load mid-transition.
        private static bool IsValidMediaPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase)) return true;
            return System.IO.File.Exists(path);
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