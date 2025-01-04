using System;
using System.Collections.Generic;
using Avalonia.Platform.Storage;

namespace HandsLiftedApp.Core.Models.UI
{
    public class AddFilesToGroupItemCommand
    {
        public IEnumerable<IStorageItem> SourceFiles { get; set;}
        
        // SlideReference
        
        public int DestSlideIndex { get; set;}
        public Guid DestItemUUID { get; set;}
    }
}