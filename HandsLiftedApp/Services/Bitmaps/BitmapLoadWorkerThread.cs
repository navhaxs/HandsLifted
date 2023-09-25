using Avalonia.Media.Imaging;
using ConcurrentPriorityQueue.Core;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;
using System;
using System.Threading;

namespace HandsLiftedApp.Services.Bitmaps
{
    public class BitmapLoadWorkerThread : ReactiveObject
    {
        public BitmapLoadWorkerThread()
        {
            new Thread(RunWorkerLoop) { IsBackground = true }.Start();
        }

        private bool _IsBusy = false;
        public bool IsBusy { get => _IsBusy; set => this.RaiseAndSetIfChanged(ref _IsBusy, value); }

        public static System.Collections.Concurrent.BlockingCollection<BitmapLoadRequest> priorityQueue = new ConcurrentPriorityQueue<BitmapLoadRequest, int>().ToBlockingCollection();

        public class BitmapLoadRequest : IHavePriority<int>
        {
            public int Priority { get; set; }
            public string BitmapFilePath { get; set; }
            public Action<Bitmap> Callback { get; set; }
        }

        void RunWorkerLoop()
        {
            Log.Verbose("Initializing bitmap load thread");

            // this foreach will block until ready -
            foreach (var item in priorityQueue.GetConsumingEnumerable())
            {
                IsBusy = true;

                // grab the next item
                BitmapLoadRequest request = item;
                //Log.Verbose($"Bitmap load thread got new item BitmapFilePath={request.BitmapFilePath}");

                // actual work to process
                // TODO: skip if not required (hash of filpath+file.io last modified OR already loaded)
                var result = BitmapUtils.LoadBitmap(request.BitmapFilePath, 1920);

                // return via callback
                request.Callback(result);

                IsBusy = false;
            }

            Log.Verbose("Bitmap load thread destroyed");
        }
    }
}
