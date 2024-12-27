using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels
{
    public class LibraryItemPreviewViewModel : ReactiveObject
    {
        public LibraryItemPreviewViewModel(LibraryItem libraryItem)
        {
            this._libraryItem = libraryItem;
        }
        
        private LibraryItem _libraryItem;

        private Item _item;
        public Item Item
        {
            get => _item;
            set => this.RaiseAndSetIfChanged(ref _item, value);
        }
    }
}