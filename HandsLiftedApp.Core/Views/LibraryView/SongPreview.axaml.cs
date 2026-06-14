using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Models.PlaylistActions;
using HandsLiftedApp.Models.UI;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.LibraryView
{
    public partial class SongPreview : UserControl
    {
        public static readonly StyledProperty<LibraryItem?> LibraryItemProperty =
            AvaloniaProperty.Register<SongPreview, LibraryItem?>(nameof(LibraryItem));

        public LibraryItem? LibraryItem
        {
            get => GetValue(LibraryItemProperty);
            set => SetValue(LibraryItemProperty, value);
        }

        public static readonly StyledProperty<SongItem?> SongItemProperty =
            AvaloniaProperty.Register<SongPreview, SongItem?>(nameof(SongItem));

        public SongItem? SongItem
        {
            get => GetValue(SongItemProperty);
            set => SetValue(SongItemProperty, value);
        }

        public SongPreview()
        {
            InitializeComponent();
        }

        private void AddToPlaylistButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (LibraryItem == null) return;
            MessageBus.Current.SendMessage(new AddItemByFilePathMessage(
                new List<string> { LibraryItem.FullFilePath }, null)); // null = append to end
        }

        private void EditSongButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (SongItem is not { } songItem) return;

            var instance = ItemInstanceFactory.ToItemInstance(songItem, Globals.Instance.MainViewModel.Playlist) as SongItemInstance;
            if (instance == null) return;

            MessageBus.Current.SendMessage(new MainWindowModalMessage(
                new SongEditorWindow(), false,
                new SongEditorViewModel(instance, Globals.Instance.MainViewModel.Playlist)));
        }
    }
}