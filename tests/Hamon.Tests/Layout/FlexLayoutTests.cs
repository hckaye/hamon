using Hamon.Layout;
using Xunit;

namespace Hamon.Tests.Layout;

/// <summary>Deterministic testing of the Flexbox subset layout solver (GPU independent/pure).</summary>
public class FlexLayoutTests
{
    // 列挙改称: Axis / MainAxisAlignment / CrossAxisAlignment
    private static LayoutNode Fixed(float w, float h) =>
        new(new Style { Width = Dimension.Px(w), Height = Dimension.Px(h) });

    private static void AssertRect(float x, float y, float w, float h, Rect r)
    {
        Assert.Equal(x, r.X, 3);
        Assert.Equal(y, r.Y, 3);
        Assert.Equal(w, r.Width, 3);
        Assert.Equal(h, r.Height, 3);
    }

    [Fact]
    public void Leaf_FixedSize_UsesFixed()
    {
        var node = Fixed(40, 20);
        FlexLayoutEngine.Layout(node, BoxConstraints.Loose(new Size(1000, 1000)));
        AssertRect(0, 0, 40, 20, node.Bounds);
    }

    [Fact]
    public void Leaf_Measure_UsesIntrinsic()
    {
        var node = new LayoutNode(default, _ => new Size(123, 45));
        FlexLayoutEngine.Layout(node, BoxConstraints.Loose(new Size(1000, 1000)));
        AssertRect(0, 0, 123, 45, node.Bounds);
    }

    [Fact]
    public void Row_FixedChildren_LaidOutSequentially()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, CrossAxisAlignment = CrossAxisAlignment.Start, Width = Dimension.Px(300), Height = Dimension.Px(50) });
        root.Add(Fixed(40, 20)).Add(Fixed(60, 20)).Add(Fixed(50, 20));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(0, 0, 300, 50, root.Bounds);
        AssertRect(0, 0, 40, 20, root.Children[0].Bounds);
        AssertRect(40, 0, 60, 20, root.Children[1].Bounds);
        AssertRect(100, 0, 50, 20, root.Children[2].Bounds);
    }

    [Fact]
    public void Row_TwoGrow_SplitFreeSpaceEqually()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, Width = Dimension.Px(100), Height = Dimension.Px(10) });
        root.Add(new LayoutNode(new Style { FlexGrow = 1, Height = Dimension.Px(10) }));
        root.Add(new LayoutNode(new Style { FlexGrow = 1, Height = Dimension.Px(10) }));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(0, 0, 50, 10, root.Children[0].Bounds);
        AssertRect(50, 0, 50, 10, root.Children[1].Bounds);
    }

    [Fact]
    public void Row_FixedPlusGrow_GrowTakesRemainder()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, Width = Dimension.Px(100), Height = Dimension.Px(10) });
        root.Add(Fixed(30, 10));
        root.Add(new LayoutNode(new Style { FlexGrow = 1, Height = Dimension.Px(10) }));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(0, 0, 30, 10, root.Children[0].Bounds);
        AssertRect(30, 0, 70, 10, root.Children[1].Bounds);
    }

    [Fact]
    public void Row_Padding_OffsetsChildren()
    {
        var root = new LayoutNode(new Style
        {
            Direction = Axis.Horizontal,
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Width = Dimension.Px(100),
            Height = Dimension.Px(40),
            Padding = EdgeInsets.All(8),
        });
        root.Add(Fixed(20, 20));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        // padding 8 で内側へ寄る（align Start なので交差軸も padding 分）
        AssertRect(8, 8, 20, 20, root.Children[0].Bounds);
    }

    [Fact]
    public void Row_JustifyCenter_CentersBlock()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, MainAxisAlignment = MainAxisAlignment.Center, Width = Dimension.Px(100), Height = Dimension.Px(10) });
        root.Add(Fixed(20, 10));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(40, 0, 20, 10, root.Children[0].Bounds);
    }

    [Fact]
    public void Row_JustifySpaceBetween_PushesToEnds()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, MainAxisAlignment = MainAxisAlignment.SpaceBetween, Width = Dimension.Px(100), Height = Dimension.Px(10) });
        root.Add(Fixed(20, 10)).Add(Fixed(20, 10));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(0, 0, 20, 10, root.Children[0].Bounds);
        AssertRect(80, 0, 20, 10, root.Children[1].Bounds);
    }

    [Fact]
    public void Row_AlignCenter_CentersOnCrossAxis()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, CrossAxisAlignment = CrossAxisAlignment.Center, Width = Dimension.Px(100), Height = Dimension.Px(50) });
        root.Add(Fixed(20, 20));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        // (50 - 20) / 2 = 15
        AssertRect(0, 15, 20, 20, root.Children[0].Bounds);
    }

    [Fact]
    public void Row_AlignStretch_FillsCrossAxis()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, CrossAxisAlignment = CrossAxisAlignment.Stretch, Width = Dimension.Px(100), Height = Dimension.Px(50) });
        root.Add(new LayoutNode(new Style { Width = Dimension.Px(20) })); // 高さ Auto → stretch

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(0, 0, 20, 50, root.Children[0].Bounds);
    }

    [Fact]
    public void Column_FixedChildren_StackVertically()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Vertical, Width = Dimension.Px(50), Height = Dimension.Px(100) });
        root.Add(Fixed(50, 20)).Add(Fixed(50, 30));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(0, 0, 50, 20, root.Children[0].Bounds);
        AssertRect(0, 20, 50, 30, root.Children[1].Bounds);
    }

    [Fact]
    public void Margin_AddsSpaceAroundChild()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, CrossAxisAlignment = CrossAxisAlignment.Start, Width = Dimension.Px(100), Height = Dimension.Px(40) });
        root.Add(new LayoutNode(new Style { Width = Dimension.Px(20), Height = Dimension.Px(20), Margin = EdgeInsets.All(5) }));
        root.Add(Fixed(20, 20));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        // 1つ目: margin 5 で (5,5)。2つ目: 5+20+5 = 30 から開始
        AssertRect(5, 5, 20, 20, root.Children[0].Bounds);
        AssertRect(30, 0, 20, 20, root.Children[1].Bounds);
    }

    [Fact]
    public void PercentWidth_ResolvesAgainstParent()
    {
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, Width = Dimension.Px(200), Height = Dimension.Px(20) });
        root.Add(new LayoutNode(new Style { Width = Dimension.Percent(50), Height = Dimension.Px(20) }));

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(0, 0, 100, 20, root.Children[0].Bounds);
    }

    [Fact]
    public void Nested_AbsoluteCoordinatesAccumulate()
    {
        var inner = new LayoutNode(new Style { Direction = Axis.Horizontal, Width = Dimension.Px(60), Height = Dimension.Px(20) });
        inner.Add(Fixed(20, 20));
        var root = new LayoutNode(new Style { Direction = Axis.Horizontal, Width = Dimension.Px(100), Height = Dimension.Px(40), Padding = EdgeInsets.All(10) });
        root.Add(inner);

        FlexLayoutEngine.Layout(root, BoxConstraints.Loose(new Size(1000, 1000)));

        AssertRect(10, 10, 60, 20, inner.Bounds);
        // 孫は inner の原点(10,10) からさらに 0 → (10,10)
        AssertRect(10, 10, 20, 20, inner.Children[0].Bounds);
    }
}
