﻿using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.ItemState;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using HandsLiftedApp.Utils;

namespace HandsLiftedApp.ViewModels
{
    public class GroupItemsEditorViewModel : ReactiveObject
    {
        public GroupItemsEditorViewModel()
        {
            OpenFile = ReactiveCommand.CreateFromTask(OpenFileAsync);
            MoveItemUp = ReactiveCommand.CreateFromTask(MoveItemUpAsync);
            //this.WhenAnyValue(x => x.SelectedIndex, x => x.Item.Items, (selectedIndex, items) => canMove(selectedIndex, selectedIndex - 1, items.Count)));
            MoveItemDown = ReactiveCommand.CreateFromTask(MoveItemDownAsync);
            //this.WhenAnyValue(x => x.SelectedIndex, x => x.Item.Items, (selectedIndex, items) => canMove(selectedIndex, selectedIndex + 1, items.Count)));
            RemoveItem = ReactiveCommand.CreateFromTask(RemoveItemAsync);
            ExploreFile = ReactiveCommand.CreateFromTask(ExploreFileAsync);

            // The ShowOpenFileDialog interaction requests the UI to show the file open dialog.
            ShowOpenFileDialog = new Interaction<Unit, string?>();
        }

        // Properties
        public SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> Item { get; set; }
        private int _selectedIndex = -1;
        public int SelectedIndex { get => _selectedIndex; set => this.RaiseAndSetIfChanged(ref _selectedIndex, value); }

        public ReactiveCommand<Unit, Unit> OpenFile { get; }
        public ReactiveCommand<Unit, Unit> MoveItemUp { get; }
        public ReactiveCommand<Unit, Unit> MoveItemDown { get; }
        public ReactiveCommand<Unit, Unit> RemoveItem { get; }
        public ReactiveCommand<Unit, Unit> ExploreFile { get; }
        public Interaction<Unit, string?> ShowOpenFileDialog { get; }

        private bool canMove(int sourceIndex, int nextIndex, int count)
            => (sourceIndex > -1 && nextIndex > -1 && nextIndex < count);

        private async Task OpenFileAsync()
        {
            try
            {
                var fileName = await ShowOpenFileDialog.Handle(Unit.Default);

                if (fileName is object)
                {
                    // Put your logic for opening file here.
                    //if (SUPPORTED_VIDEO.Contains(extNoDot) || SUPPORTED_IMAGE.Contains(extNoDot))
                    //{
                    //    SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl> slidesGroupItem = createMediaGroupItem(fileName);

                    //    if (slidesGroupItem != null)
                    //        addedItems.Add(slidesGroupItem);
                    //}
                    Item.Items.Add(fileName);
                }
            }
            catch (System.Exception e)
            {
                System.Diagnostics.Debug.Print(e.Message);
                throw;
            }
        }

        private async Task MoveItemUpAsync()
        {
            MoveItem(SelectedIndex, SelectedIndex - 1);
        }
        
        private bool CanMoveItemUp()
        {
            return true;
        }

        private async Task MoveItemDownAsync()
        {
            MoveItem(SelectedIndex, SelectedIndex + 1);
        }

        private async Task RemoveItemAsync()
        {
            if (SelectedIndex > -1)
                Item.Items.RemoveAt(SelectedIndex);
        }
       private async Task ExploreFileAsync()
        {
            if (SelectedIndex > -1)
                FileUtils.ExploreFile(Item.Items[SelectedIndex]);
        }

        private void MoveItem(int sourceIndex, int nextIndex)
        {
            if (sourceIndex > -1 && nextIndex > -1 && nextIndex < Item.Items.Count)
            {
                Item.Items.Move(sourceIndex, nextIndex);
                SelectedIndex = nextIndex;
            }
        }
    }
}