using System;
using System.Threading;
using ConcurrentPriorityQueue.Core;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
    public class ImportWorkerThread : ReactiveObject, IDisposable
    {
        private volatile bool _isRunning = true;
        private readonly Thread _workerThread;

        public ImportWorkerThread()
        {
            _workerThread = new Thread(RunWorkerLoop) { IsBackground = true };
            _workerThread.Start();
        }

        public void Dispose()
        {
            _isRunning = false;
            // Add a dummy request to wake up the blocking collection
            priorityQueue.Add(new BackgroundWorkRequest { Callback = () => { } });
            _workerThread.Join(500);
        }

        private bool _IsBusy = false;
        public bool IsBusy { get => _IsBusy; set => this.RaiseAndSetIfChanged(ref _IsBusy, value); }

        public static System.Collections.Concurrent.BlockingCollection<BackgroundWorkRequest> priorityQueue = new ConcurrentPriorityQueue<BackgroundWorkRequest, int>().ToBlockingCollection();

        public class BackgroundWorkRequest : IHavePriority<int>
        {
            public int Priority { get; set; }
            public Action Callback { get; set; }
        }

        void RunWorkerLoop()
        {
            Log.Verbose("Initializing background worker thread");

            // this foreach will block until ready -
            foreach (var item in priorityQueue.GetConsumingEnumerable())
            {
                if (!_isRunning) break;

                Log.Verbose("Picking up background work job");
                IsBusy = true;

                // grab the next item
                BackgroundWorkRequest request = item;
                
                // execute
                try
                {
                    request.Callback?.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in background work callback");
                }

                IsBusy = false;
            }

            Log.Verbose("Background worker thread destroyed");
        } 
    }
}