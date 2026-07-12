using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test of automatic repeat and long press (press and hold behavior) of gamepad buttons.</summary>
public class GamepadHoldTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    // 縦並びの3ボタン（各 60px）。先頭を autofocus。
    private static (HamonRoot host, FocusNode a, FocusNode b, FocusNode c) MountThree()
    {
        var a = new FocusNode();
        var b = new FocusNode();
        var c = new FocusNode();
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Column
        {
            Children = new Widget[]
            {
                new Button { Node = a, Autofocus = true, OnPressed = () => { }, Child = new SizedBox { Width = Dimension.Px(180), Height = Dimension.Px(60) } },
                new Button { Node = b, OnPressed = () => { }, Child = new SizedBox { Width = Dimension.Px(180), Height = Dimension.Px(60) } },
                new Button { Node = c, OnPressed = () => { }, Child = new SizedBox { Width = Dimension.Px(180), Height = Dimension.Px(60) } },
            },
        });
        host.Update(Viewport);
        return (host, a, b, c);
    }

    [Fact]
    public void DirectionalHold_AutoNavigatesAfterDelay()
    {
        (HamonRoot host, FocusNode a, FocusNode b, FocusNode c) = MountThree();
        Assert.True(a.HasFocus); // 初期

        host.HandleButtonDown(GamepadButton.DpadDown); // 初回押下＝1つ進む
        Assert.True(b.HasFocus);

        // 初回遅延（0.4s）未満では進まない。
        host.Update(Viewport, 0.3f);
        Assert.True(b.HasFocus);

        // 遅延超＋間隔1つ分でさらに進む。
        host.Update(Viewport, 0.2f);  // 累計 0.5s（>0.4 遅延）→ リピート開始
        Assert.True(c.HasFocus);
    }

    [Fact]
    public void Release_StopsRepeat()
    {
        (HamonRoot host, FocusNode a, FocusNode b, FocusNode c) = MountThree();

        host.HandleButtonDown(GamepadButton.DpadDown); // b
        host.HandleButtonUp(GamepadButton.DpadDown);   // 離す

        host.Update(Viewport, 1.0f); // 十分進めてもリピートしない
        Assert.True(b.HasFocus);
    }

    [Fact]
    public void LongPress_FiresOnceAfterThreshold()
    {
        var a = new FocusNode();
        int longPresses = 0;
        a.OnButtonLongPress = _ => longPresses++;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Focus { Node = a, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } });
        host.Update(Viewport);

        host.HandleButtonDown(GamepadButton.X);
        host.Update(Viewport, 0.3f);
        Assert.Equal(0, longPresses); // 閾値（0.5s）未満
        host.Update(Viewport, 0.3f);
        Assert.Equal(1, longPresses); // 0.6s で発火
        host.Update(Viewport, 0.5f);
        Assert.Equal(1, longPresses); // 二度は発火しない
    }

    [Fact]
    public void NonDirectionalRepeat_DeliveredWhenEnabled()
    {
        var a = new FocusNode();
        int repeats = 0;
        a.OnButtonRepeat = _ => repeats++;
        var host = new HamonRoot(new StubTextRenderer());
        host.Hold.RepeatDirectionalOnly = false; // 非方向もリピート配送
        host.SetRoot(() => new Focus { Node = a, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } });
        host.Update(Viewport);

        host.HandleButtonDown(GamepadButton.X);
        host.Update(Viewport, 0.4f); // 遅延ちょうど
        host.Update(Viewport, 0.24f); // 0.08 間隔 × 3
        Assert.True(repeats >= 3, $"repeats={repeats}");
    }

    [Fact]
    public void NonDirectionalRepeat_SuppressedByDefault()
    {
        var a = new FocusNode();
        int repeats = 0;
        a.OnButtonRepeat = _ => repeats++;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Focus { Node = a, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } });
        host.Update(Viewport);

        host.HandleButtonDown(GamepadButton.X);
        host.Update(Viewport, 1.0f);
        Assert.Equal(0, repeats); // 既定（RepeatDirectionalOnly=true）では非方向は配送しない
    }
}
