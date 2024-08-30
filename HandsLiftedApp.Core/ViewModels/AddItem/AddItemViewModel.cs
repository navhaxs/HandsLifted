using System.Reactive;
using System.Reactive.Disposables;
using System.Windows.Input;
using HandsLiftedApp.Core.ViewModels.AddItem.Pages;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels.AddItem
{
    public class AddItemViewModel : ReactiveObject, IActivatableViewModel
    {
        public enum MyEnum
        {
            Value1,
            Value2,
            Value3,
        }
        
        private MyEnum _currentPage = MyEnum.Value1;

        public MyEnum CurrentPage
        {
            get => _currentPage;
            set => this.RaiseAndSetIfChanged(ref _currentPage, value);
        }
        public ViewModelActivator Activator { get; }
        public ICommand OpenCommand { get; }
 
        public int? ItemInsertIndex { get; set;  }

        internal LibraryViewModel LibraryViewModel;

        private AddItemPageViewModel _page;
        public AddItemPageViewModel Page
        {
            get => _page;
            set => this.RaiseAndSetIfChanged(ref _page, value);
        }
        
        public AddItemViewModel(LibraryViewModel libraryViewModel)
        {
            LibraryViewModel = libraryViewModel;
            
            Page = new StartViewModel(this);
            
            Activator = new ViewModelActivator();
            this.WhenActivated((CompositeDisposable disposables) =>
            {
                /* handle activation */
                Disposable
                    .Create(() => { /* handle deactivation */ })
                    .DisposeWith(disposables);
            });
            
            // The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
            ShowOpenFileDialog = new Interaction<Unit, string[]?>();
            
            OpenCommand = ReactiveCommand.CreateFromTask(async () =>
            {
               
            });
        }

        public Interaction<Unit, string[]?> ShowOpenFileDialog { get; }
    }
}