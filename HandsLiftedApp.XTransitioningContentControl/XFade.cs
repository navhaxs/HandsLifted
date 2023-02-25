﻿using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System.Reactive.Disposables;


namespace HandsLiftedApp.XTransitioningContentControl
{

    /// <summary>
    /// Defines a cross-fade animation between two <see cref="IVisual"/>s.
    /// </summary>
    public class XFade : IPageTransition
    {
        private readonly Animation _fadeOutAnimation;
        private readonly Animation _fadeInAnimation;

        /// <summary>
        /// Initializes a new instance of the <see cref="XFade"/> class.
        /// </summary>
        public XFade()
            : this(TimeSpan.Zero)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XFade"/> class.
        /// </summary>
        /// <param name="duration">The duration of the animation.</param>
        public XFade(TimeSpan duration)
        {
            _fadeOutAnimation = new Animation
            {
                Children =
                {
                    new KeyFrame()
                    {
                        Setters =
                        {
                            new Setter
                            {
                                Property = Visual.OpacityProperty,
                                Value = 1d
                            }
                        },
                        Cue = new Cue(0d)
                    },
                    new KeyFrame()
                    {
                        Setters =
                        {
                            new Setter
                            {
                                Property = Visual.OpacityProperty,
                                Value = 1d
                            }
                        },
                        Cue = new Cue(0.9d)
                    },
                    new KeyFrame()
                    {
                        Setters =
                        {
                            new Setter
                            {
                                Property = Visual.OpacityProperty,
                                Value = 0d
                            }
                        },
                        Cue = new Cue(1d)
                    },
                },
                Easing = new XEaseInOutExpo()
                //, Delay = new TimeSpan(0,0,0,0, 500)
                //Easing = new CubicEaseIn()
            };
            _fadeInAnimation = new Animation
            {
                Children =
                {
                    new KeyFrame()
                    {
                        Setters =
                        {
                            new Setter
                            {
                                Property = Visual.OpacityProperty,
                                Value = 1d
                            }
                        },
                        Cue = new Cue(1d)
                    }

                },
                //Easing = new CubicEaseOut()
                Easing = new XEaseInOutExpo()
            };
            _fadeOutAnimation.Duration = _fadeInAnimation.Duration = duration;
        }

        /// <summary>
        /// Gets the duration of the animation.
        /// </summary>
        public TimeSpan Duration
        {
            get => _fadeOutAnimation.Duration;
            set => _fadeOutAnimation.Duration = _fadeInAnimation.Duration = value;
        }

        /// <summary>
        /// Gets or sets element entrance easing.
        /// </summary>
        public Easing FadeInEasing
        {
            get => _fadeInAnimation.Easing;
            set => _fadeInAnimation.Easing = value;
        }

        /// <summary>
        /// Gets or sets element exit easing.
        /// </summary>
        public Easing FadeOutEasing
        {
            get => _fadeOutAnimation.Easing;
            set => _fadeOutAnimation.Easing = value;
        }

        /// <inheritdoc cref="Start(Visual, Visual, CancellationToken)" />
        public async Task Start(Visual from, Visual to, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var tasks = new List<Task>();
            using (var disposables = new CompositeDisposable())
            {
                if (to != null)
                {
                    disposables.Add(to.SetValue(Visual.OpacityProperty, 0, Avalonia.Data.BindingPriority.Animation));
                }

                // TODO: take a screenshot of the NEXT SLIDE
                // TODO: take a screenshot of the NEXT SLIDE
                // TODO: take a screenshot of the NEXT SLIDE
                // TODO: take a screenshot of the NEXT SLIDE
                // TODO: take a screenshot of the NEXT SLIDE
                // then fade between two NON-Alpha images :)

                //if (from != null)
                //{
                //    from.PageTransitionIsVisible = true;
                //    from.Opacity = 1;
                //    Dispatcher.UIThread.RunJobs(DispatcherPriority.Render); // required to wait for images to load

                //    tasks.Add(_fadeOutAnimation.RunAsync(from, null, cancellationToken));
                //}

                if (to != null)
                {
                    tasks.Add(_fadeInAnimation.RunAsync(to, null, cancellationToken));
                    to.IsVisible = true;
                }

                await Task.WhenAll(tasks);

                if (from != null && !cancellationToken.IsCancellationRequested)
                {
                    from.Opacity = 0;
                }
            }
        }

        /// <summary>
        /// Starts the animation.
        /// </summary>
        /// <param name="from">
        /// The control that is being transitioned away from. May be null.
        /// </param>
        /// <param name="to">
        /// The control that is being transitioned to. May be null.
        /// </param>
        /// <param name="forward">
        /// Unused for cross-fades.
        /// </param>
        /// <param name="cancellationToken">allowed cancel transition</param>
        /// <returns>
        /// A <see cref="Task"/> that tracks the progress of the animation.
        /// </returns>
        Task IPageTransition.Start(Visual from, Visual to, bool forward, CancellationToken cancellationToken)
        {
            return Start(from, to, cancellationToken);
        }
    }

}