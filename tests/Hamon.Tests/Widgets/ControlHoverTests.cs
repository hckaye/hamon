using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic testing of Checkbox/Switch/Slider/Tabs hover state (cursor presentation, deactivation, tab hover index).</summary>
public class ControlHoverTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 200);

    private static HamonRoot Mount(Widget root)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => root);
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Checkbox_Hover_ShowsClickCursor_DoesNotToggle()
    {
        int changes = 0;
        HamonRoot host = Mount(new Checkbox { OnChanged = _ => changes++ });

        host.DispatchHover(new Vec2(10, 10));
        Assert.Equal(MouseCursor.Click, host.CurrentCursor);
        Assert.Equal(0, changes); // hover ではトグルしない
    }

    [Fact]
    public void Switch_Hover_ShowsClickCursor()
    {
        HamonRoot host = Mount(new Switch { OnChanged = _ => { } });
        host.DispatchHover(new Vec2(10, 10));
        Assert.Equal(MouseCursor.Click, host.CurrentCursor);
    }

    [Fact]
    public void Slider_Hover_ShowsClickCursor()
    {
        HamonRoot host = Mount(new Slider { Value = 0.5f, OnChanged = _ => { } });
        host.DispatchHover(new Vec2(50, 14));
        Assert.Equal(MouseCursor.Click, host.CurrentCursor);
    }

    [Fact]
    public void Tabs_Hover_SetsHoveredIndex_AndClearsOnExit()
    {
        TabController? controller = null;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() =>
        {
            controller ??= new TabController(2);
            return new Tabs
            {
                Controller = controller,
                Items = new[]
                {
                    new TabItem(new Text("A") { FontSize = 14 }, () => new SizedBox()),
                    new TabItem(new Text("B") { FontSize = 14 }, () => new SizedBox()),
                },
            };
        });
        host.Update(Viewport);

        // 先頭タブ（左上）へ hover
        host.DispatchHover(new Vec2(10, 10));
        host.Update(Viewport);
        Assert.Equal(0, controller!.HoveredIndex);
        Assert.Equal(MouseCursor.Click, host.CurrentCursor);

        // 領域外へ
        host.DispatchHover(new Vec2(290, 190));
        host.Update(Viewport);
        Assert.Equal(-1, controller!.HoveredIndex);
    }
}
