using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Layout determinism test for Flutter style widgets (Row/Column/Container/SizedBox).</summary>
public class FlutterWidgetsTests
{
    private static SizedBox Sb(float w, float h) =>
        new() { Width = Dimension.Px(w), Height = Dimension.Px(h) };

    private static RenderElement Build(Widget widget)
    {
        var element = (RenderElement)Reconciler.Reconcile(null, widget);
        FlexLayoutEngine.Layout(element.LayoutNode, BoxConstraints.Loose(new Size(1000, 1000)));
        return element;
    }

    [Fact]
    public void Row_Spacing_InsertsGapBetweenChildren()
    {
        RenderElement e = Build(new Row { Spacing = 8f, Children = new Widget[] { Sb(20, 10), Sb(20, 10) } });

        Assert.Equal(0f, e.Children[0].LayoutNode.Bounds.X, 3);
        Assert.Equal(28f, e.Children[1].LayoutNode.Bounds.X, 3); // 20 + spacing 8
    }

    [Fact]
    public void Column_Spacing_InsertsGapVertically()
    {
        RenderElement e = Build(new Column { Spacing = 6f, Children = new Widget[] { Sb(10, 20), Sb(10, 20) } });

        Assert.Equal(0f, e.Children[0].LayoutNode.Bounds.Y, 3);
        Assert.Equal(26f, e.Children[1].LayoutNode.Bounds.Y, 3); // 20 + spacing 6
    }

    [Fact]
    public void Row_MainAxisSizeMin_ShrinksToContent()
    {
        RenderElement e = Build(new Row { MainAxisSize = MainAxisSize.Min, Children = new Widget[] { Sb(20, 10), Sb(20, 10) } });

        Assert.Equal(40f, e.LayoutNode.Bounds.Width, 3); // 内容に縮む（fill しない）
    }

    [Fact]
    public void Container_Padding_WrapsChild()
    {
        RenderElement e = Build(new Container { Padding = EdgeInsets.All(8), Child = Sb(20, 20) });

        Assert.Equal(36f, e.LayoutNode.Bounds.Width, 3); // 20 + 8*2
        Assert.Equal(36f, e.LayoutNode.Bounds.Height, 3);
        Assert.Equal(8f, e.Children[0].LayoutNode.Bounds.X, 3);
        Assert.Equal(8f, e.Children[0].LayoutNode.Bounds.Y, 3);
    }

    [Fact]
    public void SizedBox_FixedSize()
    {
        RenderElement e = Build(new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(40) });

        Assert.Equal(50f, e.LayoutNode.Bounds.Width, 3);
        Assert.Equal(40f, e.LayoutNode.Bounds.Height, 3);
    }
}
