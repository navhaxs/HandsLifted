using Avalonia.Controls;

namespace HandsLiftedApp.Models.UI
{
    public class MainWindowModalMessage
    {
        public MainWindowModalMessage(Window window) { this.window = window; }
        public Window window { get; }
    }
}
