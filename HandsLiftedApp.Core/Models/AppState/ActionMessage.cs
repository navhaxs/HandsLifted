namespace HandsLiftedApp.Core.Models.AppState
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
            /// <summary>Clears Logo / Blank / QuickShow and shows the current slide again.</summary>
            GotoSlides,
            // GotoFreeze,
        }
    }
}
