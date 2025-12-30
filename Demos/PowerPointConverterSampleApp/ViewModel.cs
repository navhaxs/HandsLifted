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

        private DocumentStatus _status = DocumentStatus.Inactive;
        public DocumentStatus Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public bool IsActive => Status == DocumentStatus.Active;
    }
}