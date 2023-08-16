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

            DateField.SelectedDateChanged += DateField_SelectedDateChanged;
            calendar.SelectedDatesChanged += Calendar_SelectedDatesChanged;
        }

        private void DateField_SelectedDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
        {
            calendar.SelectedDate = DateField.SelectedDate.Value.DateTime;
            calendar.DisplayDate = DateField.SelectedDate.Value.DateTime;
        }

        private void Calendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            DateField.SelectedDate = calendar.SelectedDate;
        }
    }
}
