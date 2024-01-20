using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.ViewModels;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels;

public class MainViewModel : ViewModelBase
{
#pragma warning disable CA1822 // Mark members as static
    public string Greeting => "Welcome to the VisionScreens Web Editor! :D";

    private Playlist _Playlist;
    public Playlist Playlist { get => _Playlist; set => this.RaiseAndSetIfChanged(ref _Playlist, value); }
#pragma warning restore CA1822 // Mark members as static
}
