using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Core.ViewModels;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views
{
    public partial class AddItemView : ReactiveWindow<AddItemViewModel>
    {
        public AddItemView()
        {
            ViewModel = new AddItemViewModel();
            // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
            this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));
            
            Activated += delegate { AddItemContent.Focus(); };

            InitializeComponent();
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

        private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}