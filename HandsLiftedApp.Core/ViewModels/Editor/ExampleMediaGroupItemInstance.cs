using DynamicData;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    public class ExampleMediaGroupItemInstance : MediaGroupItemInstance
    {
        public ExampleMediaGroupItemInstance(): base(null)
        {
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/VisionScreens_1440_placeholder.png" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/VisionScreens_1440_placeholder.png" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/VisionScreens_1440_placeholder.png" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/VisionScreens_1440_placeholder.png" });
            var newSlideData = new CustomSlide();
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = newSlideData });
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
        }
    }
}