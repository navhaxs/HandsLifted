using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HandsLiftedApp.Views.Render
{
    public partial class SlideRenderer : UserControl
    {
        public SlideRenderer()
        {
            InitializeComponent();

            if (IsLive == false)
            {
                TransitioningContentControl root = this.FindControl<TransitioningContentControl>("root");
                root.PageTransition = null;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public static readonly DirectProperty<SlideRenderer, bool> IsLiveProperty =
            AvaloniaProperty.RegisterDirect<SlideRenderer, bool>(
                nameof(IsLive),
                o => o.IsLive,
                (o, v) => o.IsLive = v);

        private bool _items = true;

        public bool IsLive
        {
            get { return _items; }
            set {
                SetAndRaise(IsLiveProperty, ref _items, value);

                if (value == false)
                {
                    TransitioningContentControl root = this.FindControl<TransitioningContentControl>("root");
                    if (root != null)
                    {
                        root.PageTransition = null;
                    }
                }
            }
        }
    }
}
