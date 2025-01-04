using System;

namespace HandsLiftedApp.Core.Models.UI
{
    public class MoveSlideCommand
    {
        // SlideReference
        
        public int SourceSlideIndex { get; set;}
        public Guid SourceItemUUID { get; set;}
        
        // SlideReference
        
        public int DestSlideIndex { get; set;}
        public Guid DestItemUUID { get; set;}
    }
}