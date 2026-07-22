using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace HandsLiftedApp.Core.Views.Confirmation
{
    public partial class RenameDialog : Window
    {
        public string? ResultName { get; private set; }

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameBox.Text = currentName;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            NameBox.Focus();
            NameBox.SelectAll();
        }

        private void OnConfirmRename(object? sender, RoutedEventArgs e) => Confirm();

        private void NameBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Confirm();
        }

        private void Confirm()
        {
            var name = NameBox.Text?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                ResultName = name;
                Close();
            }
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close();
    }
}
