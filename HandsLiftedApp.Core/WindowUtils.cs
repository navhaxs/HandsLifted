using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.ViewModels;

namespace HandsLiftedApp.Core
{
    public class WindowUtils
    {
        public static void ShowAndRestoreWindowBounds(Window targetWindow,
            AppPreferencesViewModel.DisplayModel? targetSavedDisplay,
            bool? forceShow = null)
        {

            if (targetSavedDisplay != null) {

                var targetPosition = new PixelPoint(targetSavedDisplay.X, targetSavedDisplay.Y);

                if (!IsDisplayConnected(targetWindow, targetSavedDisplay))
                {
                    if (forceShow != true)
                        return;
                }

                // force re-toggle
                if (targetWindow.WindowState == WindowState.FullScreen)
                {
                    targetWindow.WindowState = WindowState.Normal;
                    targetWindow.Height = 0;
                    targetWindow.Width = 0;
                }

                targetWindow.Position = targetPosition;
                targetWindow.Height = Math.Max(100, targetSavedDisplay.Height);
                targetWindow.Width = Math.Max(100, targetSavedDisplay.Width);

                if (!OperatingSystem.IsMacOS())
                    targetWindow.WindowState = WindowState.FullScreen;
            }
            
            targetWindow.Show();

        }

        public static bool IsDisplayConnected(Window window, AppPreferencesViewModel.DisplayModel targetSavedDisplay)
        {
            var targetPosition = new PixelPoint(targetSavedDisplay.X, targetSavedDisplay.Y);
            return window.Screens.All.Any(target => target.Bounds.Contains(targetPosition));
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