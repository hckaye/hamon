using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for pointer (hit test, tap, capture, tap → focus) and Button.</summary>
public class PointerTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static void Down(HamonRoot host, float x, float y) =>
        host.DispatchPointer(new PointerEvent(new Vec2(x, y), PointerPhase.Down));

    private static void Up(HamonRoot host, float x, float y) =>
        host.DispatchPointer(new PointerEvent(new Vec2(x, y), PointerPhase.Up));

    [Fact]
    public void Tap_InsideBounds_FiresOnTap()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int taps = 0;
        host.SetRoot(() => new GestureDetector
        {
            OnTap = () => taps++,
            Child = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) },
        });
        host.Update(new Size(200, 200));

        Down(host, 10, 10);
        Up(host, 10, 10);

        Assert.Equal(1, taps);
    }

    [Fact]
    public void Tap_ReleasedOutside_DoesNotFire()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int taps = 0;
        host.SetRoot(() => new GestureDetector
        {
            OnTap = () => taps++,
            Child = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) },
        });
        host.Update(new Size(200, 200));

        Down(host, 10, 10);
        Up(host, 500, 500); // 範囲外で離す → タップ不成立（キャプチャ先へ配送）

        Assert.Equal(0, taps);
    }

    [Fact]
    public void HitTest_PicksTopmostOfOverlapping()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int first = 0;
        int second = 0;
        host.SetRoot(() => new Container
        {
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Start,
                Children = new Widget[]
                {
                    new GestureDetector { OnTap = () => first++, Child = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) } },
                    new GestureDetector { OnTap = () => second++, Child = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) } },
                },
            },
        });
        host.Update(new Size(200, 200));

        // 2つ目（y=50..100）の中をタップ
        Down(host, 10, 70);
        Up(host, 10, 70);

        Assert.Equal(0, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void Button_TapAndGamepad_BothInvokeOnPressed()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int pressed = 0;
        var node = new FocusNode();
        host.SetRoot(() => new Button
        {
            Node = node,
            OnPressed = () => pressed++,
            Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) },
        });
        host.Update(new Size(200, 200));

        // タップ
        Down(host, 10, 10);
        Up(host, 10, 10);
        Assert.Equal(1, pressed);

        // ゲームパッド OK（A）。タップでフォーカス済みのはず
        Assert.Same(node, host.Focus.Focused);
        host.HandleButtonDown(GamepadButton.A);
        Assert.Equal(2, pressed);
    }

    [Fact]
    public void Tap_FocusesAncestorFocusNode()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var node = new FocusNode();
        host.SetRoot(() => new Focus
        {
            Node = node,
            Child = new GestureDetector { Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) } },
        });
        host.Update(new Size(200, 200));

        Down(host, 10, 10);

        Assert.Same(node, host.Focus.Focused);
    }
}
