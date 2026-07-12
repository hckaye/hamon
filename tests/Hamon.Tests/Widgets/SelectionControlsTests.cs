using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for Radio / SegmentedControl (onChanged on single selection/tap/OK).</summary>
public class SelectionControlsTests
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
    public void Radio_Tap_FiresOnChangedWithValue()
    {
        int? chosen = null;
        HamonRoot host = Mount(new Radio<int> { Value = 2, GroupValue = 1, OnChanged = v => chosen = v });

        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Up, 0.01f));

        Assert.Equal(2, chosen);
    }

    [Fact]
    public void Radio_Gamepad_Activate_FiresOnChanged()
    {
        int? chosen = null;
        HamonRoot host = Mount(new Radio<int> { Value = 5, GroupValue = 0, Autofocus = true, OnChanged = v => chosen = v });

        host.HandleButtonDown(GamepadButton.A);
        Assert.Equal(5, chosen);
    }

    [Fact]
    public void Radio_Hover_ShowsClickCursor()
    {
        HamonRoot host = Mount(new Radio<int> { Value = 1, GroupValue = 1, OnChanged = _ => { } });
        host.DispatchHover(new Vec2(10, 10));
        Assert.Equal(MouseCursor.Click, host.CurrentCursor);
    }

    [Fact]
    public void SegmentedControl_TapSegment_FiresOnChanged()
    {
        string? chosen = null;
        HamonRoot host = Mount(new SegmentedControl<string>
        {
            Value = "a",
            OnChanged = v => chosen = v,
            Segments = new[]
            {
                new SegmentItem<string>("a", new SizedBox { Width = Dimension.Px(60), Height = Dimension.Px(30) }),
                new SegmentItem<string>("b", new SizedBox { Width = Dimension.Px(60), Height = Dimension.Px(30) }),
            },
        });

        // 2 番目のセグメント（"b"）あたりをタップ。先頭は ~2..~90px、2 番目はその右。
        host.DispatchPointer(new PointerEvent(new Vec2(130, 100), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(130, 100), PointerPhase.Up, 0.01f));

        Assert.Equal("b", chosen);
    }
}
