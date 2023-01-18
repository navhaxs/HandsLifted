using Avalonia.Controls;
using HandsLiftedApp.ViewModels;
using System;

namespace HandsLiftedApp.Views.Editor
{
    public partial class PlaylistInfoEditorWindow : Window
    {
        public PlaylistInfoEditorWindow()
        {
            InitializeComponent();

            DoneButton.Click += (object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
            {
                MainWindowViewModel vm = this.DataContext as MainWindowViewModel;
                vm.Playlist.Title = TitleField.Text;

                if (DateField.SelectedDate != null)
                    vm.Playlist.Date = (DateTimeOffset)DateField.SelectedDate;

                Close();
            };
            CancelButton.Click += (s, e) => Close();
        }
    }
}
