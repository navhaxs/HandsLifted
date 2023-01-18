using Avalonia.Controls;

namespace HandsLiftedApp.Models.UI
{
    public class MainWindowModalMessage
    {
        public MainWindowModalMessage(Window Window) {this.Window = Window; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="window"></param>
        /// <param name="dataContext">Optional. Defaults to the MainWindowViewModel if unset.</param>
        public MainWindowModalMessage(Window Window, bool ShowAsDialog) : this(Window) {this.ShowAsDialog = ShowAsDialog; }
        public MainWindowModalMessage(Window Window, bool ShowAsDialog, object dataContext) : this(Window, ShowAsDialog) {this.DataContext = dataContext;}
        public Window Window { get; }
        public object? DataContext { get; } = null;
        public bool ShowAsDialog { get; } = true;
    }
}
