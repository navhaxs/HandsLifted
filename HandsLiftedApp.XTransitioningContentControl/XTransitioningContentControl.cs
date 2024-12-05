using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;

namespace HandsLiftedApp.XTransitioningContentControl
{
    /// <summary>
    /// Displays <see cref="ContentControl.Content"/> according to a <see cref="FuncDataTemplate"/>.
    /// Uses <see cref="PageTransition"/> to move between the old and new content values. 
    /// </summary>
    public class XTransitioningContentControl : ContentControl
    {
        private CancellationTokenSource? _currentTransition;
        private ContentPresenter? _presenter2;
        private bool _isFirstFull;
        private bool _shouldAnimate;

        /// <summary>
        /// Defines the <see cref="PageTransition"/> property.
        /// </summary>
        public static readonly StyledProperty<IPageTransition?> PageTransitionProperty =
            AvaloniaProperty.Register<TransitioningContentControl, IPageTransition?>(
                nameof(PageTransition),
                defaultValue: new ImmutableCrossFade(TimeSpan.FromMilliseconds(125)));

        /// <summary>
        /// Gets or sets the animation played when content appears and disappears.
        /// </summary>
        public IPageTransition? PageTransition
        {
            get => GetValue(PageTransitionProperty);
            set => SetValue(PageTransitionProperty, value);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);

            if (_shouldAnimate)
            {
                _currentTransition?.Cancel();

                if (_presenter2 is not null &&
                    Presenter is Visual presenter &&
                    PageTransition is { } transition)
                {
                    _shouldAnimate = false;

                    var cancel = new CancellationTokenSource();
                    _currentTransition = cancel;

                    var from = _isFirstFull ? _presenter2 : presenter;
                    var to = _isFirstFull ? presenter : _presenter2;

                    from.ZIndex = 0;
                    to.ZIndex = 1;

                    transition.Start(from, to, true, cancel.Token).ContinueWith(x =>
                    {
                        if (!cancel.IsCancellationRequested)
                        {
                            HideOldPresenter();
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }

                _shouldAnimate = false;
            }

            return result;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateContent(false);
        }

        protected override bool RegisterContentPresenter(ContentPresenter presenter)
        {
            if (base.RegisterContentPresenter(presenter))
            {
                return true;
            }

            if (presenter is ContentPresenter p &&
                p.Name == "PART_ContentPresenter2")
            {
                _presenter2 = p;
                _presenter2.IsVisible = false;
                UpdateContent(false);
                return true;
            }

            return false;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == ContentProperty)
            {
                UpdateContent(true);
                return;
            }

            base.OnPropertyChanged(change);
        }

        private void UpdateContent(bool withTransition)
        {
            if (VisualRoot is null || _presenter2 is null || Presenter is null)
            {
                return;
            }

            if (_presenter2?.Content is ISlideRender isr)
            {
                isr.OnLeaveSlide();
            }

            if (Presenter?.Content is ISlideRender isr2)
            {
                isr2.OnLeaveSlide();
            }

            var currentPresenter = _isFirstFull ? _presenter2 : Presenter;
            currentPresenter.Content = Content;
            currentPresenter.IsVisible = true;

            _isFirstFull = !_isFirstFull;

            if (PageTransition is not null && withTransition)
            {
                _shouldAnimate = true;
                InvalidateArrange();
            }
            else
            {
                HideOldPresenter();
            }
        }

        private void HideOldPresenter()
        {
            var oldPresenter = _isFirstFull ? _presenter2 : Presenter;
            if (oldPresenter is not null)
            {
                oldPresenter.Content = null;
                oldPresenter.IsVisible = false;

                oldPresenter.ZIndex = 1;
            }

            var newPresenter = _isFirstFull ? Presenter : _presenter2;
            if (newPresenter is not null)
            {
                newPresenter.ZIndex = 0;
            }
        }

        private class ImmutableCrossFade : IPageTransition
        {
            private readonly CrossFade _inner;

            public ImmutableCrossFade(TimeSpan duration) => _inner = new CrossFade(duration);

            public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
            {
                return _inner.Start(from, to, cancellationToken);
            }
        }
    }
}