using Avalonia.Threading;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.UI;
using ReactiveUI;
using System;
using System.Diagnostics;

namespace HandsLiftedApp.Models.ItemExtensionState
{
    public class ItemAutoAdvanceTimerStateImpl : ReactiveObject, IItemAutoAdvanceTimerState
    {
        SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> parentSlidesGroup;

        DispatcherTimer timer;

        // required for Activator
        public ItemAutoAdvanceTimerStateImpl(ref SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> parent)
        {
            parentSlidesGroup = parent;

            timer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(parent.AutoAdvanceTimer.IntervalMs) };

            parentSlidesGroup.AutoAdvanceTimer.WhenAnyValue(config => config.IsEnabled, config => config.IntervalMs)
                .Subscribe(x =>
                {
                    ApplyTimerConfig(x.Item1, x.Item2);
                });


            // already handled
             //parentSlidesGroup.WhenAnyValue(p => p.State.IsSelected)
             //   .Subscribe(x =>
             //   {
             //       if (x == false) {
             //           timer.Stop();
             //       }
             //   });

            timer.Tick += (sender, e) =>
            {
                if (!parentSlidesGroup.State.IsSelected)
                    return;

                if (parentSlidesGroup.AutoAdvanceTimer.IsLooping)
                {
                    parentSlidesGroup.State.SelectedSlideIndex = (parentSlidesGroup.State.SelectedSlideIndex + 1) % parentSlidesGroup.Slides.Count;
                }
                else
                {
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.NextSlide });
                }
            };

            ApplyTimerConfig(parent.AutoAdvanceTimer.IsEnabled, parent.AutoAdvanceTimer.IntervalMs);

            //parentSlidesGroup.State.PageTransition = new XTransitioningContentControl.XFade(TimeSpan.FromSeconds(2.300));

            MessageBus.Current.Listen<ActiveSlideChangedMessage>()
             .Subscribe(x =>
             {
                 ResetTimer();
             });
        }

        private void ApplyTimerConfig(bool isEnabled, int intervalMs)
        {
            // stop timer
            timer.Stop();

            // update interval
            timer.Interval = TimeSpan.FromMilliseconds(intervalMs);

            // restart timer if enabled
            if (isEnabled)
            {
                timer.Start();
            }
        }    
        private void ResetTimer()
        {
            // stop timer
            timer.Stop();

            // restart timer if enabled and item is active
            if (parentSlidesGroup.State.IsSelected == true && parentSlidesGroup.AutoAdvanceTimer.IsEnabled)
            {
                timer.Start();
            }
        }
    }
}
