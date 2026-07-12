using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for VirtualJoystick/Dpad (direction output, automatic centering, press detection).</summary>
public class GameInputTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 300);

    private static (HamonRoot host, Element el) Mount(Widget w)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = w });
        host.Update(Viewport);
        return (host, host.Root!.Children[0]);
    }

    [Fact]
    public void Joystick_DragRight_OutputsPositiveX()
    {
        Vec2 last = Vec2.Zero;
        // size 120, knob 48 → center (60,60), maxRadius=36。右端まで引く。
        (HamonRoot host, _) = Mount(new VirtualJoystick { Size = 120f, KnobSize = 48f, OnChanged = v => last = v });

        host.DispatchPointer(new PointerEvent(new Vec2(70, 60), PointerPhase.Down, 0f));  // 内側で押す（捕捉）
        host.DispatchPointer(new PointerEvent(new Vec2(200, 60), PointerPhase.Move, 0.01f)); // 右へ大きくドラッグ（clamp）
        Assert.Equal(1f, last.X, 0.01f);   // 右いっぱい
        Assert.Equal(0f, last.Y, 0.01f);
    }

    [Fact]
    public void Joystick_Release_AutoCenters()
    {
        Vec2 last = new(9f, 9f);
        (HamonRoot host, _) = Mount(new VirtualJoystick { Size = 120f, KnobSize = 48f, OnChanged = v => last = v });

        host.DispatchPointer(new PointerEvent(new Vec2(90, 60), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(90, 60), PointerPhase.Up, 0.01f));
        Assert.Equal(0f, last.X, 0.01f);
        Assert.Equal(0f, last.Y, 0.01f);
    }

    [Fact]
    public void Joystick_DragWithinRadius_NotClamped()
    {
        Vec2 last = Vec2.Zero;
        (HamonRoot host, _) = Mount(new VirtualJoystick { Size = 120f, KnobSize = 48f, AutoCenter = false, OnChanged = v => last = v });

        // 中心(60,60) から下へ 18px（maxR=36 の半分）→ y=0.5
        host.DispatchPointer(new PointerEvent(new Vec2(60, 78), PointerPhase.Down, 0f));
        Assert.Equal(0f, last.X, 0.01f);
        Assert.Equal(0.5f, last.Y, 0.01f);
    }

    [Fact]
    public void Dpad_PressRegions_EmitDirections()
    {
        var log = new System.Collections.Generic.List<FocusDirection>();
        // size 120 → center (60,60)。
        (HamonRoot host, Element el) = Mount(new Dpad { Size = 120f, OnPressed = d => log.Add(d) });

        host.DispatchPointer(new PointerEvent(new Vec2(60, 10), PointerPhase.Down, 0f)); // 上
        host.DispatchPointer(new PointerEvent(new Vec2(60, 10), PointerPhase.Up, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(110, 60), PointerPhase.Down, 0.02f)); // 右
        host.DispatchPointer(new PointerEvent(new Vec2(110, 60), PointerPhase.Up, 0.03f));

        Assert.Equal(new[] { FocusDirection.Up, FocusDirection.Right }, log);
    }

    [Fact]
    public void Dpad_DeadZone_IgnoresCenter()
    {
        int presses = 0;
        (HamonRoot host, _) = Mount(new Dpad { Size = 120f, DeadZone = 20f, OnPressed = _ => presses++ });

        host.DispatchPointer(new PointerEvent(new Vec2(62, 62), PointerPhase.Down, 0f)); // 中心付近＝デッドゾーン
        Assert.Equal(0, presses);
    }

    [Fact]
    public void Dpad_Release_FiresOnReleased()
    {
        FocusDirection? released = null;
        (HamonRoot host, _) = Mount(new Dpad { Size = 120f, OnReleased = d => released = d });

        host.DispatchPointer(new PointerEvent(new Vec2(10, 60), PointerPhase.Down, 0f)); // 左
        host.DispatchPointer(new PointerEvent(new Vec2(10, 60), PointerPhase.Up, 0.01f));
        Assert.Equal(FocusDirection.Left, released);
    }
}
