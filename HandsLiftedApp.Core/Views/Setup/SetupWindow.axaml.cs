using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using HandsLiftedApp.Controls;
using HandsLiftedApp.Core.Models.UI;
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
            InitializeComponent();
            this.DataContext = _setupWindowViewModel = new SetupWindowViewModel(this.Screens);
            // this.DataContext = _setupWindowViewModel;
            this.Closed += PreferencesWindow_Closed;
            
            Win10DropshadowWorkaround.Register(this);
            
            var themeVariants = this.Get<ComboBox>("ThemeVariants");
            themeVariants.SelectedItem = Application.Current!.RequestedThemeVariant;
            themeVariants.SelectionChanged += (sender, e) =>
            {
                if (themeVariants.SelectedItem is ThemeVariant themeVariant)
                {
                    Application.Current!.RequestedThemeVariant = themeVariant;
                }
            };
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

        private void ProjectorOutput_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count > 0)
                MessageBus.Current.SendMessage(new OutputDisplayConfigurationChangeMessage() { ChangedDisplay = OutputDisplayConfigurationChangeMessage.Display.Projector });
        }
        
        private void StageOutput_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count > 0)
                MessageBus.Current.SendMessage(new OutputDisplayConfigurationChangeMessage() { ChangedDisplay = OutputDisplayConfigurationChangeMessage.Display.StageDisplay });
        }
    }
}