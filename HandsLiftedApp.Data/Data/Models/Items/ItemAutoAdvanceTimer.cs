using ReactiveUI;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{

    // TODO: need to define list of media, rather than Slide ??? for serialization
    //[XmlType(TypeName = "SlidesGroupItemX")]
    [XmlRoot("ItemAutoAdvanceTimer", Namespace = Constants.Namespace, IsNullable = false)]
    public class ItemAutoAdvanceTimer : ReactiveObject
    {

        public ItemAutoAdvanceTimer() { }

        private bool _IsEnabled = false;
        /// <summary>
        /// Enable the auto-advance slide function
        /// </summary>
        public bool IsEnabled { get => _IsEnabled; set => this.RaiseAndSetIfChanged(ref _IsEnabled, value); }

        private bool _IsLooping = false;
        /// <summary>
        /// Loop back to the first slide of the item once reaching the end 
        /// </summary>
        public bool IsLooping { get => _IsLooping; set => this.RaiseAndSetIfChanged(ref _IsLooping, value); }

        private int _IntervalMs = 3000;
        /// <summary>
        /// Interval (miliseconds)
        /// </summary>
        public int IntervalMs { get => _IntervalMs; set => this.RaiseAndSetIfChanged(ref _IntervalMs, value); }
    }

    // instance
    public interface IItemAutoAdvanceTimerState
    {

    }
}
