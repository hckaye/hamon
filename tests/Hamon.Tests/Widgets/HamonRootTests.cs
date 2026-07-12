using Hamon;
using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic testing for reactive rebuilding, dirty driving, and layout enforcement (GPU independent/stub renderer).</summary>
public class HamonRootTests
{
    /// <summary>Text measurement stub without GPU (1 character = size*0.5 width, height = size).</summary>
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize * 0.5f, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Style Container = new() { Direction = Axis.Horizontal };

    private static BoxWidget Leaf(float w, float h) =>
        new() { Style = new Style { Width = Dimension.Px(w), Height = Dimension.Px(h) } };

    [Fact]
    public void StateChange_RebuildsTree()
    {
        var host = new HamonRoot(new StubTextRenderer());
        State<int> count = host.CreateState(1);
        host.SetRoot(() =>
        {
            var kids = new Widget[count.Value];
            for (int i = 0; i < kids.Length; i++)
            {
                kids[i] = Leaf(10, 10);
            }

            return new BoxWidget { Style = Container, Children = kids };
        });

        host.Update(new Size(300, 50));
        Assert.Single(((RenderElement)host.Root!).Children);

        count.Value = 3;
        host.Update(new Size(300, 50));
        Assert.Equal(3, ((RenderElement)host.Root!).Children.Count);
    }

    [Fact]
    public void NoStateChange_KeepsRootInstance()
    {
        var host = new HamonRoot(new StubTextRenderer());
        State<int> n = host.CreateState(2);
        host.SetRoot(() => new BoxWidget { Style = Container, Children = new Widget[] { Leaf(10, 10), Leaf(10, 10) } });

        host.Update(new Size(300, 50));
        Element root = host.Root!;
        host.Update(new Size(300, 50)); // dirty でない → 同一実体

        Assert.Same(root, host.Root);
    }

    [Fact]
    public void Update_AppliesLayout()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new BoxWidget
        {
            Style = new Style { Direction = Axis.Horizontal, Width = Dimension.Px(300), Height = Dimension.Px(50) },
            Children = new Widget[] { Leaf(40, 20), Leaf(60, 20) },
        });

        host.Update(new Size(300, 50));

        var re = (RenderElement)host.Root!;
        Assert.Equal(0f, re.Children[0].LayoutNode.Bounds.X, 3);
        Assert.Equal(40f, re.Children[1].LayoutNode.Bounds.X, 3);
    }

    [Fact]
    public void Text_MeasuredViaRenderer()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new BoxWidget
        {
            Style = new Style { Direction = Axis.Horizontal, Width = Dimension.Px(300), Height = Dimension.Px(50) },
            Children = new Widget[] { new Text("abcd") { FontSize = 10f, Color = Color.White } },
        });

        host.Update(new Size(300, 50));

        // stub: 4 文字 * 10 * 0.5 = 20 幅、高さ 10
        Rect bounds = ((RenderElement)host.Root!).Children[0].LayoutNode.Bounds;
        Assert.Equal(20f, bounds.Width, 3);
        Assert.Equal(10f, bounds.Height, 3);
    }
}
