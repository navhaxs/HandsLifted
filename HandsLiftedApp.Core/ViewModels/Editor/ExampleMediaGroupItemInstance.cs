using DynamicData;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    internal class ExampleMediaGroupItemInstance : MediaGroupItemInstance
    {
        public ExampleMediaGroupItemInstance(PlaylistInstance parentPlaylist = null) : base(parentPlaylist)
        {
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide1.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide2.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide3.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide4.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide5.PNG" });
            this.Items.Add(new MediaGroupItem.MediaItem() { SourceMediaFilePath = "D:\\VisionScreensCore-TestData\\Slide6.PNG" });
        }
    }
}