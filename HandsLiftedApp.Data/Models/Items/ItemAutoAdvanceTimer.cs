using System.Xml.Serialization;
using ReactiveUI;

namespace HandsLiftedApp.Data.Data.Models.Items
{

    // TODO: need to define list of media, rather than Slide ??? for serialization
    //[XmlType(TypeName = "SlidesGroupItemX")]
    [XmlRoot("ItemAutoAdvanceTimer", Namespace = Constants.Namespace, IsNullable = false)]
    public class ItemAutoAdvanceTimer : ReactiveObject
    {

        public ItemAutoAdvanceTimer() { }

        private bool _isEnabled = false;
        /// <summary>
        /// Enable the auto-advance slide function
        /// </summary>
        public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }

        // private bool _IsLooping = false;
        // /// <summary>
        // /// Loop back to the first slide of the item once reaching the end 
        // /// </summary>
        // public bool IsLooping { get => _IsLooping; set => this.RaiseAndSetIfChanged(ref _IsLooping, value); }

        private int _intervalMs = 3_000;
        /// <summary>
        /// Interval (miliseconds)
        /// </summary>
        public int IntervalMs { get => _intervalMs; set => this.RaiseAndSetIfChanged(ref _intervalMs, value); }
    }
}
