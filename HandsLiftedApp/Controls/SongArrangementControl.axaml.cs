using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.SlideState;
using ReactiveUI;
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
            var m = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza>() { Value = stanza };

            //var g = ((Control)sender).Visua

            //ItemsControl itemsControl = ((Control)sender).FindAncestor<ItemsControl>();
            //Control? parent = (Control)button.Parent;
            //int idx = 0;
            //foreach (var item in parent.GetLogicalChildren())
            //{
            //    if (item == parent)
            //        break;
            //    idx++;
            //}

            //int idx = itemsControl.IndexFromContainer();

            //int idx = 0;
            //foreach (var item in itemsControl.GetRealizedContainers()
            //{

            //    //System.Collections.Generic.IEnumerable<ILogical> logicalChildren = control.GetLogicalChildren();
            //    //System.Collections.Generic.IEnumerable<Visual> visualChildren = control.GetVisualChildren();

            //    //if (visualChildren.Contains(sender) || logicalChildren.Contains(sender))
            //    //{
            //    //    break;
            //    //}
            //    if (item.GetLogicalChildren().Contains(sender))
            //    {
            //        break;
            //    }
            //    idx++;
            //}

            //Debug.WriteLine($"Inserting into position {idx}");
            ((SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext).Arrangement.Insert(idx + 1, m);

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
            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza> stanza = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza>)((Control)sender).DataContext;

            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza> clonedStanza = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza> { Value = stanza.Value };

            var lastIndex = ((SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext).Arrangement.IndexOf(stanza);

            ((SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext).Arrangement.Insert(lastIndex + 1, clonedStanza);
        }

        public void OnRemovePartClick(object? sender, RoutedEventArgs args)
        {
            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza> stanza = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>.Ref<SongStanza>)((Control)sender).DataContext;

            ((SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext).Arrangement.Remove(stanza);
        }

        public void a()
        {
            using (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> songItem = (SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>)this.DataContext)
            {
                songItem.ResetArrangement();
            }
        }

    }
}
