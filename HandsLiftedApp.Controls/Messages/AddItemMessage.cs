namespace HandsLiftedApp.Controls.Messages
{
    public class AddItemMessage
    {
        public AddItemType Type { get; set; }

        public enum AddItemType
        {
            Presentation,
            GoogleSlides,
            PDF,
            ExistingSong,
            NewSong,
            Media,
            Logo,
            SectionHeading,
            MediaGroup,
            BlankGroup,
            BibleReadingSlideGroup
        }
    }
}
