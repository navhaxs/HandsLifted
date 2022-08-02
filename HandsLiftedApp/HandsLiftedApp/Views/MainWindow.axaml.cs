using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using HandsLiftedApp.ViewModels;
using System.Linq;
using HandsLiftedApp.Views.Editor;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace HandsLiftedApp.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {

        ListBox OrderableListBox;
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
            this.KeyDown += ZoomBorder_KeyDown;

            // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
            this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));

            //OrderableListBox = this.FindControl<ListBox>("itemsListBox");
            //OrderableListBox.PointerReleased += X_PointerReleased;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {

            // TODO confirm
            //throw new NotImplementedException();
        }

        //private void X_PointerReleased(object? sender, PointerReleasedEventArgs e)
        //{
        //    var hoveredItem = (ListBoxItem)OrderableListBox.GetLogicalChildren().FirstOrDefault(x => this.GetVisualsAt(e.GetPosition(this)).Contains(((IVisual)x).GetVisualChildren().First()));
        //    if (DragItem == null ||
        //        hoveredItem == null ||
        //        DragItem == hoveredItem)
        //    {
        //        return;
        //    }

        //    Items.Move(
        //        OrderableListBox.GetLogicalChildren().ToList().IndexOf(DragItem),
        //        OrderableListBox.GetLogicalChildren().ToList().IndexOf(hoveredItem));

        //    DragItem = null;
        //}

        private async Task ShowOpenFileDialog(InteractionContext<Unit, string?> interaction)
        {
            try
            {
                var dialog = new OpenFileDialog();
                var fileNames = await dialog.ShowAsync(this);
                interaction.SetOutput(fileNames.FirstOrDefault());
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

        private void ZoomBorder_KeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                ListBox _list = this.Find<ListBox>("List");

                switch (e.Key)
                {
                    case Key.Right:
                        //_list.SelectedIndex += 1;
                        break;
                    case Key.Left:
                        //_list.SelectedIndex -= 1;
                        break;
                }
            }
            catch (Exception)
            {

                throw;
            }

        }

        private void MainWindow_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (e.Source is Border)
                this.WindowState = (this.WindowState == WindowState.FullScreen) ? WindowState.Normal : WindowState.FullScreen;
        }
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            ((ClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current.ApplicationLifetime).Shutdown(0);
        }

    }
}
