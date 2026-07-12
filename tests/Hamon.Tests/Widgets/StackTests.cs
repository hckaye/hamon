using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism tests for Stack (stacked, anchored, z-ordered) and Positioned (absolutely positioned inset).</summary>
public class StackTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static HamonRoot Mount(Widget root, Size size)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => root);
        host.Update(size);
        return host;
    }

    private static Rect ChildBounds(HamonRoot host, int index) =>
        host.Root!.LayoutNode.Children[index].Bounds;

    [Fact]
    public void Stack_FillsTightConstraints_AtRoot()
    {
        var host = Mount(
            new Stack
            {
                Children = new Widget[] { new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) } },
            },
            new Size(300, 200));

        Rect bounds = host.Root!.LayoutNode.Bounds;
        Assert.Equal(300f, bounds.Width);
        Assert.Equal(200f, bounds.Height);
    }

    [Fact]
    public void Stack_AnchorsNonPositionedChild_Center()
    {
        var host = Mount(
            new Stack
            {
                Alignment = Alignment.Center,
                Children = new Widget[] { new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(20) } },
            },
            new Size(300, 200));

        Rect child = ChildBounds(host, 0);
        Assert.Equal((300f - 40f) / 2f, child.X);
        Assert.Equal((200f - 20f) / 2f, child.Y);
    }

    [Fact]
    public void Stack_AnchorsNonPositionedChild_BottomRight()
    {
        var host = Mount(
            new Stack
            {
                Alignment = Alignment.BottomRight,
                Children = new Widget[] { new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(20) } },
            },
            new Size(300, 200));

        Rect child = ChildBounds(host, 0);
        Assert.Equal(300f - 40f, child.X);
        Assert.Equal(200f - 20f, child.Y);
    }

    [Fact]
    public void Positioned_LeftTop_PlacesAtInset()
    {
        var host = Mount(
            new Stack
            {
                Children = new Widget[]
                {
                    new Positioned
                    {
                        Left = Dimension.Px(30),
                        Top = Dimension.Px(20),
                        Width = Dimension.Px(50),
                        Height = Dimension.Px(60),
                        Child = new SizedBox(),
                    },
                },
            },
            new Size(300, 200));

        Rect child = ChildBounds(host, 0);
        Assert.Equal(30f, child.X);
        Assert.Equal(20f, child.Y);
        Assert.Equal(50f, child.Width);
        Assert.Equal(60f, child.Height);
    }

    [Fact]
    public void Positioned_BothEnds_StretchesToFillBetweenInsets()
    {
        var host = Mount(
            new Stack
            {
                Children = new Widget[]
                {
                    new Positioned
                    {
                        Left = Dimension.Px(10),
                        Right = Dimension.Px(20),
                        Top = Dimension.Px(0),
                        Bottom = Dimension.Px(0),
                        Child = new SizedBox(),
                    },
                },
            },
            new Size(300, 200));

        Rect child = ChildBounds(host, 0);
        Assert.Equal(10f, child.X);
        Assert.Equal(300f - 10f - 20f, child.Width); // 270
        Assert.Equal(0f, child.Y);
        Assert.Equal(200f, child.Height);
    }

    [Fact]
    public void Positioned_RightBottom_AnchorsToFarEdges()
    {
        var host = Mount(
            new Stack
            {
                Children = new Widget[]
                {
                    new Positioned
                    {
                        Right = Dimension.Px(12),
                        Bottom = Dimension.Px(8),
                        Width = Dimension.Px(40),
                        Height = Dimension.Px(30),
                        Child = new SizedBox(),
                    },
                },
            },
            new Size(300, 200));

        Rect child = ChildBounds(host, 0);
        Assert.Equal(300f - 12f - 40f, child.X);
        Assert.Equal(200f - 8f - 30f, child.Y);
    }

    [Fact]
    public void Positioned_ChildFillsPositionedRect()
    {
        // Positioned の子（背景付き SizedBox 相当）は配置矩形いっぱいに広がる。
        var host = Mount(
            new Stack
            {
                Children = new Widget[]
                {
                    new Positioned
                    {
                        Left = Dimension.Px(0),
                        Top = Dimension.Px(0),
                        Width = Dimension.Px(80),
                        Height = Dimension.Px(40),
                        Child = new Container { Color = Color.Red },
                    },
                },
            },
            new Size(300, 200));

        // Positioned ノードの子（Container）が矩形を満たす
        Rect inner = host.Root!.LayoutNode.Children[0].Children[0].Bounds;
        Assert.Equal(80f, inner.Width);
        Assert.Equal(40f, inner.Height);
    }

    [Fact]
    public void Stack_ZOrder_TopmostHitTestWins()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int back = 0;
        int front = 0;
        host.SetRoot(() => new Stack
        {
            Children = new Widget[]
            {
                new GestureDetector { OnTap = () => back++, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } },
                new GestureDetector { OnTap = () => front++, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } },
            },
        });
        host.Update(new Size(300, 200));

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up));

        Assert.Equal(0, back);
        Assert.Equal(1, front); // 後の子（前面）が勝つ
    }
}
