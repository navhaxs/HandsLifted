using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.SlideElement;

namespace HandsLiftedApp.Core.ViewModels.Editor
{
    public class MockMediaGroupItemInstance : MediaGroupItemInstance
    {
        public MockMediaGroupItemInstance() : base(null)
        {
            Title = "Sample Media Group";

            // Add various media items for design-time visualization
            Items.Add(new MediaItem() { SourceMediaFilePath = "/Assets/sample1.jpg" });
            Items.Add(new MediaItem() { SourceMediaFilePath = "/Assets/sample2.png" });
            Items.Add(new MediaItem() { SourceMediaFilePath = "/Assets/sample3.jpg" });

            // Add a custom slide item
            Items.Add(new SlideItem() 
            { 
                SlideData = new CustomSlide { SlideElements = new() { new TextElement() } }
            });

            Items.Add(new MediaItem() { SourceMediaFilePath = "/Assets/sample4.png" });
            Items.Add(new MediaItem() { SourceMediaFilePath = "/Assets/sample5.jpg" });

            // Configure auto advance timer
            AutoAdvanceTimer.IsEnabled = true;
            AutoAdvanceTimer.IntervalMs = 3000;
        }
    }
}
