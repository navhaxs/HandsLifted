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

namespace HandsLiftedApp.Controls
{
    public partial class SongArrangementControl : UserControl
    {
        public ReactiveCommand<Unit, Unit> EditCommand { get; }

        public SongArrangementControl()
        {
            InitializeComponent();

            EditCommand = ReactiveCommand.Create(RunTheThing);
        }
        void RunTheThing()
        {
            Debug.Print("a");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        private void ArrangementButtonClick(object? sender, RoutedEventArgs e)
        {
            //(sender as Button)!.Content = "Ginger";
            a();
        }

        //public void btn_OnClick(object? sender, RoutedEventArgs args)
        //{
        //    (sender as Button)!.Content = "Ginger";
        //}
        public void OnAddPartClick(object? sender, RoutedEventArgs args)
        {
            var button = (Control)sender;


            ItemsControl? arrangement = this.FindControl<ItemsControl>("PART_ArrangementTokens");
            Popup popup = button.FindAncestor<Popup>();

            int idx = 0;
            while (idx < arrangement.Items.Count)
            {
                Control? control = arrangement.ContainerFromIndex(idx);
                ContentPresenter contentPresenter = popup.FindAncestor<ContentPresenter>();

                if (control == contentPresenter)
                {
                    break;
                }

                Debug.WriteLine(idx);
                idx++;
            }

            var stanza = (SongStanza)((Control)sender).DataContext;
            var m = new SongItem.Ref<SongStanza>() { Value = stanza };

            //Debug.WriteLine($"Inserting into position {idx}");
            try
            {
                ((SongItem)this.DataContext).Arrangement.Insert(idx, m);
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
            SongItem.Ref<SongStanza> stanza = (SongItem.Ref<SongStanza>)((Control)sender).DataContext;

            SongItem.Ref<SongStanza> clonedStanza = new SongItem.Ref<SongStanza> { Value = stanza.Value };

            var lastIndex = ((SongItem)this.DataContext).Arrangement.IndexOf(stanza);

            ((SongItem)this.DataContext).Arrangement.Insert(lastIndex + 1, clonedStanza);
        }

        public void OnRemovePartClick(object? sender, RoutedEventArgs args)
        {
            SongItem.Ref<SongStanza> stanza = (SongItem.Ref<SongStanza>)((Control)sender).DataContext;

            ((SongItem)this.DataContext).Arrangement.Remove(stanza);
        }

        public void a()
        {
            //using (SongItem songItem = (SongItem)this.DataContext)
            //{
            //    songItem.ResetArrangement();
            //}
        }

    }
}
