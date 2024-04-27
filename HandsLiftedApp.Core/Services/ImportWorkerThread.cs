using System;
using System.Threading;
using ConcurrentPriorityQueue.Core;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
    public class ImportWorkerThread : ReactiveObject
    {
        public ImportWorkerThread()
        {
            new Thread(RunWorkerLoop) { IsBackground = true }.Start();
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
                Log.Verbose("Picking up background work job");
                IsBusy = true;

                // grab the next item
                BackgroundWorkRequest request = item;
                
                // execute
                request.Callback();

                IsBusy = false;
            }

            Log.Verbose("Background worker thread destroyed");
        } 
    }
}