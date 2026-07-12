using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Behavior test of composite (CooldownButton/SlotButton/Badge) for games.</summary>
public class GameWidgetsTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    private static HamonRoot Mount(Widget w)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = w });
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void CooldownButton_DisabledWhileCooling()
    {
        int presses = 0;
        HamonRoot host = Mount(new CooldownButton { Progress = 0.5f, Size = 56f, OnPressed = () => presses++ });

        host.DispatchPointer(new PointerEvent(new Vec2(28, 28), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(28, 28), PointerPhase.Up, 0.01f));
        Assert.Equal(0, presses); // クールダウン中は押せない
    }

    [Fact]
    public void CooldownButton_PressableWhenReady()
    {
        int presses = 0;
        HamonRoot host = Mount(new CooldownButton { Progress = 1f, Size = 56f, OnPressed = () => presses++ });

        host.DispatchPointer(new PointerEvent(new Vec2(28, 28), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(28, 28), PointerPhase.Up, 0.01f));
        Assert.Equal(1, presses);
    }

    [Fact]
    public void SlotButton_Tap_FiresOnPressed()
    {
        int presses = 0;
        HamonRoot host = Mount(new SlotButton { Size = 56f, Count = 3, OnPressed = () => presses++ });

        host.DispatchPointer(new PointerEvent(new Vec2(28, 28), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(28, 28), PointerPhase.Up, 0.01f));
        Assert.Equal(1, presses);
    }

    [Fact]
    public void Badge_Hidden_ShowsOnlyChild()
    {
        HamonRoot host = Mount(new Badge { Show = false, Label = "9", Child = new SizedBox { Width = Dimension.Px(20), Height = Dimension.Px(20) } });
        // Show=false なら子だけ（Stack を作らない）。Align→StatelessElement(Badge)→built。
        Assert.IsType<SizedBox>(host.Root!.Children[0].Children[0].Widget);
    }

    [Fact]
    public void Badge_Shown_WrapsInStack()
    {
        HamonRoot host = Mount(new Badge { Show = true, Label = "9", Child = new SizedBox { Width = Dimension.Px(20), Height = Dimension.Px(20) } });
        Assert.IsType<Stack>(host.Root!.Children[0].Children[0].Widget);
    }
}
