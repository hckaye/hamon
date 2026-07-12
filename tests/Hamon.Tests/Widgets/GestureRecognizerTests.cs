using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test of gesture recognizer (long press/double tap).</summary>
public class GestureRecognizerTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    private static HamonRoot Mount(GestureDetector detector)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => detector);
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void LongPress_FiresAfterDuration_SuppressesTap()
    {
        int longPress = 0;
        int tap = 0;
        HamonRoot host = Mount(new GestureDetector
        {
            LongPressDuration = 0.5f,
            OnLongPress = () => longPress++,
            OnTap = () => tap++,
            Child = new SizedBox { Width = Dimension.Px(120), Height = Dimension.Px(120) },
        });

        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Down, 0.01f));
        host.Update(Viewport, 0.6f); // 0.6s 保持＝長押し確定
        Assert.Equal(1, longPress);

        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Up, 0.61f));
        Assert.Equal(0, tap); // 長押し後の離しはタップにしない
    }

    [Fact]
    public void QuickRelease_FiresTap_NotLongPress()
    {
        int longPress = 0;
        int tap = 0;
        HamonRoot host = Mount(new GestureDetector
        {
            OnLongPress = () => longPress++,
            OnTap = () => tap++,
            Child = new SizedBox { Width = Dimension.Px(120), Height = Dimension.Px(120) },
        });

        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Down, 0.01f));
        host.Update(Viewport, 0.1f); // 閾値未満
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Up, 0.11f));

        Assert.Equal(0, longPress);
        Assert.Equal(1, tap);
    }

    [Fact]
    public void Move_CancelsLongPress()
    {
        int longPress = 0;
        HamonRoot host = Mount(new GestureDetector
        {
            OnLongPress = () => longPress++,
            Child = new SizedBox { Width = Dimension.Px(160), Height = Dimension.Px(160) },
        });

        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(60, 110), PointerPhase.Move, 0.05f)); // 50px 移動＝slop 超
        host.Update(Viewport, 0.6f);

        Assert.Equal(0, longPress); // 動いたら長押し不成立
    }

    [Fact]
    public void DoubleTap_FiresOnTwoQuickTaps()
    {
        int doubleTap = 0;
        int tap = 0;
        HamonRoot host = Mount(new GestureDetector
        {
            DoubleTapWindow = 0.3f,
            OnDoubleTap = () => doubleTap++,
            OnTap = () => tap++,
            Child = new SizedBox { Width = Dimension.Px(120), Height = Dimension.Px(120) },
        });

        // 1回目（通常タップ）
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Up, 0.05f));
        // 2回目（窓内＝ダブルタップ、2回目はタップにしない）
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Down, 0.10f));
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Up, 0.15f));

        Assert.Equal(1, tap);
        Assert.Equal(1, doubleTap);
    }

    [Fact]
    public void SlowTwoTaps_AreTwoSingleTaps_NotDouble()
    {
        int doubleTap = 0;
        int tap = 0;
        HamonRoot host = Mount(new GestureDetector
        {
            DoubleTapWindow = 0.3f,
            OnDoubleTap = () => doubleTap++,
            OnTap = () => tap++,
            Child = new SizedBox { Width = Dimension.Px(120), Height = Dimension.Px(120) },
        });

        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Up, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Down, 1.00f));
        host.DispatchPointer(new PointerEvent(new Vec2(60, 60), PointerPhase.Up, 1.05f)); // 窓外

        Assert.Equal(2, tap);
        Assert.Equal(0, doubleTap);
    }
}
