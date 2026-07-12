using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of Button's visual builder (escape hatch that assembles the entire appearance depending on the state).</summary>
public class ButtonBuilderTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    [Fact]
    public void Builder_RebuildsChild_OnPressState()
    {
        var seen = new List<WidgetState>();
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Button
            {
                OnPressed = () => { },
                Builder = s =>
                {
                    seen.Add(s);
                    return new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) };
                },
            },
        });
        host.Update(Viewport);

        seen.Clear();
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport); // 状態変化で子を組み直す
        Assert.Contains(seen, s => s.Has(WidgetState.Pressed));
    }

    [Fact]
    public void Builder_StillActivatesOnTap()
    {
        int taps = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Button
            {
                OnPressed = () => taps++,
                Builder = _ => new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
            },
        });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.01f));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void Builder_ReceivesDisabledState()
    {
        WidgetState last = WidgetState.None;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Button
        {
            Enabled = false,
            OnPressed = () => { },
            Builder = s =>
            {
                last = s;
                return new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) };
            },
        });
        host.Update(Viewport);
        Assert.True(last.Has(WidgetState.Disabled));
    }
}
