using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Views.Setup;
using HandsLiftedApp.Extensions;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels
{
    public class SetupWindowViewModel : ReactiveObject
    {
        private List<AppPreferencesViewModel.DisplayModel?> _AllAvailableScreens = new();
        public List<AppPreferencesViewModel.DisplayModel?> AllAvailableScreens { get => _AllAvailableScreens; set => this.RaiseAndSetIfChanged(ref _AllAvailableScreens, value); }
        List<DisplayIdentifyWindow> wnds = new();
        
        public SetupWindowViewModel(Screens screens)
        {
            AllAvailableScreens = screens.All.Select(i => new AppPreferencesViewModel.DisplayModel(i.Bounds)).ToList<AppPreferencesViewModel.DisplayModel?>();
            foreach (var (i, index) in AllAvailableScreens.WithIndex())
            {
                if (i != null) i.Label = $"Display {index + 1}";
            };
            AllAvailableScreens.Add(null);
        }

        public void ShowDisplayIdentification(Screens screens)
        {
            foreach (var (i, index) in screens.All.WithIndex())
            {
                DisplayIdentifyWindow displayIdentifyWindow = new DisplayIdentifyWindow() { Screen = i };
                displayIdentifyWindow.Show();
                displayIdentifyWindow.Position = new PixelPoint(i.Bounds.X, i.Bounds.Y);
                displayIdentifyWindow.Topmost = true;
                
                if (OperatingSystem.IsWindows())
                    displayIdentifyWindow.WindowState = WindowState.FullScreen;
                
                displayIdentifyWindow.ScreenBounds.Text = $"{i.Bounds.Width} x {i.Bounds.Height}";
                displayIdentifyWindow.ScreenLocation.Text = $"({i.Bounds.X}, {i.Bounds.Y})";
                displayIdentifyWindow.ScreenNumber.Text = " ";
                displayIdentifyWindow.ScreenNumber.Text = $"{index + 1}";
                //Dispatcher.UIThread.InvokeAsync(() =>
                //{
                //    displayIdentifyWindow.WindowState = WindowState.FullScreen;
                //});
                // DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                // timer.Tick += (object? sender, EventArgs e) =>
                // {
                //     displayIdentifyWindow.Close();
                // };
                // timer.Start();
                wnds.Add(displayIdentifyWindow);
            }
        }
        public void HideDisplayItentification()
        {
            wnds.ForEach(wnd =>
            {
                if (wnd != null && wnd.IsVisible)
                {
                    wnd.Close();
                }
            });
        }
    }
}