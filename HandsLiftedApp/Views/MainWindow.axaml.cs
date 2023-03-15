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
using HandsLiftedApp.Views.Preferences;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HandsLiftedApp.Utils;
using HandsLiftedApp.Views.Prepare;
using HandsLiftedApp.Views.Editor;
using System.Diagnostics;
using Avalonia.Threading;

namespace HandsLiftedApp.Views {
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


            myClickWaitTimer = new DispatcherTimer(
                new TimeSpan(0, 0, 0, 0, 300),
                DispatcherPriority.Background,
                mouseWaitTimer_Tick
            );


            SubscribeToWindowState();

            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed;
            this.KeyDown += MainWindow_KeyDown;

            // When the window is activated, registers a handler for the ShowOpenFileDialog interaction.
            this.WhenActivated(d => d(ViewModel.ShowOpenFileDialog.RegisterHandler(ShowOpenFileDialog)));
            this.WhenActivated(d => d(ViewModel.ShowOpenFolderDialog.RegisterHandler(ShowOpenFolderDialog)));

            MessageBus.Current.Listen<MainWindowMessage>()
             .Subscribe(x =>
             {
                 switch (x.Action)
                 {
                     case ActionType.CloseWindow:
                         Close();
                         break;
                     case ActionType.AboutWindow:
                         AboutWindow a = new AboutWindow() { };
                         this.FindControl<Control>("shade").IsVisible = true;
                         a.ShowDialog(this);
                         a.Closed += (object? sender, EventArgs e) =>
                         {
                             this.FindControl<Control>("shade").IsVisible = false;
                         };
                         break;
                     case ActionType.PreferencesWindow:
                         PreferencesWindow p = new PreferencesWindow() { };
                         this.FindControl<Control>("shade").IsVisible = true;
                         p.ShowDialog(this);
                         p.Closed += (object? sender, EventArgs e) =>
                         {
                             this.FindControl<Control>("shade").IsVisible = false;
                         };
                         break;
                 }
             });

            MessageBus.Current.Listen<MainWindowModalMessage>()
             .Subscribe(x =>
             {

                 if (x.ShowAsDialog)
                     this.FindControl<Control>("shade").IsVisible = true;

                 //TODO do not always want to set DataContext if object has set it itself
                 if (x.Window.DataContext == null)
                     x.Window.DataContext = x.DataContext ?? this.DataContext;

                 x.Window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                 x.Window.Closed += (object? sender, EventArgs e) =>
                     {
                         this.FindControl<Control>("shade").IsVisible = false;
                     };

                 if (x.ShowAsDialog)
                     x.Window.ShowDialog(this);
                 else
                     x.Window.Show(this);
             });


            this.TemplateApplied += MainWindow_TemplateApplied;

            this.Loaded += (e, s) =>
            {
                updateWin32Border(this.WindowState);
            };

            this.GetObservable(Window.WindowStateProperty)
                .Subscribe(v =>
                {
                    updateWin32Border(v);
                });

            LibraryToggleButton.Click += (object? sender, RoutedEventArgs e) => {
                if (isLibraryVisible) {
                    lastLibraryContentGridLength = this.FindControl<Grid>("CentreGrid").RowDefinitions[2].Height;
                    lastLibrarySplitterGridLength = this.FindControl<Grid>("CentreGrid").RowDefinitions[1].Height;
                    this.FindControl<Grid>("CentreGrid").RowDefinitions[2].Height = new GridLength(0);
                    this.FindControl<Grid>("CentreGrid").RowDefinitions[1].Height = new GridLength(0);
                }
                else {
                    this.FindControl<Grid>("CentreGrid").RowDefinitions[2].Height = lastLibraryContentGridLength;
                    this.FindControl<Grid>("CentreGrid").RowDefinitions[1].Height = lastLibrarySplitterGridLength;
                }
                isLibraryVisible = !isLibraryVisible;
            };


