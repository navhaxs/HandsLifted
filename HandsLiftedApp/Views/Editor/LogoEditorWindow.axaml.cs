using Avalonia;
using Avalonia.ReactiveUI;
using HandsLiftedApp.ViewModels;

namespace HandsLiftedApp.Views.Editor
{
    public partial class LogoEditorWindow : ReactiveWindow<GroupItemsEditorViewModel>
    {
        public LogoEditorWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }
    }
}
