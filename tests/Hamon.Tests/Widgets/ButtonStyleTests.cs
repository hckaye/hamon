using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary><see cref="ButtonStyle"/>(Solving by state, size constraints, cursor, StyleFrom) deterministic test.</summary>
public class ButtonStyleTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 300);

    private static (HamonRoot host, Element button) Mount(Button button)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Center { Child = button });
        host.Update(Viewport);
        return (host, host.Root!.Children[0]);
    }

    [Fact]
    public void StyleFrom_ResolvesConstantValuesForAllStates()
    {
        ButtonStyle style = Button.StyleFrom(background: new Color(10, 20, 30), radius: 8f);
        Assert.Equal(new Color(10, 20, 30), style.BackgroundColor!.Resolve(WidgetState.None));
        Assert.Equal(new Color(10, 20, 30), style.BackgroundColor!.Resolve(WidgetState.Pressed));
        Assert.Equal(8f, style.Radius!.Resolve(WidgetState.Hovered));
    }

    [Fact]
    public void FixedSize_SetsExactBounds()
    {
        (_, Element button) = Mount(new Button
        {
            OnPressed = () => { },
            Style = Button.StyleFrom(fixedSize: new Size(120f, 48f)),
            Child = new SizedBox { Width = Dimension.Px(10), Height = Dimension.Px(10) },
        });

        Rect b = button.LayoutNode.Bounds;
        Assert.Equal(120f, b.Width, 0.5f);
        Assert.Equal(48f, b.Height, 0.5f);
    }

    [Fact]
    public void MinimumSize_EnforcesLowerBound()
    {
        (_, Element button) = Mount(new Button
        {
            OnPressed = () => { },
            Style = Button.StyleFrom(minimumSize: new Size(100f, 40f)),
            Child = new SizedBox { Width = Dimension.Px(10), Height = Dimension.Px(10) }, // 小さい子
        });

        Rect b = button.LayoutNode.Bounds;
        Assert.True(b.Width >= 100f, $"width={b.Width}");
        Assert.True(b.Height >= 40f, $"height={b.Height}");
    }

    [Fact]
    public void StateBasedCursor_ResolvesOnHover()
    {
        var style = new ButtonStyle
        {
            MouseCursor = WidgetStateProperty<MouseCursor?>.ResolveWith(s =>
                s.Has(WidgetState.Hovered) ? MouseCursor.Grab : MouseCursor.Basic),
        };
        (HamonRoot host, _) = Mount(new Button
        {
            OnPressed = () => { },
            Style = style,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(40) },
        });

        host.DispatchHover(new Vec2(150, 150)); // 中央のボタンへ
        Assert.Equal(MouseCursor.Grab, host.CurrentCursor);
    }

    [Fact]
    public void DisabledCursor_FromStyle()
    {
        var style = new ButtonStyle
        {
            MouseCursor = WidgetStateProperty<MouseCursor?>.ResolveWith(s =>
                s.Has(WidgetState.Disabled) ? MouseCursor.None : MouseCursor.Click),
        };
        (HamonRoot host, _) = Mount(new Button
        {
            Enabled = false,
            OnPressed = () => { },
            Style = style,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(40) },
        });

        host.DispatchHover(new Vec2(150, 150));
        Assert.Equal(MouseCursor.None, host.CurrentCursor);
    }

    [Fact]
    public void StatePadding_AffectsLayoutViaBaseState()
    {
        (_, Element button) = Mount(new Button
        {
            OnPressed = () => { },
            Style = Button.StyleFrom(padding: EdgeInsets.All(20f)),
            Child = new SizedBox { Width = Dimension.Px(10), Height = Dimension.Px(10) },
        });

        // 子 10x10 + padding 20*2 = 50x50。
        Rect b = button.LayoutNode.Bounds;
        Assert.Equal(50f, b.Width, 0.5f);
        Assert.Equal(50f, b.Height, 0.5f);
    }
}
