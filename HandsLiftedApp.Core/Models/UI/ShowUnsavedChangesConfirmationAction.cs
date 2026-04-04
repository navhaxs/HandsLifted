using System.Threading.Tasks;
using HandsLiftedApp.Core.Views.Confirmation;

namespace HandsLiftedApp.Core.Models.UI
{
    public class ShowUnsavedChangesConfirmationAction
    {
        public TaskCompletionSource<UnsavedChangesConfirmationWindow.DialogResult> TaskCompletionSource { get; } = new();
    }
}
