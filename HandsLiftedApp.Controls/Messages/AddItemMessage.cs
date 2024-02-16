using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Controls.Messages
{
    public class AddItemMessage
    {
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
            BibleReadingSlideGroup
        }
    }
}
