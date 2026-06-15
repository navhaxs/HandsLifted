using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Models.PlaylistActions;
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

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            bool hasMainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.Windows.OfType<MainWindow>().Any() ?? false;
            AddToPlaylistButton.IsVisible = hasMainWindow;
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

            var playlist = Globals.Instance.MainViewModel.Playlist;
            var instance = ItemInstanceFactory.ToItemInstance(songItem, playlist) as SongItemInstance;
            if (instance == null) return;

            var editor = new SongEditorWindow { DataContext = new SongEditorViewModel(instance, playlist) };
            editor.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var parent = TopLevel.GetTopLevel(this) as Window;
            if (parent != null)
                editor.Show(parent);
            else
                editor.Show();
        }
    }
}