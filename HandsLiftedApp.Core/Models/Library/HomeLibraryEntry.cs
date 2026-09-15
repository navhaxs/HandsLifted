namespace HandsLiftedApp.Core.Models.Library
{
    public class HomeLibraryEntry
    {
        public string FullPath { get; init; }
        public string Title { get; init; }
        public bool IsDirectory { get; init; }
    }

    public record BreadcrumbSegment(string Label, string RelativePath);
}
