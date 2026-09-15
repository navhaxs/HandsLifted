using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace HandsLiftedApp.Core.Views.ItemEditDock
{
    public partial class ItemAutoplayIndicator : UserControl
    {
        public ItemAutoplayIndicator()
        {
            InitializeComponent();
        }

        private void Indicator_OnClick(object? sender, RoutedEventArgs e)
        {
            // The Edit button is our sibling in the DataTemplate's StackPanel (see
            // ItemEditDockRoot.axaml); the Autoplay Timer row lives inside its Flyout.
            if (this.Parent is not Panel siblingPanel) return;

            var editButton = siblingPanel.Children.OfType<Button>().FirstOrDefault();
            if (editButton?.Flyout is not Flyout editFlyout) return;

            var timerRow = (editFlyout.Content as ILogical)?
                .GetLogicalDescendants()
                .OfType<ItemTimer>()
                .FirstOrDefault();

            if (timerRow == null)
            {
                editFlyout.ShowAt(editButton);
                return;
            }

            // The timer row's own Flyout target isn't attached to the visual tree until
            // the Edit flyout has actually opened, so defer opening it until then.
            void OnEditFlyoutOpened(object? s, EventArgs args)
            {
                editFlyout.Opened -= OnEditFlyoutOpened;
                timerRow.OpenTimerFlyout();
            }

            editFlyout.Opened += OnEditFlyoutOpened;
            editFlyout.ShowAt(editButton);
        }
    }
}
