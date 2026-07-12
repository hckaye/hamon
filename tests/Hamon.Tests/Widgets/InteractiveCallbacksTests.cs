using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of Slider/TextField/VirtualJoystick interaction callbacks (hover/drag/focus/grab = origin of custom animation/sound effects).</summary>
public class InteractiveCallbacksTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    [Fact]
    public void Slider_OnStateChanged_FiresPressedOnDrag()
    {
        var states = new List<WidgetState>();
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Slider { Value = 0.5f, OnStateChanged = s => states.Add(s) },
        });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Down, 0f));
        host.Update(Viewport);
        Assert.Contains(states, s => s.Has(WidgetState.Pressed)); // つまみを掴んだ

        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Up, 0.1f));
        host.Update(Viewport);
        Assert.False(states[^1].Has(WidgetState.Pressed)); // 離したら解除
    }

    [Fact]
    public void Slider_OnStateChanged_FiresHovered()
    {
        var states = new List<WidgetState>();
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Slider { Value = 0.5f, OnStateChanged = s => states.Add(s) },
        });
        host.Update(Viewport);

        host.DispatchHover(new Vec2(10, 10));
        host.Update(Viewport);
        Assert.Contains(states, s => s.Has(WidgetState.Hovered));
    }

    [Fact]
    public void TextField_OnStateChanged_FiresFocused()
    {
        var states = new List<WidgetState>();
        var host = new HamonRoot(new StubText());
        var controller = new TextEditingController(string.Empty);
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new TextField { Controller = controller, Width = Dimension.Px(120f), OnStateChanged = s => states.Add(s) },
        });
        host.Update(Viewport);

        host.MoveFocus(FocusDirection.Down); // テキストフィールドへフォーカス
        host.Update(Viewport);
        Assert.Contains(states, s => s.Has(WidgetState.Focused));
    }

    [Fact]
    public void VirtualJoystick_OnActiveChanged_FiresGrabAndRelease()
    {
        var active = new List<bool>();
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new VirtualJoystick { Size = 120f, OnActiveChanged = a => active.Add(a) },
        });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Down, 0f)); // 中心を掴む
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Up, 0.1f));  // 離す

        Assert.Equal(new[] { true, false }, active);
    }
}
