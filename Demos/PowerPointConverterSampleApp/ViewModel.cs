using System;
using ReactiveUI;

namespace PowerPointConverterSampleApp
{
    public enum DocumentStatus
    {
        Inactive,
        Active,
        Error
    }

    public class ViewModel : ReactiveObject 
    {
        public ViewModel()
        {
            this.WhenAnyValue(x => x.Status)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(IsActive)));
        }
        
        private String? _filePath;
        public String? FilePath
        {
            get => _filePath;
            set => this.RaiseAndSetIfChanged(ref _filePath, value);
        }
        
        private bool _isBusy = false;
        public bool IsBusy
        {
            get => _isBusy;
            set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        private DocumentStatus _status = DocumentStatus.Inactive;
        public DocumentStatus Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public bool IsActive => Status == DocumentStatus.Active;

        private bool _progressIsIndeterminate = false;
        public bool ProgressIsIndeterminate
        {
            get => _progressIsIndeterminate;
            set => this.RaiseAndSetIfChanged(ref _progressIsIndeterminate, value);
        }

        private double _progressIsValue = 0;
        public double ProgressValue
        {
            get => _progressIsValue;
            set => this.RaiseAndSetIfChanged(ref _progressIsValue, value);
        }

    }
}