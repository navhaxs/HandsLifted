using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerspectiveDemo;

namespace HandsLiftedApp.Tests.Views;

[TestClass]
public class SelectedAdornerTests
{
    private static readonly string[] Parts =
    {
        "PART_Drag", "PART_Top", "PART_Bottom", "PART_Left", "PART_Right",
        "PART_TopLeft", "PART_TopRight", "PART_BottomLeft", "PART_BottomRight",
    };

    // An adorner with the template's parts, following a proxy border bound two-way to the element exactly as the
    // designer's proxies are (int X/Y/Width/Height on the element, double Canvas.Left/Top on the proxy). There is no
    // window here, so no adorner layer: the template and the adorned canvas are supplied by hand.
    private static (SelectedAdorner Adorner, TextElement Element) Create(int x, int y)
    {
        AvaloniaTextRendering.EnsureInitialized();
        var element = new TextElement { X = x, Y = y, Width = 100, Height = 100 };
        var proxy = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        proxy.Bind(Canvas.LeftProperty, new Binding { Source = element, Path = nameof(SlideElement.X), Mode = BindingMode.TwoWay });
        proxy.Bind(Canvas.TopProperty, new Binding { Source = element, Path = nameof(SlideElement.Y), Mode = BindingMode.TwoWay });
        proxy.Bind(Layoutable.WidthProperty, new Binding { Source = element, Path = nameof(SlideElement.Width), Mode = BindingMode.TwoWay });
        proxy.Bind(Layoutable.HeightProperty, new Binding { Source = element, Path = nameof(SlideElement.Height), Mode = BindingMode.TwoWay });

        var host = new Canvas();
        host.Children.Add(proxy);
        var adorner = new SelectedAdorner
        {
            Control = proxy,
            SlideSize = new Size(1920, 1080),
            Template = new FuncControlTemplate<SelectedAdorner>((_, scope) =>
            {
                var canvas = new Canvas();
                scope.Register("PART_Canvas", canvas);
                foreach (var name in Parts)
                {
                    var thumb = new Thumb();
                    scope.Register(name, thumb);
                    canvas.Children.Add(thumb);
                }
                return canvas;
            }),
        };
        adorner[Avalonia.Controls.Primitives.AdornerLayer.AdornedElementProperty] = host;
        adorner.ApplyTemplate();

        // the proxy has no layout in this host, so the handles start empty: report the element's rect to them
        element.X = x + 1;
        element.X = x;
        element.Y = y + 1;
        element.Y = y;
        return (adorner, element);
    }

    [TestMethod]
    public Task SlowDragAwayFromSnap_LeavesTheSnapPoint_DespiteRoundingBinding() => DispatcherTestThread.Run(() =>
    {
        var (adorner, element) = Create(40, 40);

        // drag diagonally until the box snaps to the slide's top-left corner (raw position is 4.7, 4.7: inside the
        // 10 px range, so the box is drawn at 0)
        adorner.DragBy(-35.3, -35.3);
        Assert.AreEqual(0, element.X, "snapped to the slide's left edge");
        Assert.AreEqual(0, element.Y, "snapped to the slide's top edge");

        // one more small move out of range: the raw position (10.9) is what counts, not the snapped 0 + 6.2
        adorner.DragBy(6.2, 6.2);
        Assert.IsTrue(element.X >= 10, $"snap session was lost: X = {element.X}");
        Assert.IsTrue(element.Y >= 10, $"snap session was lost: Y = {element.Y}");

        // then away again in tiny fractional steps (the zoomed pointer deltas): the raw position leaves the 10 px
        // snap range after 21 steps, so by 40 steps the box must be well clear of the edge
        for (var i = 0; i < 40; i++) adorner.DragBy(0.5, 0.5);

        Assert.IsTrue(element.X >= 15, $"X still stuck at the snap point: {element.X}");
        Assert.IsTrue(element.Y >= 15, $"Y still stuck at the snap point: {element.Y}");
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ExternalMoveOfElement_RebasesTheHandles() => DispatcherTestThread.Run(() =>
    {
        var (adorner, element) = Create(40, 40);
        adorner.DragBy(10, 10);

        element.X = 500; // e.g. undo or a typed value: the handles follow and the next drag starts from there
        element.Y = 300;
        adorner.DragBy(5, 5);

        Assert.AreEqual(505, element.X);
        Assert.AreEqual(305, element.Y);
        return Task.CompletedTask;
    });
}
