namespace HandsLiftedApp.Core.Models.AppState
{
    internal class ActionMessage
    {
        public NavigateSlideAction Action { get; set; }
        
        // TODO - clear LOGO / BLANK
        public enum NavigateSlideAction
        {
            NextSlide,
            PreviousSlide,
            GotoLogo,
            GotoBlank,
            // GotoFreeze,
        }
    }
}
