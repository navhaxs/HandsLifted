using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.ItemState;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp
{
    internal class Types
    {
        internal class Items
        {
            internal static Type GOOGLE_SLIDES = typeof(GoogleSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, GoogleSlidesGroupItemStateImpl>);
            internal static Type POWERPOINT = typeof(PowerPointSlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl, PowerPointSlidesGroupItemStateImpl>);
        }


    }
}
