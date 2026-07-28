using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Controls.Messages
{
    public record AddItemMessage
    {
        public int? InsertIndex { get; init; }
        public Item? ItemToInsertAfter { get; init; }
        public AddItemType Type { get; init; }

        public enum AddItemType
        {
            Presentation,
            GoogleSlides,
            PDF,
            ExistingSong,
            NewSong,
            // Media,
            Logo,
            SectionHeading,
            MediaGroup,
            BlankGroup,
            BibleReadingSlideGroup,
            Comment,
            Scripture
        }

        // TODO make this an interface?
        public string? CreateInfo { get; init; } = null;

        public string? ScriptureBookCode { get; init; }
        public string? ScriptureBookName { get; init; }
        public int? ScriptureStartChapter { get; init; }
        public int? ScriptureStartVerse { get; init; }
        public int? ScriptureEndChapter { get; init; }
        public int? ScriptureEndVerse { get; init; }
    }
}
