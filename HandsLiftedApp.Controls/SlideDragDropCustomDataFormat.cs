using System;

namespace HandsLiftedApp.Controls
{
    public class SlideDragDropCustomDataFormat
    {
        public const string CustomFormat = "application/xxx-avalonia-controlcatalog-custom";
        
        // SlideReference
        
        public int SourceSlideIndex { get; set;}
        public Guid SourceItemUUID { get; set;}
    }
}