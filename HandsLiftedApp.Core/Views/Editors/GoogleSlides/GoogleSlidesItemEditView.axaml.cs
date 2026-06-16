using Avalonia.Controls;
using Avalonia.Interactivity;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Utils;

namespace HandsLiftedApp.Core.Views.Editors.GoogleSlides
{
    public partial class GoogleSlidesItemEditView : UserControl
    {
        public GoogleSlidesItemEditView()
        {
            InitializeComponent();
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is GoogleSlidesGroupItemInstance googleSlidesGroupItemInstance)
            {
                var url =
                    $"https://docs.google.com/presentation/d/{googleSlidesGroupItemInstance.SourceGooglePresentationId}/edit";
                OpenUrlLink.OpenUrl(url);
            }
        }

        private void SyncNow_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is IItemSyncable instance)
                instance.Sync();
        }

        private void Done_OnClick(object? sender, RoutedEventArgs e)
        {
            (TopLevel.GetTopLevel(this) as Window)?.Close();
        }
    }
}