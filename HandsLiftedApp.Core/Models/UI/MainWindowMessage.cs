namespace HandsLiftedApp.Core.Models.UI
{
    public enum ActionType
    {
        CloseWindow,
        AboutWindow,
        // PreferencesWindow
    }
    public class MainWindowMessage
    {
        public MainWindowMessage(ActionType actionType) { Action = actionType; }
        public ActionType Action { get; set; }
    }
}