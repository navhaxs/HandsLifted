namespace HandsLiftedApp.Models.WebsocketsV1
{
    internal class VisionScreensState
    {
        // TODO map Slide object with annotations e.g. [Published]Title
        public PublishedSlide CurrentSlide { get; set; }
    }

    abstract class PublishedSlide
    {
    }

    class PublishedSongSlide : PublishedSlide
    {
        public string SlideType = "Song";

        public string Text { get; set; }
    }
    class PublishedSongTitleSlide : PublishedSlide
    {
        public string SlideType = "SongTitle";
        public string Title { get; set; }
        public string Copyright { get; set; }
    }
}
