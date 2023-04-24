using Avalonia.Controls;
using Avalonia.Threading;
using HandsLiftedApp.Utils;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace HandsLiftedApp.Views.Debugging
{
    public partial class LogViewerWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string _LogData;
        public string LogData { get => _LogData; set
            {
                _LogData = value;
                OnPropertyChanged(nameof(LogData));
            }
        }

        public LogViewerWindow()
        {
            InitializeComponent();

            MyTextBox.DataContext = this;

            Action a = () =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MyTextBox.CaretIndex = int.MaxValue;
                });
            };

            var debouncedWrapper = a.Debounce();

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                /* run your code here */

                Watcher w = new Watcher();
                w.MyEvent += (string s1) =>
                {
                    LogData += s1;
                    debouncedWrapper();
                };
                w.Start();
            }).Start();
        }

        class Watcher
        {
            //private static CancellationTokenSource ctSource;

            public event Action<string> MyEvent;

            string FILE_TO_READ = @"logs\visionscreens_app_log.txt";
            public void Start()
            {

                //ctSource = new CancellationTokenSource();

                var wh = new AutoResetEvent(false);
                var fsw = new FileSystemWatcher(".");
                fsw.Filter = FILE_TO_READ;
                fsw.EnableRaisingEvents = true;
                fsw.Changed += (s, e) => wh.Set();

                var fs = new FileStream(FILE_TO_READ, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (var sr = new StreamReader(fs))
                {
                    var s = "";

                    // read initial block
                    s = sr.ReadToEnd();
                    if (s != null)
                    {
                        if (MyEvent != null)
                            MyEvent(s);
                    }

                    while (true)
                    {
                        //if (ctSource.IsCancellationRequested)
                        //    break;
                        
                        s = sr.ReadLine();

                        if (s != null)
                        {
                            if (MyEvent != null)
                                MyEvent(s + Environment.NewLine);
                        }
                        else
                            wh.WaitOne(1000);
                            //wh.WaitOne(1000);
                        //Thread.Sleep(1000);
                    }
                }

                wh.Close();
            }

        }
    }
}
