using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Models.Items;
using Serilog;

namespace HandsLiftedApp.Core.Views.ItemEditDock
{
    public partial class ItemEditMenuButton : UserControl
    {
        public static readonly StyledProperty<object?> ExtraRowsProperty =
            AvaloniaProperty.Register<ItemEditMenuButton, object?>(nameof(ExtraRows));

        /// <summary>Type-specific menu rows, shown between "Edit item" and "Fade transition".</summary>
        public object? ExtraRows
        {
            get => GetValue(ExtraRowsProperty);
            set => SetValue(ExtraRowsProperty, value);
        }

        public ItemEditMenuButton()
        {
            InitializeComponent();
        }

        /// <summary>The "Edit" button that owns the dropdown (used by <see cref="ItemAutoplayIndicator"/>).</summary>
        public Button MenuButton => EditButton;

        private async void EditButton_OnClick(object? sender, RoutedEventArgs e)
        {
            switch (DataContext)
            {
                case SongItemInstance item:
                {
                    SongEditorViewModel songEditorViewModel =
                        new SongEditorViewModel(item, Globals.Instance.MainViewModel.Playlist);
                    SongEditorWindow songEditorWindow = new SongEditorWindow() { DataContext = songEditorViewModel };
                    songEditorWindow.Show();
                    return;
                }
                case MediaGroupItemInstance mediaGroupItemInstance:
                {
                    GenericContentEditorWindow editorWindow =
                        new GenericContentEditorWindow() { DataContext = mediaGroupItemInstance };
                    editorWindow.Show();
                    return;
                }
                case PDFSlidesGroupItemInstance pdfSlidesGroupItemInstance:
                {
                    GenericContentEditorWindow editorWindow =
                        new GenericContentEditorWindow() { DataContext = pdfSlidesGroupItemInstance };
                    editorWindow.Show();
                    return;
                }
                case PowerPointPresentationItemInstance powerPointPresentationItemInstance:
                {
                    GenericContentEditorWindow editorWindow =
                        new GenericContentEditorWindow() { DataContext = powerPointPresentationItemInstance };
                    editorWindow.Show();
                    return;
                }
                case GoogleSlidesGroupItemInstance googleSlidesGroupItemInstance:
                {
                    GenericContentEditorWindow editorWindow =
                        new GenericContentEditorWindow() { DataContext = googleSlidesGroupItemInstance };
                    editorWindow.Show();
                    return;
                }
                case ScriptureItemInstance scripture:
                {
                    var parentWindow = TopLevel.GetTopLevel(this) as Window;
                    if (parentWindow == null) return;

                    var dialog = new ScriptureAddDialog(scripture.Book, scripture.StartChapter, scripture.StartVerse,
                        scripture.EndChapter, scripture.EndVerse, scripture.Translation);
                    await dialog.ShowDialog(parentWindow);
                    if (dialog.Result == null) return;

                    var result = dialog.Result.Value;
                    var newReference = new ScriptureReference(
                        result.BookCode, result.StartChapter, result.StartVerse, result.EndChapter, result.EndVerse,
                        result.Translation,
                        ScriptureTitleFormatter.Format(result.BookName, result.StartChapter, result.StartVerse,
                            result.EndChapter, result.EndVerse));

                    var playlist = scripture.ParentPlaylist ?? Globals.Instance.MainViewModel.Playlist;
                    playlist.UndoStack.Execute(new EditScriptureReferenceAction(playlist, scripture, newReference));
                    return;
                }
            }
        }

        private void FadeOverrideCheckBox_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox { DataContext: Item item } checkBox)
            {
                return;
            }

            if (checkBox.IsChecked == true)
            {
                if (item.SlideTransitionDurationMs is null)
                {
                    item.SlideTransitionDurationMs = Globals.Instance.MainViewModel.Playlist.SlideTransitionDurationMs;
                }
            }
            else
            {
                item.SlideTransitionDurationMs = null;
            }
        }
    }
}
