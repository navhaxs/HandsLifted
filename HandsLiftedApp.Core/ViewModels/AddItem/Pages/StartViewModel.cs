namespace HandsLiftedApp.Core.ViewModels.AddItem.Pages
{
    public class StartViewModel(AddItemViewModel addItemViewModel) : AddItemPageViewModel(addItemViewModel)
    {
        public LibraryViewModel LibraryViewModel
        {
            get => AddItemViewModel.LibraryViewModel;
        }
    }
}