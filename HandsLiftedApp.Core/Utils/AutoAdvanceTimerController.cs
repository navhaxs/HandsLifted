using System;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.RuntimeData;
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
        private Slide? itemInstanceSlide;
        private ISlideInstance? slideInstance;
        public void OnSlideNavigation(IItemInstance? _itemInstance)
        {
            Timer.Stop(true);
            
            itemInstance = _itemInstance;
            itemInstanceSlide = itemInstance?.Slides[itemInstance.SelectedSlideIndex];
            slideInstance = itemInstanceSlide?.GetAsISlideInstance();

            IsTimerConfigured = slideInstance?.SlideTimerConfig != null;
            
            if (slideInstance == null || !IsTimerConfigured)
            {
                return;
            }
            
            PrettyTimerInterval = FormatTimeSpan(TimeSpan.FromMilliseconds(slideInstance.SlideTimerConfig.IntervalMs));

            if (slideInstance.SlideTimerConfig.IsEnabled)
            {
                Timer.Start(slideInstance.SlideTimerConfig.IntervalMs);
            }
        }
        
        private void ApplyTimerConfig(object? sender, EventArgs e)
        {
            Timer.Stop();
            // Log.Information($"OnElapsed {parentSlidesGroup.UUID}");
            //
            // if (!parentSlidesGroup.AutoAdvanceTimer.IsEnabled)
            //     return;
            //
            // if (!parentSlidesGroup.State.IsSelected)
            //     return;
            //
            if (slideInstance?.SlideTimerConfig?.IsEnabled != true)
                return;
                    
            if (itemInstance != null)
            {
                var slideIndex = (itemInstance.SelectedSlideIndex + 1) % itemInstance.Slides.Count;
                Dispatcher.UIThread.InvokeAsync(() =>
                    MessageBus.Current.SendMessage(new NavigateToSlideReferenceAction()
                        { SlideReference = new SlideReference() { SlideIndex = slideIndex } })
                );
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