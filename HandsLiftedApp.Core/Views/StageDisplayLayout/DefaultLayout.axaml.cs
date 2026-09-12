using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using ReactiveUI;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Views.StageDisplayLayout
{
    public partial class DefaultLayout : UserControl
    {
        private IDisposable? _slideSubscription;
        private MainViewModel? _vm;
        private int _transitionGeneration;

        public DefaultLayout()
        {
            InitializeComponent();
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
            int myGeneration = Interlocked.Increment(ref _transitionGeneration);

            var logoPath = SlideSpecResolver.NormalizeMediaPath(_vm?.Playlist.LogoGraphicFile);

            SlideRenderSpec? spec = SlideSpecResolver.Resolve(slide, logoPath);

            await Task.Yield();
            if (myGeneration != _transitionGeneration) return;

            if (MotionBackgroundService.IsCurrentlyTransitioning)
            {
                StageSlideCanvas.Transition(null, MotionBackgroundService.CrossFadeOutDuration);

                await MotionBackgroundService.IsTransitioning
                    .Where(v => !v)
                    .FirstAsync()
                    .Timeout(TimeSpan.FromSeconds(10))
                    .Catch(Observable.Return(false));
                if (myGeneration != _transitionGeneration) return;

                StageSlideCanvas.Transition(spec, MotionBackgroundService.CrossFadeInDuration);
            }
            else
            {
                StageSlideCanvas.Transition(spec, TimeSpan.FromMilliseconds(_vm?.Playlist.GetEffectiveTransitionDurationMs(_vm.Playlist.SelectedItem) ?? 120));
            }
        }

    }
}
