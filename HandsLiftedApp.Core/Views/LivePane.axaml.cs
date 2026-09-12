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
                        d.Add(DataTransferItem.Create(DataFormat.File, storageFile));
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

        private SlideRenderSpec? BuildSlideSpec(Slide? slide)
        {
            var logoPath = SlideSpecResolver.NormalizeMediaPath(_vm?.Playlist.LogoGraphicFile);
            return SlideSpecResolver.Resolve(slide, logoPath);
        }

        private async void OnActiveSlideChanged(Slide? slide)
        {
            // Generation token: if another slide change fires while we are preloading,
            // our token will be stale and we must not call Transition() — doing so would
            // override the newer slide with an out-of-order result.
            int myGeneration = System.Threading.Interlocked.Increment(ref _transitionGeneration);

            var logoPath = SlideSpecResolver.NormalizeMediaPath(_vm?.Playlist.LogoGraphicFile);
            Log.Debug("[LivePane] OnActiveSlideChanged: {SlideType}, ImagePath={Path}, LogoPath={Logo}",
                slide?.GetType().Name ?? "null",
                (slide as ImageSlideInstance)?.SourceMediaFilePath ?? "-",
                logoPath ?? "-");

            SlideRenderSpec? spec = BuildSlideSpec(slide);

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
                LivePreviewCanvas.Transition(spec, TimeSpan.FromMilliseconds(_vm?.Playlist.GetEffectiveTransitionDurationMs(_vm.Playlist.SelectedItem) ?? 120));
            }
        }

        private void SetupDnd(string suffix, Func<DataTransfer, Task> factory, DragDropEffects effects)
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
                if (!e.DataTransfer.Contains(DataFormat.Text)
                    && !e.DataTransfer.Contains(DataFormat.File))
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

                if (e.DataTransfer.Contains(DataFormat.Text))
                {
                    _dropState.Text = e.DataTransfer.TryGetText();
                }
                else if (e.DataTransfer.Contains(DataFormat.File))
                {
                    var files = e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>();
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


                Globals.Instance.MainViewModel.Playlist.PresentationState =
                    PlaylistInstance.PresentationStateEnum.QuickShow;
            }

            AddHandler(DragDrop.DropEvent, Drop);
            AddHandler(DragDrop.DragOverEvent, DragOver);
        }
    }
}