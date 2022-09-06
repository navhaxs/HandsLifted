using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    // TODO: need to define list of media, rather than Slide ??? for serialization
    public class GoogleSlidesGroupItem<I> : SlidesGroupItem<I> where I : IItemState
    {
        public GoogleSlidesGroupItem()
        {
        }
    }
}
