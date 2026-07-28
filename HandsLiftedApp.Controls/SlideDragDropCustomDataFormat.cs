using Avalonia.Input;
using System;

namespace HandsLiftedApp.Controls
{
    public class SlideDragDropCustomDataFormat
    {
        public static readonly DataFormat<SlideDragDropCustomDataFormat> Format =
            DataFormat.CreateInProcessFormat<SlideDragDropCustomDataFormat>("application/xxx-avalonia-controlcatalog-custom");

        // SlideReference

        public int SourceSlideIndex { get; set;}
        public Guid SourceItemUUID { get; set;}
    }
}