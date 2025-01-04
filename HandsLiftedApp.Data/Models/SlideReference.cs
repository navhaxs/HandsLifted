using System;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core.Models
{
    public class SlideReference
    {
        public int? SlideIndex { get; set; }
        public Slide? Slide { get; set; }
        
        public Guid? ItemUUID { get; set;}

        public int? ItemIndex { get; set; }

        public override string? ToString()
        {
            return $"ItemIndex={ItemIndex} SlideIndex={SlideIndex}";
        }
    }
}
