using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Extensions;
using ReactiveUI;
using Serilog;
using System;
using System.Diagnostics;
using System.Reactive;
using HandsLiftedApp.Core.Models.RuntimeData.Items;

namespace HandsLiftedApp.Controls
{
    public partial class SongArrangementControl : UserControl
    {
        public ReactiveCommand<Unit, Unit> EditCommand { get; }

        public SongArrangementControl()
        {
            InitializeComponent();

            EditCommand = ReactiveCommand.Create(RunTheThing);

            var flyout = this.Resources["MySharedFlyout"] as Flyout;
            if (flyout != null)
            {
                flyout.Opened += (s, e) =>
                {
                    if (flyout.Target is Control target)
                    {
                        target.Classes.Add("flyout-open");
                    }
                };
                flyout.Closed += (s, e) =>
                {
                    if (flyout.Target is Control target)
                    {
                        target.Classes.Remove("flyout-open");
                    }
                };
            }
        }
        void RunTheThing()
        {
            Debug.Print("a");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        private void ResetArrangementButtonClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is SongItemInstance songItem)
            {
                songItem.ResetArrangement();
            }
        }

        public void OnAddPartClick(object? sender, RoutedEventArgs args)
        {
            var button = (Control)sender;

            ItemsControl? arrangement = this.FindControl<ItemsControl>("PART_ArrangementTokens");
            Popup popup = button.FindAncestor<Popup>();

            // Find the index of the item that opened the flyout
            int idx = 0;
            bool found = false;
            ContentPresenter contentPresenter = popup?.FindAncestor<ContentPresenter>();

            if (contentPresenter != null)
            {
                while (idx < arrangement.Items.Count)
                {
                    Control? control = arrangement.ContainerFromIndex(idx);
                    if (control == contentPresenter)
                    {
                        found = true;
                        break;
                    }
                    idx++;
                }
            }
            else
            {
                // If no ContentPresenter is found, it's likely the global add button at the end
                idx = arrangement.Items.Count;
            }

            // Check if the flyout was opened from an "After" button
            var placementTarget = popup?.PlacementTarget as Control;
            bool isAfter = placementTarget?.Tag?.ToString() == "After";

            if (found && isAfter)
            {
                idx++;
            }

            var stanza = (SongStanza)((Control)sender).DataContext;

            //Debug.WriteLine($"Inserting into position {idx}");
            try
            {
                ((SongItem)this.DataContext).Arrangement.Insert(idx, stanza.Id);
            }
            catch (Exception ex)
            {
                Log.Error("Insert error", ex);
            }

        }
        public void OnFillerButtonClick(object? sender, RoutedEventArgs args)
        {
            //Button button = this.FindControl<Button>("AddPartFlyoutToggleButton");
            //if (button.Flyout.IsOpen)
            //{
            //    button.Flyout.Hide();
            //}
            //else
            //{
            //    button.Flyout.ShowAt(button);
            //}
        }

        //insert clone
        public void OnRepeatPartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (ArrangementRef)((Control)sender).DataContext;
            ((SongItem)this.DataContext).Arrangement.Insert(clickedStanza.Index + 1, clickedStanza.SongStanza.Id);
        }

        public void OnRemovePartClick(object? sender, RoutedEventArgs args)
        {
            var clickedStanza = (ArrangementRef)((Control)sender).DataContext;
            ((SongItem)this.DataContext).Arrangement.RemoveAt(clickedStanza.Index);
        }
    }
}
