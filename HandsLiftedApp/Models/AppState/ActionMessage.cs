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
