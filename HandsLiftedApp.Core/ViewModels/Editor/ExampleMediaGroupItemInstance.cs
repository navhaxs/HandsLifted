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
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide1.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide2.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide3.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide4.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide5.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide6.PNG" });
            var newSlideData = new CustomSlide();
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = newSlideData });
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
            this.Items.Add(new MediaGroupItem.SlideItem() { SlideData = new CustomSlide() });
        }
    }
}