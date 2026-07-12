using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test of Expanded/Flexible/Spacer (distribute surplus main axis with flex-grow).</summary>
public class ExpandedTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static Element MountRoot(Widget root, Size size)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => root);
        host.Update(size);
        return host.Root!;
    }

    [Fact]
    public void Expanded_FillsRemainingMainAxis()
    {
        Element col = MountRoot(
            new Column
            {
                Children = new Widget[]
                {
                    new SizedBox { Height = Dimension.Px(50) },
                    new Expanded { Child = new SizedBox() },
                    new SizedBox { Height = Dimension.Px(30) },
                },
            },
            new Size(200, 300));

        // 300 - 50 - 30 = 220 が Expanded の高さ。
        Assert.Equal(220f, col.Children[1].LayoutNode.Bounds.Height, 0.5f);
        Assert.Equal(50f, col.Children[1].LayoutNode.Bounds.Y, 0.5f); // ヘッダ直後
    }

    [Fact]
    public void TwoExpanded_ShareByFlex()
    {
        Element col = MountRoot(
            new Column
            {
                Children = new Widget[]
                {
                    new Expanded { Flex = 1, Child = new SizedBox() },
                    new Expanded { Flex = 3, Child = new SizedBox() },
                },
            },
            new Size(200, 320));

        // 1:3 配分 → 80 / 240。
        Assert.Equal(80f, col.Children[0].LayoutNode.Bounds.Height, 0.5f);
        Assert.Equal(240f, col.Children[1].LayoutNode.Bounds.Height, 0.5f);
    }

    [Fact]
    public void Spacer_PushesSiblingsApart()
    {
        Element row = MountRoot(
            new Row
            {
                Children = new Widget[]
                {
                    new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(20) },
                    new Spacer(),
                    new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(20) },
                },
            },
            new Size(200, 40));

        // Spacer 幅 = 200 - 50 - 50 = 100。右の箱は x=150。
        Assert.Equal(100f, row.Children[1].LayoutNode.Bounds.Width, 0.5f);
        Assert.Equal(150f, row.Children[2].LayoutNode.Bounds.X, 0.5f);
    }
}
