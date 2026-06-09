using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Common;
using HandsLiftedApp.Core.Controller;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Models.RuntimeData.Slides;
using HandsLiftedApp.Core.Render.Skia;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Core.Utils.MacOS;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Views
{
    public partial class ProjectorWindow : Window, INotifyPropertyChanged
    {
        public ProjectorWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
                return;

            // Main NDI output (motion background + slide canvas + video layer):
            // high-res when a motion background is playing or a slide transition is active.
            NdiMainContainer.IsContentHighResCheckFunc = _ =>
                MotionBackgroundService.CurrentContext != null || MainSlideCanvas.IsTransitioning;

            // Lyrics NDI output (AltSlideRenderer only): high-res during slide transitions.
            NdiLyricsContainer.IsContentHighResCheckFunc = _ =>
                MainSlideCanvas.IsTransitioning;

            Log.Information("Created ProjectorWindow");

            if (OperatingSystem.IsWindows())
            {
                WindowUtils.RegisterWindowWatcher(this);
            }

            this.WhenAnyValue(value => value.IsVisible).Subscribe(isVisible =>
            {
                Log.Information("ProjectorWindow IsVisible {State}", isVisible);

                // TODO macOS: keep awake
                if (OperatingSystem.IsWindows() && !Debugger.IsAttached)
                {
                    Caffeine.KeepAwake(isVisible);
                }
            });

            MessageBus.Current.Listen<OutputDisplayConfigurationChangeMessage>()
                .Subscribe(async msg =>
                {
                    if (IsVisible && msg.ChangedDisplay == OutputDisplayConfigurationChangeMessage.Display.Projector)
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                            WindowUtils.ShowAndRestoreWindowBounds(this,
                                Globals.Instance.AppPreferences.OutputDisplayBounds));
                    }
                });

            RegisterControlsOverlayTimer();
           
            this.PositionChanged += (s, e) =>
            {
                BoundPosition = e.Point;
                if (aTimer != null && ControlsOverlay.IsVisible)
                {
                    aTimer.Stop();
                    aTimer.Start();
                }
            };
        }

        private IDisposable? _slideSubscription;
        private MainViewModel? _vm;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _slideSubscription?.Dispose();

            if (DataContext is not MainViewModel vm) return;
            _vm = vm;

            _slideSubscription = vm.Playlist
                .WhenAnyValue(p => p.ActiveSlide)
                .Subscribe(OnActiveSlideChanged);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _slideSubscription?.Dispose();
        }

        private void OnActiveSlideChanged(Slide? slide)
        {
            // Bitmap preload is intentionally omitted here — LivePane.OnActiveSlideChanged
            // already runs Task.Run(Preload) for image slides on the same ActiveSlide change,
            // and the bitmap cache is shared. A second preload call would race to find the
            // entry already cached (no-op) but wastes a thread-pool job.

            var logoPath = NormalizeMediaPath(_vm?.Playlist.LogoGraphicFile);
            Log.Debug("[ProjectorWindow] OnActiveSlideChanged: {SlideType}, ImagePath={Path}, LogoPath={Logo}",
                slide?.GetType().Name ?? "null",
                (slide as ImageSlideInstance)?.SourceMediaFilePath ?? "-",
                logoPath ?? "-");

            SlideRenderSpec? spec = slide switch
            {
                SongSlideInstance s      => SongSlideSpecBuilder.Build(s),
                SongTitleSlideInstance t => SongTitleSlideSpecBuilder.Build(t),
                ImageSlideInstance img   => string.IsNullOrWhiteSpace(img.SourceMediaFilePath)
                    ? null
                    : new SlideRenderSpec(new ImageBackground(img.SourceMediaFilePath), Array.Empty<RenderElement>()),
                LogoSlide                => string.IsNullOrWhiteSpace(logoPath)
                    ? null
                    : new SlideRenderSpec(new ImageBackground(logoPath), Array.Empty<RenderElement>()),
                HandsLiftedApp.Data.Data.Models.Slides.CustomSlide cs => CustomSlideSpecBuilder.Build(cs),
                _                        => null,
            };

            // Skip animation when the projector window is hidden — avoids starting a
            // DispatcherTimer that fires InvalidateVisual() on an invisible window.
            if (!IsVisible)
            {
                MainSlideCanvas.Spec = spec;
                return;
            }

            MainSlideCanvas.Transition(spec, TimeSpan.FromMilliseconds(_vm?.Playlist.SlideTransitionDurationMs ?? 120));
        }

        /// <summary>
        /// Repairs paths where the serializer has mangled an avares:// URI into a Windows-style
        /// absolute path (e.g. "C:\...\avares:\Assembly\Assets\...").
        /// Returns the corrected avares:// URI, or the original path unchanged for normal files.
        /// </summary>
        private static string? NormalizeMediaPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            var idx = path.IndexOf("avares:", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                // Repair paths mangled by the serializer, e.g.:
                //   "C:\VisionScreens Data\avares:\Assembly\Assets\logo.png"
                // → "avares://Assembly/Assets/logo.png"
                var rest = path.Substring(idx + "avares:".Length)
                               .Replace('\\', '/')
                               .TrimStart('/');
                if (rest.Length == 0) return path; // malformed — return original unchanged
                return "avares://" + rest;
            }

            return path;
        }

        private bool _isFullScreen = false;
        public bool IsFullScreen
        {
            get => _isFullScreen;
            set => SetProperty(ref _isFullScreen, value);
        }
        
        private PixelPoint _boundPosition;

        public PixelPoint BoundPosition
        {
            get => _boundPosition;
            set => SetProperty(ref _boundPosition, value);
        }

        private System.Timers.Timer? aTimer;
        public void RegisterControlsOverlayTimer()
        {
            ControlsOverlay.IsVisible = false;

            aTimer = new System.Timers.Timer();
            aTimer.Elapsed += ((sender, args) =>
            {
                aTimer.Stop();
                Dispatcher.UIThread.InvokeAsync(() => ControlsOverlay.IsVisible = false);
            });
            aTimer.Interval = 5000; // ~ 5 seconds
            aTimer.AutoReset = false; // one-shot; restarted on each pointer move

            this.PointerMoved += (sender, args) =>
            {
                ControlsOverlay.IsVisible = true;
                aTimer.Stop();
                aTimer.Start();
            };
        }
        
        Rect? previousBounds = null;
        PixelPoint? previousPosition = null;

        public void onToggleFullscreen()
        {
            bool isRequestingFullscreen = !IsFullScreen;
            
            SetTopmost(isRequestingFullscreen);

            if (OperatingSystem.IsMacOS())
            {
                this.SystemDecorations = isRequestingFullscreen ? SystemDecorations.None : SystemDecorations.Full;
            }
            
            if (isRequestingFullscreen)
            {
                var screen = Screens.ScreenFromPoint(Position) ?? Screens.ScreenFromWindow(this);

                if (screen == null)
                {
                    Log.Error("Unable to find screen for projector window");
                    return;
                }

                this.previousBounds = this.Bounds;
                this.previousPosition = this.Position;
                
                this.Height = screen.Bounds.Height;
                this.Width = screen.Bounds.Width;
                
                this.Position = screen.Bounds.Position;
            }
            
            this.ShowInTaskbar = !isRequestingFullscreen; // make this user option
            
            if (OperatingSystem.IsWindows())
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.WindowState = isRequestingFullscreen ? WindowState.FullScreen : WindowState.Normal;
                    

                    if (!isRequestingFullscreen && previousBounds != null)
                    {
                        // Dispatcher.UIThread.InvokeAsync(() =>
                        // {
                        //     Thread.Sleep(1_000);

                            this.Height = previousBounds.Value.Height;
                            this.Width = previousBounds.Value.Width;
                        // });
                    }
                });
            }
            
            if (OperatingSystem.IsMacOS() && !isRequestingFullscreen)
            {
                this.Position = (PixelPoint)previousPosition;

                this.Height = previousBounds.Value.Height;
                this.Width = previousBounds.Value.Width;
            }
            
            IsFullScreen = isRequestingFullscreen;
        }

        private void ProjectorWindow_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (ControlExtension.FindAncestor<Button>((Control)e.Source) != null)
                return;

            onToggleFullscreen();
        }

        private void ProjectorWindow_KeyDown(object? sender, KeyEventArgs e)
        {
            // var window = TopLevel.GetTopLevel(this);
            //
            // // if (!window.IsFocused)
            // // {
            // //     return;
            // // }
            //
            // // TODO: if a textbox, datepicker etc is selected - then skip this func.
            // var focusManager = TopLevel.GetTopLevel(this).FocusManager;
            // var focusedElement = focusManager.GetFocusedElement();
            //
            // if (focusedElement is TextBox || focusedElement is DatePicker)
            //     return;

            KeyboardSlideNavigation.OnKeyDown(e);
        }

        private void Window_OnClosing(object? sender, WindowClosingEventArgs e)
        {
            this.IsVisible = false;
            e.Cancel = true;
        }

        private void ToggleFullscreen_OnClick(object? sender, RoutedEventArgs e)
        {
            onToggleFullscreen();
        }

        private void ToggleTopmost_OnClick(object? sender, RoutedEventArgs e)
        {
            SetTopmost(!this.Topmost);
        }

        private void SetTopmost(bool requestTopmost)
        {
            // do not set Topmost flag on macOS - check if this breaks on Windows OS
            // Topmost = requestTopmost;
            
            if (OperatingSystem.IsMacOS())
            {
                if (requestTopmost)
                {
                    MacWindowLevel.RaiseAboveDock(this);
                }
                else
                {
                    MacWindowLevel.RestoreToNormal(this);
                }
            }
        }

        private void Close_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void PreviousButton_OnClick(object? sender, RoutedEventArgs e)
        {
            MessageBus.Current.SendMessage(new ActionMessage()
                { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
        }

        private void NextButton_OnClick(object? sender, RoutedEventArgs e)
        {
            MessageBus.Current.SendMessage(new ActionMessage()
                { Action = ActionMessage.NavigateSlideAction.NextSlide });
        }

        private void FullscreenToggleButton_OnClick(object? sender, RoutedEventArgs e)
        {
            onToggleFullscreen();
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region "disable topmost when contextmenu is open, because macOS fullscreen hack will otherwise hide it"
        private void ContextMenu_OnOpening(object? sender, CancelEventArgs e)
        {
            SetTopmost(false);
        }

        private void ContextMenu_OnClosing(object? sender, CancelEventArgs e)
        {
            SetTopmost(IsFullScreen);
        }
        #endregion
    }
}