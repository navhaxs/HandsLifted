namespace HandsLiftedApp.Core.Models.UI
{
    public enum ActionType
    {
        CloseWindow,
        AboutWindow,
        WelcomeWindow,
        // PreferencesWindow
    }
    public class MainWindowMessage
    {
        public MainWindowMessage(ActionType actionType) { Action = actionType; }
        public ActionType Action { get; set; }
    }
}