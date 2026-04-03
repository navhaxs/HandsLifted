using System.Threading.Tasks;
using HandsLiftedApp.Core.Views;

namespace HandsLiftedApp.Core.Models.UI
{
    public class ShowUnsavedChangesConfirmationAction
    {
        public TaskCompletionSource<UnsavedChangesConfirmationWindow.DialogResult> TaskCompletionSource { get; } = new();
    }
}
