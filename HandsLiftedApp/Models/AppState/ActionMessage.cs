using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.AppState
{
    internal class ActionMessage
    {
        public NavigateSlideAction Action { get; set; }
        public enum NavigateSlideAction
        {
            NextSlide,
            PreviousSlide,
            GotoLogo,
            GotoBlank,
            GotoFreeze,
        }
    }
}
