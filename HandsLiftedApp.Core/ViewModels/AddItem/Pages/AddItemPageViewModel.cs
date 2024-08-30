using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels.AddItem.Pages
{
    public abstract class AddItemPageViewModel(AddItemViewModel _addItemViewModel) : ReactiveObject
    {
        internal AddItemViewModel AddItemViewModel = _addItemViewModel;
    }
}