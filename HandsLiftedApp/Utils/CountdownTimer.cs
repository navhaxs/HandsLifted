using ReactiveUI;
using System;
using System.Timers;

namespace HandsLiftedApp.Utils
{
    public class CountdownTimer : ReactiveObject
    {
        public event EventHandler? OnElapsed;

        Timer timer;

        private int _TotalTime;

        public int TotalTime { get => _TotalTime; private set => this.RaiseAndSetIfChanged(ref _TotalTime, value); }

        private int _RemainingTime;

        public int RemainingTime { get => _RemainingTime; private set => this.RaiseAndSetIfChanged(ref _RemainingTime, value); }


        private int RESOLUTION = 100;
        public CountdownTimer()
        {
            timer = new Timer() { Interval = RESOLUTION };
            timer.Elapsed += (sender, e) => HandleTimerTick();
        }

        public void Start(int totalTime)
        {
            timer.Stop();
            TotalTime = totalTime;
            RemainingTime = totalTime;
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
        }

        private void HandleTimerTick()
        {
            if (RemainingTime > 0)
                RemainingTime = RemainingTime - RESOLUTION;

            if (RemainingTime == 0)
                HandleTimerElapsed();
        }

        private void HandleTimerElapsed()
        {
            timer.Stop();
            OnElapsed?.Invoke(this, EventArgs.Empty);
        }
    }
}
