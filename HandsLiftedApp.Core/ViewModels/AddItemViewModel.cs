using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels
{
    public class AddItemViewModel : ReactiveObject, IActivatableViewModel
    {
        public ViewModelActivator Activator { get; }
        
        public AddItemViewModel()
        {
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
        }

        public Interaction<Unit, string[]?> ShowOpenFileDialog { get; }

    }
}