            PlaylistTitleButton.PointerPressed += PlaylistTitleButton_PointerPressed;
            PlaylistTitleButton.PointerReleased += PlaylistTitleButton_PointerReleased;
            PlaylistTitleButton.PointerMoved += PlaylistTitleButton_PointerMoved;
            PlaylistTitleButton.DoubleTapped += PlaylistTitleButton_DoubleTapped;
            PlaylistTitleButton.Click += PlaylistTitleButton_Click;
            //PlaylistTitleButton.AddHandler(TappedEvent, this.RegisterTap, handledEventsToo: true);
        }

        private void PlaylistTitleButton_DoubleTapped(object? sender, TappedEventArgs e) {
            // Stop the timer from ticking.
            myClickWaitTimer.Stop();

            Trace.WriteLine("Double Click");
            e.Handled = true;

            this.WindowState = (this.WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        }

        private void PlaylistTitleButton_PointerMoved(object? sender, PointerEventArgs e) {
            if (mouseState != null) {
                this.BeginMoveDrag(mouseState);
            }
        }

        PointerPressedEventArgs? mouseState = null;

        private void PlaylistTitleButton_PointerReleased(object? sender, PointerReleasedEventArgs e) {
            mouseState = null;
        }
        /*
                public static event EventHandler<RoutedEventArgs> TapRegistered;
                private void RegisterTap(object sender, RoutedEventArgs e) => MainWindow.TapRegistered?.Invoke(this, e);
        */
        private DispatcherTimer myClickWaitTimer;
        private void mouseWaitTimer_Tick(object sender, EventArgs e) {
            myClickWaitTimer.Stop();

            // Handle Single Click Actions
            Trace.WriteLine("Single Click");

            MessageBus.Current.SendMessage(new MainWindowModalMessage(new PlaylistInfoEditorWindow()));
            //editPlaylistTitleFlyout.IsOpen = true;
        }
        private void PlaylistTitleButton_PointerPressed(object? sender, PointerPressedEventArgs e) {
            //this.BeginMoveDrag(e);
            mouseState = e;
            e.Handled = true;
        }

        private void PlaylistTitleButton_Click(object? sender, RoutedEventArgs e) {
            myClickWaitTimer.Start();
            //MessageBus.Current.SendMessage(new MainWindowModalMessage(new PlaylistInfoEditorWindow()));
            mouseState = null;
        }

        bool isLibraryVisible = false;
        GridLength lastLibraryContentGridLength = new GridLength(260);
        GridLength lastLibrarySplitterGridLength = new GridLength(0);

        void updateWin32Border(WindowState v)
        {
            if (v != WindowState.Maximized)
            {

                var margins = new Win32.MARGINS
                {
                    cyBottomHeight = 1,
                    cxRightWidth = 1,
                    cxLeftWidth = 1,
                    cyTopHeight = 1
                };

                Win32.DwmExtendFrameIntoClientArea(this.PlatformImpl.Handle.Handle, ref margins);
            }
        }



        private void Exit(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            MainWindow hostWindow = (MainWindow)this.VisualRoot;
            hostWindow.Close();
            //this.Close();
        }

        private void MainWindow_TemplateApplied(object? sender, Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            //StartWindow startWindow = new StartWindow();
            //startWindow.ShowDialog(this);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;

            this.FindControl<Control>("shade").IsVisible = true;

            ExitConfirmationWindow w = new ExitConfirmationWindow();
            w.Closed += (object? sender, EventArgs e) =>
            {
                this.FindControl<Control>("shade").IsVisible = false;
            };

            w.ShowDialog(this);
        }

        private void MainWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {

            // TODO: if a textbox, datepicker etc is selected - then skip this func.
            var m = FocusManager.Instance?.Current;

            if (m is TextBox || m is DatePicker)
                return;

            switch (e.Key)
            {
                case Key.F12:
                    ((MainWindowViewModel)this.DataContext).Playlist.State.IsLogo = !((MainWindowViewModel)this.DataContext).Playlist.State.IsLogo;
                    e.Handled = true;
                    break;
                case Key.PageDown:
                case Key.Right:
                case Key.Space:
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.NextSlide });
                    MessageBus.Current.SendMessage(new FocusSelectedItem());
                    e.Handled = true;
                    break;
                case Key.PageUp:
                case Key.Left:
                    MessageBus.Current.SendMessage(new ActionMessage() { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
                    MessageBus.Current.SendMessage(new FocusSelectedItem());
                    e.Handled = true;
                    break;
            }
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

        private void OnScrollToItemClick(object? sender, RoutedEventArgs e)
        {
            MessageBus.Current.SendMessage(new FocusSelectedItem());
            //MessageBus.Current.SendMessage(new Test());
        }
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            ((ClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current.ApplicationLifetime).Shutdown(0);
        }

        private async void SubscribeToWindowState()
        {
            Window hostWindow = (Window)this.VisualRoot;

            while (hostWindow == null)
            {
                hostWindow = (Window)this.VisualRoot;
                await Task.Delay(50);
            }

            hostWindow.GetObservable(Window.WindowStateProperty).Subscribe(s =>
            {
                if (s != WindowState.Maximized)
                {
                    hostWindow.Padding = new Thickness(0, 0, 0, 0);
                }
                if (s == WindowState.Maximized)
                {
                    hostWindow.Padding = new Thickness(7, 7, 7, 7);

                    // This should be a more universal approach in both cases, but I found it to be less reliable, when for example double-clicking the title bar.
                    /*hostWindow.Padding = new Thickness(
                            hostWindow.OffScreenMargin.Left,
                            hostWindow.OffScreenMargin.Top,
                            hostWindow.OffScreenMargin.Right,
                            hostWindow.OffScreenMargin.Bottom);*/
                }
            });
        }


    }
}
