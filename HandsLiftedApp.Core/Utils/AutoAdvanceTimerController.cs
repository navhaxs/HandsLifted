using System;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Utils
{
    public class AutoAdvanceTimerController: ReactiveObject, IDisposable
    {
        private CountdownTimer _Timer = new();

        public AutoAdvanceTimerController()
        {
            Timer.OnElapsed += ApplyTimerConfig;
            Timer.WhenAnyValue(t => t.Enabled).Subscribe(_ => this.RaisePropertyChanged(nameof(IsPaused)));
        }

        // True from the moment the countdown elapses until OnSlideNavigation restarts it for the next slide.
        // The timer is stopped in that gap, but it is not "paused" from the user's point of view.
        private bool _isAdvancing;

        /// <summary>What the UI shows as "Paused": the timer is stopped and we are not mid-advance.</summary>
        public bool IsPaused => !Timer.Enabled && !_isAdvancing;

        private void SetAdvancing(bool value)
        {
            if (_isAdvancing == value) return;
            _isAdvancing = value;
            this.RaisePropertyChanged(nameof(IsPaused));
        }

        public CountdownTimer Timer { get => _Timer; set => this.RaiseAndSetIfChanged(ref _Timer, value); }

        private bool _IsTimerConfigured = false;
        public bool IsTimerConfigured
        {
            get => _IsTimerConfigured;
            set => this.RaiseAndSetIfChanged(ref _IsTimerConfigured, value);
        }

        private string _PrettyTimerInterval = "";

        public string PrettyTimerInterval { get => _PrettyTimerInterval; set => this.RaiseAndSetIfChanged(ref _PrettyTimerInterval, value); }

        private IItemInstance? itemInstance;
        private ItemAutoAdvanceTimer? timerConfig;
        private bool userPaused;

        /// <summary>
        /// Autoplay is configured per item (media group), not per slide, so every slide type in the group -
        /// image, custom, video - follows it. Items that aren't media groups (songs, scripture) have none.
        /// </summary>
        private static ItemAutoAdvanceTimer? ResolveTimerConfig(IItemInstance? item) =>
            (item as MediaGroupItem)?.AutoAdvanceTimer;

        public void PauseTimer()
        {
            userPaused = true;
            Timer.Stop();
        }

        public void ResumeTimer()
        {
            userPaused = false;
            Timer.Resume();
        }

        public void OnSlideNavigation(IItemInstance? _itemInstance)
        {
            try
            {
                OnSlideNavigationCore(_itemInstance);
            }
            finally
            {
                SetAdvancing(false);
            }
        }

        private void OnSlideNavigationCore(IItemInstance? _itemInstance)
        {
            var isSameItemGroup = itemInstance != null && ReferenceEquals(itemInstance, _itemInstance);
            var wasPaused = IsTimerConfigured && userPaused && isSameItemGroup;

            Timer.Stop(true);

            itemInstance = _itemInstance;
            timerConfig = ResolveTimerConfig(itemInstance);

            IsTimerConfigured = timerConfig is { IsEnabled: true };

            if (itemInstance == null || timerConfig == null || !IsTimerConfigured)
            {
                userPaused = false;
                return;
            }

            PrettyTimerInterval = FormatTimeSpan(TimeSpan.FromMilliseconds(timerConfig.IntervalMs));

            Timer.Start(timerConfig.IntervalMs);

            if (wasPaused)
            {
                Timer.Stop();
            }

            userPaused = wasPaused;
        }
        
        /// <summary>
        /// Call when items leave the playlist. If the timer was driving a removed item, stop it and hide the
        /// control; otherwise it keeps counting down (and could auto-advance) for an item that no longer exists.
        /// </summary>
        public void OnItemsRemoved(System.Collections.IEnumerable? removed)
        {
            if (removed == null || itemInstance == null) return;
            foreach (var r in removed)
            {
                if (ReferenceEquals(r, itemInstance))
                {
                    OnSlideNavigation(null);
                    return;
                }
            }
        }

        private void ApplyTimerConfig(object? sender, EventArgs e)
        {
            // Flag the advance before stopping, so the UI never sees "stopped and not advancing".
            if (timerConfig?.IsEnabled == true && itemInstance is { Slides.Count: > 0 })
                SetAdvancing(true);

            Timer.Stop();
            // Log.Information($"OnElapsed {parentSlidesGroup.UUID}");
            //
            // if (!parentSlidesGroup.AutoAdvanceTimer.IsEnabled)
            //     return;
            //
            // if (!parentSlidesGroup.State.IsSelected)
            //     return;
            //
            if (timerConfig?.IsEnabled != true)
                return;
                    
            if (itemInstance != null)
            {
                if (itemInstance.Slides.Count > 0)
                {
                    var slideIndex = (itemInstance.SelectedSlideIndex + 1) % itemInstance.Slides.Count;
                    Dispatcher.UIThread.InvokeAsync(() =>
                        MessageBus.Current.SendMessage(new NavigateToSlideReferenceAction()
                            { SlideReference = new SlideReference() { SlideIndex = slideIndex } })
                    );
                }
                return;
            }

            MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.NextSlide });
        }
        
        public string FormatTimeSpan(TimeSpan timeSpan)
        {
            string FormatPart(int quantity, string name) => quantity > 0 ? $"{quantity}{name}" : null;
            string FormatPart2(double quantity, string name) => quantity > 0 ? $"{quantity}{name}" : null;
            return string.Join(", ", new[] { FormatPart(timeSpan.Days, "d"), FormatPart(timeSpan.Hours, "h"), FormatPart(timeSpan.Minutes, "m"), FormatPart2(timeSpan.TotalSeconds, "s") }.Where(x => x != null));
        }

        public void Dispose()
        {
            _Timer.Dispose();
        }
    }
}