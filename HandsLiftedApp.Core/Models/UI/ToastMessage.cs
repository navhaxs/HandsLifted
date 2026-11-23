namespace HandsLiftedApp.Core.Models.UI
{
    /// <summary>
    /// Non-blocking notification shown briefly as an overlay on the main window (send via MessageBus).
    /// Use <see cref="MessageWindowViewModel"/> instead when the user must acknowledge something (errors).
    /// </summary>
    public class ToastMessage
    {
        public string Text { get; set; } = "";
        public int DurationMs { get; set; } = 2200;
    }
}
