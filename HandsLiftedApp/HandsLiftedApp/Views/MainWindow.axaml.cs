using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Models.AppState;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.Views.App;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace HandsLiftedApp.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            if (Design.IsDesignMode)
                return;

            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed;
            this.KeyDown += MainWindow_KeyDown;

            // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
            this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));
            this.WhenActivated(d => d(ViewModel.ShowOpenFolderDialog.RegisterHandler(ShowOpenFolderDialog)));

            //OrderableListBox = this.FindControl<ListBox>("itemsListBox");
            //OrderableListBox.PointerReleased += X_PointerReleased;

            this.TemplateApplied += MainWindow_TemplateApplied;
        }

        private void MainWindow_TemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            //StartWindow startWindow = new StartWindow();
            //startWindow.ShowDialog(this);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {

            // TODO confirm
            //throw new NotImplementedException();
        }
        private void MainWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F12:
                    ((MainWindowViewModel)this.DataContext).Playlist.State.IsLogo = !((MainWindowViewModel)this.DataContext).Playlist.State.IsLogo;
            break;
            }
            e.Handled = true;
        }

        private async Task ShowOpenFileDialog(InteractionContext<Unit, string?> interaction)
        {
            try
            {
                var dialog = new OpenFileDialog();
                var fileNames = await dialog.ShowAsync(this);
                interaction.SetOutput(fileNames != null ? fileNames.FirstOrDefault() : null);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
                interaction.SetOutput(null);
            }
        } 
        private async Task ShowOpenFolderDialog(InteractionContext<Unit, string?> interaction)
        {
            try
            {
                var dialog = new OpenFolderDialog();
                var folder = await dialog.ShowAsync(this);
                interaction.SetOutput(folder);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
                interaction.SetOutput(null);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnScrollToItemClick(object? sender, RoutedEventArgs e)
        {
            MessageBus.Current.SendMessage(new FocusSelectedItem());
        }

        private void MainWindow_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            //if (e.Source is Border)
            //    this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        }
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            ((ClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current.ApplicationLifetime).Shutdown(0);
        }

    }
}
