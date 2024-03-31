using System;
using System.Collections.Generic;
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
    }
}