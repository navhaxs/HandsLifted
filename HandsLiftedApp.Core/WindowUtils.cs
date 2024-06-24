using System;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.ViewModels;
using ReactiveUI;

namespace HandsLiftedApp.Core
{
    public class WindowUtils
    {
        public static void RestoreWindowBounds(Window targetWindow,
            AppPreferencesViewModel.DisplayModel? targetSavedDisplay)
        {
            if (targetSavedDisplay != null &&
                Globals.Instance.AppPreferences.OutputDisplayBounds is not AppPreferencesViewModel.UnsetDisplay)
            {
                targetWindow.Position = new PixelPoint(targetSavedDisplay.X, Globals.Instance.AppPreferences
                    .OutputDisplayBounds.Y);
                targetWindow.WindowState = WindowState.FullScreen;
                targetWindow.Height = Globals.Instance.AppPreferences.OutputDisplayBounds.Height;
                targetWindow.Width = Globals.Instance.AppPreferences.OutputDisplayBounds.Width;
            }
        }
        public static void RegisterWindowWatcher(Window targetWindow)
        {
        // maintain fullscreen state when user moves this window across monitors (e.g. by Win+Shift+Left/RightArrow)
            targetWindow.PositionChanged += (sender, args) =>
            {
                // TODO dont use flag... actually check for PixelPointEventArgs and see if monitor has changed
                if (targetWindow.WindowState == WindowState.Maximized && args.Point.X == 0)
                {
                    targetWindow.WindowState = WindowState.Normal;
                    targetWindow.WindowState = WindowState.Maximized;
                }
            };
        }
    }
}