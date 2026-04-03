using System.Threading.Tasks;

namespace HandsLiftedApp.Core.Models.UI
{
    public class ShowRestoreAutosaveConfirmationAction
    {
        public TaskCompletionSource<bool> TaskCompletionSource { get; } = new();
    }
}