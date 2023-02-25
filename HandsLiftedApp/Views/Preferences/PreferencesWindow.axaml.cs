using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.ViewModels;
using HandsLiftedApp.Views.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using static HandsLiftedApp.ViewModels.PreferencesViewModel;

namespace HandsLiftedApp.Views.Preferences
{
    public partial class PreferencesWindow : Window
    {

        List<PreferencesViewModel.DisplayModel> AllAvailableScreens { get; set; }
        List<string> Items { get; set; } = new List<string>() { "a", "b", "c" };
        PreferencesViewModel preferencesViewModel { get; }

        //List<DisplayIdentifyWindow2> wnds = new List<DisplayIdentifyWindow2>();

        public PreferencesWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            if (Design.IsDesignMode)
                return;

            SlideDesignerWindow slideDesignerWindow = new SlideDesignerWindow();
            slideDesignerWindow.Show();

            preferencesViewModel = Globals.Preferences;
            AllAvailableScreens = this.Screens.All.Select(i => new DisplayModel(i.Bounds)).ToList();
            AllAvailableScreens.Add(new UnsetDisplay());
            foreach (var (i, index) in AllAvailableScreens.WithIndex())
            {
                i.Label = $"Display {index + 1}";
            };

            this.DataContext = this;

            //OutputDisplayComboBox.Items = this.Screens.All.Select(i => new DisplayModel(i.Bounds));
            //StageDisplayComboBox.Items = this.Screens.All.Select(i => new DisplayModel(i.Bounds));
            //MyComboBox.Items = new List<string>() { "a", "b", "c" };

            //foreach (var (i, index) in this.Screens.All.WithIndex())
            //{
            //    DisplayIdentifyWindow2 displayIdentifyWindow = new DisplayIdentifyWindow2();
            //    displayIdentifyWindow.Show();
            //    displayIdentifyWindow.Position = new PixelPoint(i.Bounds.X, i.Bounds.Y);
            //    displayIdentifyWindow.Topmost = true;
            //    displayIdentifyWindow.WindowState = WindowState.FullScreen;
            //    displayIdentifyWindow.ScreenBounds.Text = $"{i.Bounds.Width} x {i.Bounds.Height}";
            //    displayIdentifyWindow.ScreenLocation.Text = $"({i.Bounds.X}, {i.Bounds.Y})";
            //    displayIdentifyWindow.ScreenNumber.Text = " ";
            //    displayIdentifyWindow.ScreenNumber.Text = $"{index + 1}";
            //    //Dispatcher.UIThread.InvokeAsync(() =>
            //    //{
            //    //    displayIdentifyWindow.WindowState = WindowState.FullScreen;
            //    //});
            //    //DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            //    //timer.Tick += (object? sender, EventArgs e) =>
            //    //{
            //    //    displayIdentifyWindow.Close();
            //    //};
            //    //timer.Start();
            //    wnds.Add(displayIdentifyWindow);
            //}

            this.Closed += PreferencesWindow_Closed;
        }

        public class DisplayInfoItem
        {
            string Label;
            PixelRect Bounds;
        }

        private void PreferencesWindow_Closed(object? sender, EventArgs e)
        {
            //wnds.ForEach(wnd =>
            //{
            //    if (wnd != null && wnd.IsVisible)
            //    {
            //        wnd.Close();
            //    }
            //});

        }
    }
}
