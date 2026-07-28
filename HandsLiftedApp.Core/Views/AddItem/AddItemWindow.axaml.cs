using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReactiveUI.Avalonia;
using HandsLiftedApp.Core.ViewModels.AddItem;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Views
{
    public partial class AddItemWindow : ReactiveWindow<AddItemViewModel>
    {
        public AddItemWindow()
        {
            if (!Design.IsDesignMode)
            {
                // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
                this.WhenActivated(d => d(ViewModel?.ShowOpenFileDialog?.RegisterHandler(ShowOpenFileDialog)));
            }
            
            Activated += delegate { AddItemContent.Focus(); };

            InitializeComponent();

            Closed += (sender, args) =>
            {
                Globals.Instance.MainViewModel.Playlist.ActiveItemInsertIndex = null;
            };
        }

        private async Task ShowOpenFileDialog(IInteractionContext<Unit, string[]?> interaction)
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    AllowMultiple = true
                });

                var fileNames = files.Select(f => f.TryGetLocalPath())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => p!)
                    .ToArray();

                interaction.SetOutput(fileNames);
            }
            catch (Exception e)
            {
                Log.Error(e, "Error showing open file dialog");
                interaction.SetOutput(null);
            }
        }

        private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}