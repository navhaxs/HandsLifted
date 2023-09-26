using Avalonia.Controls;
using HandsLiftedApp.ViewModels;
using System;

namespace HandsLiftedApp.Views.App
{
    public partial class EditPlaylistTitleControl : UserControl
    {
        public EditPlaylistTitleControl()
        {
            InitializeComponent();


            DoneButton.Click += (object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
            {
                MainWindowViewModel vm = this.DataContext as MainWindowViewModel;
                vm.Playlist.Title = TitleField.Text;

                if (DateField.SelectedDate != null)
                    vm.Playlist.Date = (DateTimeOffset)DateField.SelectedDate;

                //Close(); // goto root flyout and dismiss
            };
            //CancelButton.Click += (s, e) => Close();
        }
    }
}
