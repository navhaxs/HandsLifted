using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Views.Designer
{
    public class DesignModeSongSlideInstance : SongSlideInstance
    {
        public DesignModeSongSlideInstance() : base(null, null, "design-mode-song-slide")
        {
            Text = "This is a song slide.\n\nYou can edit the text and it will update the thumbnail.";
            Theme = new BaseSlideTheme() {FontSize = 70};
        }
    }
}