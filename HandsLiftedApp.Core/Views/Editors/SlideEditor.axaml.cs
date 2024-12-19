using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.ViewModels.SlideElementEditor;
using HandsLiftedApp.Data.Data.Models.Slides;
using PerspectiveDemo;

namespace HandsLiftedApp.Core.Views.Editors
{
    public partial class SlideEditor : UserControl
    {
        public SlideEditor()
        {
            InitializeComponent();
            
            var canvas = this.FindControl<Canvas>("Canvas");
            var rectangle = this.FindControl<TextBox>("Rectangle");

            PointerPressed += (_, _) =>
            {
                if (_selected is null)
                {
                    //AddSelected(rectangle, canvas);
                }
            };

            TestWindow1 tw1 = new TestWindow1();
            tw1.Show();
        }

        private SelectedAdorner? _selected;
 
        private void AddSelected(Control control, Canvas canvas)
        {
            var layer = AdornerLayer.GetAdornerLayer(canvas);
            if (layer is null)
            {
                return;
            }

            _selected = new SelectedAdorner
            {
                [AdornerLayer.AdornedElementProperty] = canvas,
                IsHitTestVisible = true,
                ClipToBounds = false,
                Control = control
            };

            ((ISetLogicalParent) _selected).SetParent(canvas);
            layer.Children.Add(_selected);
        }
        
        private void AButton_OnClick(object? sender, RoutedEventArgs e)
        {
            // ZoomBorder.AutoFit();
            ZoomBorder.ResetMatrix();
        }
        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SlideEditorViewModel vm)
            {
                vm.Slides.Add(new CustomSlide());
            }
        }

        private void MenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SlideEditorViewModel vm)
            {
                if (sender is Control control)
                {
                    if (control.DataContext is CustomSlide targetSlide)
                    {
                        vm.Slides.Remove(targetSlide);
                    }
                }
            }
        }
    }
}