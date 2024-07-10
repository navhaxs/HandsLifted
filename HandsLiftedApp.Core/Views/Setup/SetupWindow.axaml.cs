using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Extensions;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Setup
{
    public partial class SetupWindow : Window
    {
        SetupWindowViewModel _setupWindowViewModel;
 
        public SetupWindow()
        {
            this.DataContext = _setupWindowViewModel = new SetupWindowViewModel(this.Screens);
            InitializeComponent();
            this.DataContext = _setupWindowViewModel;
            this.Closed += PreferencesWindow_Closed;
        }


        private void PreferencesWindow_Closed(object? sender, EventArgs e)
        {_setupWindowViewModel.HideDisplayItentification();
        }


        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (IdentifyToggleButton.IsChecked == true)
            {
                _setupWindowViewModel.ShowDisplayIdentification(Screens);
            }
            else
            {
                _setupWindowViewModel.HideDisplayItentification();
            }
        }

        private void EditLibraryButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Process.Start("notepad.exe", Constants.LIBRARY_CONFIG_FILEPATH);
        }

        private void ReloadLibraryButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Globals.Instance.MainViewModel.LibraryViewModel.ReloadLibraries();
        }

        private void DoneButton_OnClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}