using ReactiveUI;

namespace HandsLiftedApp.Core.Models.Library
{
    public class HomeLibraryEntry : ReactiveObject
    {
        public string FullPath { get; init; }
        public string Title { get; init; }
        public bool IsDirectory { get; init; }

        // Directory tiles show a 2x2 collage of up to four files found inside (searching nested
        // subfolders), filled in asynchronously by MediaLibraryQueryViewModel. Individual
        // properties rather than a list so XAML can bind each cell without index-out-of-range noise.
        private string? _preview1;
        public string? Preview1 { get => _preview1; set => this.RaiseAndSetIfChanged(ref _preview1, value); }

        private string? _preview2;
        public string? Preview2 { get => _preview2; set => this.RaiseAndSetIfChanged(ref _preview2, value); }

        private string? _preview3;
        public string? Preview3 { get => _preview3; set => this.RaiseAndSetIfChanged(ref _preview3, value); }

        private string? _preview4;
        public string? Preview4 { get => _preview4; set => this.RaiseAndSetIfChanged(ref _preview4, value); }

        public void SetPreviews(System.Collections.Generic.IReadOnlyList<string> files)
        {
            Preview1 = files.Count > 0 ? files[0] : null;
            Preview2 = files.Count > 1 ? files[1] : null;
            Preview3 = files.Count > 2 ? files[2] : null;
            Preview4 = files.Count > 3 ? files[3] : null;
        }
    }

    public record BreadcrumbSegment(string Label, string RelativePath);
}
