using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Utils;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Models.ItemExtensionState
{
    public class ItemAutoAdvanceTimerStateImpl : ReactiveObject, IItemAutoAdvanceTimerState
    {
        SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> parentSlidesGroup;

        public CountdownTimer Timer { get; set; }

        // required for Activator
        public ItemAutoAdvanceTimerStateImpl(ref SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> parent)
        {
            parentSlidesGroup = parent;

            Timer = new CountdownTimer();

            parentSlidesGroup.AutoAdvanceTimer.WhenAnyValue(config => config.IsEnabled, config => config.IntervalMs)
                .Subscribe(x =>
                {
                    ApplyTimerConfig(x.Item1, x.Item2);

                    // TODO if timer is NOT already running, start it now...
                    //if (parentSlidesGroup.State.IsSelected == true && parentSlidesGroup.AutoAdvanceTimer.IsEnabled)
                    //    Timer.Start(x.Item2);
                });

            // already handled
            //parentSlidesGroup.WhenAnyValue(p => p.State.IsSelected)
            //   .Subscribe(x =>
            //   {
            //       if (x == false) {
            //           timer.Stop();
            //       }
            //   });

            Timer.OnElapsed += (sender, e) =>
            {
                if (!parentSlidesGroup.AutoAdvanceTimer.IsEnabled)
                    return;

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
            Timer.Stop();

            // restart timer if enabled
            if (isEnabled)
            {
                Timer.Start(intervalMs);
            }
        }
        private void ResetTimer()
        {
            // stop timer
            Timer.Stop();

            // restart timer if enabled and item is active
            if (parentSlidesGroup.State.IsSelected == true && parentSlidesGroup.AutoAdvanceTimer.IsEnabled)
            {
                Timer.Start(parentSlidesGroup.AutoAdvanceTimer.IntervalMs);
            }
        }
    }
}
