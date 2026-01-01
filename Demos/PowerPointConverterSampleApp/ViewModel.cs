using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using ReactiveUI;

namespace PowerPointConverterSampleApp
{
    public enum ApplicationState
    {
        Init,
        Ready,
        Busy,
        Completed
    }
    

    public class ViewModel : ReactiveObject 
    {
        public static readonly string[] SUPPORTED_POWERPOINT = { "ppt", "pptx", "odp" };
        
        public ViewModel()
        {
            this.WhenAnyValue(x => x.Status)
                .Select(s => s is ApplicationState.Init)
                .ToProperty(this, x => x.IsInit, out _isInit);
            this.WhenAnyValue(x => x.Status)
                .Select(s => s is ApplicationState.Ready)
                .ToProperty(this, x => x.IsReady, out _isReady);
            this.WhenAnyValue(x => x.Status)
                .Select(s => s is ApplicationState.Busy)
                .ToProperty(this, x => x.IsBusy, out _isBusy);
            this.WhenAnyValue(x => x.Status)
                .Select(s => s is ApplicationState.Completed)
                .ToProperty(this, x => x.IsCompleted, out _isCompleted);
            this.WhenAnyValue(x => x.FilePath)
                .Select(s => s != null && s.ToLower().EndsWith(".pdf"))
                .ToProperty(this, x => x.IsPDF, out _isPdf);
            this.WhenAnyValue(x => x.FilePath)
                .Select(s => s != null && SUPPORTED_POWERPOINT.Any(x => s.ToLower().EndsWith(x)))
                .ToProperty(this, x => x.IsPPTX, out _isPptx);
            this.WhenAnyValue(x => x.ProgressValue, x => x.Status)
                .Select(x => x is { Item1: 0, Item2: ApplicationState.Busy })
                .ToProperty(this, x => x.ProgressIsIndeterminate, out _progressIsIndeterminate);
            this.WhenAnyValue(x => x.FilePath)
                .Select(x => x != null ? Path.GetFileName(x) : null)
                .ToProperty(this, x => x.FileName, out _fileName);
            this.WhenAnyValue(x => x.OutputFilePath)
                .Select(path =>
                {
                    if (string.IsNullOrEmpty(path)) return null;
                        
                    try
                    {
                        return Directory.Exists(path) ? null : Path.GetFileName(path);
                    }
                    catch
                    {
                        return null;
                    }
                })
                .ToProperty(this, x => x.OutputFileName, out _outputFileName);    }
        
        private ApplicationState _status = ApplicationState.Init;
        public ApplicationState Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }
           
        private readonly ObservableAsPropertyHelper<bool> _isInit;
        public bool IsInit => _isInit.Value;
          private readonly ObservableAsPropertyHelper<bool> _isReady;
        public bool IsReady => _isReady.Value;
              
        private readonly ObservableAsPropertyHelper<bool> _isBusy;
        public bool IsBusy => _isBusy.Value;
        
        private readonly ObservableAsPropertyHelper<bool> _isCompleted;
        public bool IsCompleted => _isCompleted.Value;

        private readonly ObservableAsPropertyHelper<bool> _isPdf;
        public bool IsPDF => _isPdf.Value;
        
        private readonly ObservableAsPropertyHelper<bool> _isPptx;
        public bool IsPPTX => _isPptx.Value;
                
        private String? _filePath;
        public String? FilePath
        {
            get => _filePath;
            set => this.RaiseAndSetIfChanged(ref _filePath, value);
        }
        
        private readonly ObservableAsPropertyHelper<string?> _fileName;
        public string? FileName => _fileName.Value;
        
        private String _progressStatusMessage;
        public String ProgressStatusMessage
        {
            get => _progressStatusMessage;
            set => this.RaiseAndSetIfChanged(ref _progressStatusMessage, value);
        }
        
        private readonly ObservableAsPropertyHelper<bool> _progressIsIndeterminate;
        public bool ProgressIsIndeterminate
        {
            get => _progressIsIndeterminate.Value;
        }

        private double _progressIsValue = 0;
        public double ProgressValue
        {
            get => _progressIsValue;
            set => this.RaiseAndSetIfChanged(ref _progressIsValue, value);
        }
        
        // set by task for reporting
        private string? _outputFilePath;
        public string? OutputFilePath
        {
            get => _outputFilePath;
            set => this.RaiseAndSetIfChanged(ref _outputFilePath, value);
        }

        private readonly ObservableAsPropertyHelper<string?> _outputFileName;
        public string? OutputFileName
        {
            get => _outputFileName.Value;
        }
    }
}