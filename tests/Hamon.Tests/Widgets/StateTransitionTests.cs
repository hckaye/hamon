using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Combinations and transitions of interactive states (hover/focus/pressed/disabled)<see cref="FocusableActionDetector"/>of
/// Validate via state builder.
/// </summary>
public class StateTransitionTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    private static (HamonRoot host, System.Func<WidgetState> state) Mount(bool enabled, bool autofocus, FocusNode? node = null)
    {
        WidgetState last = WidgetState.None;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new FocusableActionDetector
            {
                Enabled = enabled,
                Autofocus = autofocus,
                Node = node ?? new FocusNode(),
                Builder = s =>
                {
                    last = s;
                    return new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) };
                },
            },
        });
        host.Update(Viewport);
        return (host, () => last);
    }

    [Fact]
    public void HoverAndFocus_BothActiveSimultaneously()
    {
        (HamonRoot host, var state) = Mount(enabled: true, autofocus: true);
        host.Update(Viewport);
        Assert.True(state().Has(WidgetState.Focused));

        host.DispatchHover(new Vec2(50, 50));
        host.Update(Viewport);
        Assert.True(state().Has(WidgetState.Hovered));
        Assert.True(state().Has(WidgetState.Focused)); // 両立する
    }

    [Fact]
    public void Press_AddsPressed_OverHover()
    {
        (HamonRoot host, var state) = Mount(enabled: true, autofocus: false);
        host.DispatchHover(new Vec2(50, 50));
        host.Update(Viewport);
        Assert.True(state().Has(WidgetState.Hovered));

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport);
        Assert.True(state().Has(WidgetState.Pressed));
        Assert.True(state().Has(WidgetState.Hovered)); // hover は維持されつつ pressed が乗る
    }

    [Fact]
    public void Release_ReturnsToHover()
    {
        (HamonRoot host, var state) = Mount(enabled: true, autofocus: false);
        host.DispatchHover(new Vec2(50, 50));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport);
        Assert.True(state().Has(WidgetState.Pressed));

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.01f));
        host.Update(Viewport);
        Assert.False(state().Has(WidgetState.Pressed));
        Assert.True(state().Has(WidgetState.Hovered)); // 離してもまだ hover 中
    }

    [Fact]
    public void DragOutsideWhilePressed_ClearsPressed()
    {
        (HamonRoot host, var state) = Mount(enabled: true, autofocus: false);
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport);
        Assert.True(state().Has(WidgetState.Pressed));

        host.DispatchPointer(new PointerEvent(new Vec2(150, 50), PointerPhase.Move, 0.01f)); // 範囲外（100x100 の外）
        host.Update(Viewport);
        Assert.False(state().Has(WidgetState.Pressed));
    }

    [Fact]
    public void Disabled_OnlyDisabledState_IgnoresHoverAndPress()
    {
        (HamonRoot host, var state) = Mount(enabled: false, autofocus: false);
        host.DispatchHover(new Vec2(50, 50));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport);

        Assert.True(state().Has(WidgetState.Disabled));
        Assert.False(state().Has(WidgetState.Hovered));
        Assert.False(state().Has(WidgetState.Pressed));
    }
}
