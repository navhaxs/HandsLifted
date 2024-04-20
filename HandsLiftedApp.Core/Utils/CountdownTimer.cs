using ReactiveUI;
using System;
using System.Timers;

namespace HandsLiftedApp.Core.Utils
{
    public class CountdownTimer : ReactiveObject, IDisposable
    {
        public event EventHandler? OnElapsed;

        Timer timer;

        private int _TotalTime;

        public int TotalTime { get => _TotalTime; private set => this.RaiseAndSetIfChanged(ref _TotalTime, value); }

        private bool _Enabled;

        public bool Enabled { get => _Enabled; private set => this.RaiseAndSetIfChanged(ref _Enabled, value); }

        private int _RemainingTime;

        public int RemainingTime
        {
            get => _RemainingTime; private set
            {
                this.RaiseAndSetIfChanged(ref _RemainingTime, value);
                this.RaisePropertyChanged(nameof(ElapsedTime));
            }
        }
        public int ElapsedTime { get => _TotalTime - _RemainingTime; }

        private readonly int RESOLUTION = 10;
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
            Resume();
        }  
        
        public void Resume()
        {
            timer.Start();
            Enabled = true;
        }

        public void Stop(bool resetTimer = false)
        {
            timer.Stop();
            Enabled = false;
            if (resetTimer)
            {
                ResetTimer();
            }
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
            ResetTimer();
            OnElapsed?.Invoke(this, EventArgs.Empty);
        }

        private void ResetTimer()
        {
            RemainingTime = TotalTime;
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}