using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using HandsLiftedApp.Views.Render;

namespace HandsLiftedApp.Views.Prepare {
    public partial class PrepareItem : UserControl {
        public PrepareItem() {
            InitializeComponent();
        }

        private string _itemTitle = "Untitled Item";

        public string ItemTitle {
            get => _itemTitle;
            set => SetAndRaise(ItemTitleProperty, ref _itemTitle, value);
        }

        public static readonly AttachedProperty<string> ItemTitleProperty =
       AvaloniaProperty.RegisterAttached<PrepareItem, string>(nameof(ItemTitle), typeof(string));
    }
}
