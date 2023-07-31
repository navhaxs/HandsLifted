using Avalonia.Media.Imaging;
using Google.Apis.Slides.v1.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HandsLiftedApp.Utils
{
    internal class BitmapLoaderWorkQueue
    {
        public BitmapLoaderWorkQueue() {
            var _blockingCollection = new BlockingCollection<Job>(); // you may want to create bounded or unbounded collection
            var _consumingThread = new Thread(() =>
            {
                foreach (var workItem in _blockingCollection.GetConsumingEnumerable()) // blocks when there is no more work to do, continues whenever a new item is added.
                {
                    // do work with workItem
                }
            });
            _consumingThread.Start();
        }

        class Job
        {
            public string ImageFilePath { get; set; }
            public int ImageDecodeWidth { get; set; }
        }


        internal class Producer : IDisposable
        {
            private readonly BlockingCollection<RandomStringRequest> _collection;

            public Producer()
            {
                _collection = new BlockingCollection<RandomStringRequest>(new ConcurrentQueue<RandomStringRequest>());
            }

            public void Start()
            {
                //Task consumer = Task.Factory.StartNew(() => {
                //    try
                //    {
                //        foreach (var request in _collection.GetConsumingEnumerable())
                //        {
                //            Thread.Sleep(100); // long work
                //            if (File.Exists(path))
                //            {
                //                // todo try catch below line:
                //                using (Stream imageStream = File.OpenRead(path))
                //                {
                //                    try
                //                    {
                //                        // TODO should be using the BitmapLoader util
                //                        // Also should be on separate thread
                //                        Thumbnail = Bitmap.DecodeToWidth(imageStream, THUMBNAIL_WIDTH);
                //                    }
                //                    catch (Exception e)
                //                    {
                //                        //Log.Error($"Failed to decode image [{path}]");
                //                    }
                //                }
                //            }
                //            request.SetResult());
                //        }
                //    }
                //    catch (InvalidOperationException)
                //    {
                //        Console.WriteLine("Adding was compeleted!");
                //    }
                //});
            }

            public RandomStringRequest GetRandomString(string consumerName)
            {
                var request = new RandomStringRequest();
                _collection.Add(request);
                return request;
            }

            public void Dispose()
            {
                _collection.CompleteAdding();
            }

            private string GetRandomString()
            {
                var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                var random = new Random();
                var result = new string(Enumerable
                    .Repeat(chars, 8)
                    .Select(s => s[random.Next(s.Length)])
                    .ToArray());
                return result;
            }
        }

        internal class RandomStringRequest : IDisposable
        {
            private string _result;
            private ManualResetEvent _signal;

            public RandomStringRequest()
            {
                _signal = new ManualResetEvent(false);
            }

            public void SetResult(string result)
            {
                _result = result;
                _signal.Set();
            }

            public string GetResult()
            {
                _signal.WaitOne();
                return _result;
            }

            public bool TryGetResult(TimeSpan timeout, out string result)
            {
                result = null;
                if (_signal.WaitOne(timeout))
                {
                    result = _result;
                    return true;
                }
                return false;
            }

            public void Dispose()
            {
                _signal.Dispose();
            }
        }

        internal class Consumer
        {
            private Producer _producer;
            private string _name;

            public Consumer(
                Producer producer,
                string name)
            {
                _producer = producer;
                _name = name;
            }

            public string GetOrderedString()
            {
                using (var request = _producer.GetRandomString(_name))
                {
                    // wait here for result to be prepared
                    var produced = request.GetResult();
                    return String.Join(String.Empty, produced.OrderBy(c => c));
                }
            }
        }
    }
}
