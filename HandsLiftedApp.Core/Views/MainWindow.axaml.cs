using Avalonia.Controls;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Core.ViewModels;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Views.App;

namespace HandsLiftedApp.Core.Views;

public partial class MainWindow : ReactiveWindow<MainViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
        this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));

        MessageBus.Current.Listen<MessageWindowViewModel>().Subscribe(mwvm =>
        {
            MessageWindow mw = new MessageWindow() { DataContext = mwvm };
            mw.ShowDialog(this);
        });
    }

    private async Task ShowOpenFileDialog(InteractionContext<Unit, string[]?> interaction)
    {
        try
        {
            var dialog = new OpenFileDialog() { AllowMultiple = true };
            var fileNames = await dialog.ShowAsync(this);
            interaction.SetOutput(fileNames);
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
            interaction.SetOutput(null);
        }
    }

}