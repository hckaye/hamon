using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test for keyed reconciliation (reusing, rearranging, inserting, and deleting entities).</summary>
public class ReconcileTests
{
    private static readonly Style Row = new() { Direction = Axis.Horizontal, Width = Dimension.Px(300), Height = Dimension.Px(50) };

    private static BoxWidget Box(Style style, params Widget[] children) =>
        new() { Style = style, Children = children };

    private static BoxWidget Leaf(float w, float h, object? key = null) =>
        new() { Key = key, Style = new Style { Width = Dimension.Px(w), Height = Dimension.Px(h) } };

    [Fact]
    public void Reconcile_BuildsElementTree_WithLayout()
    {
        Element root = Reconciler.Reconcile(null, Box(Row, Leaf(40, 20), Leaf(60, 20)));
        var re = (RenderElement)root;

        Assert.Equal(2, re.Children.Count);
        FlexLayoutEngine.Layout(re.LayoutNode, BoxConstraints.Loose(new Size(1000, 1000)));
        Assert.Equal(0f, re.Children[0].LayoutNode.Bounds.X, 3);
        Assert.Equal(40f, re.Children[1].LayoutNode.Bounds.X, 3);
    }

    [Fact]
    public void Update_SameType_ReusesElementInstance()
    {
        Element root = Reconciler.Reconcile(null, Box(Row, Leaf(40, 20)));
        var re = (RenderElement)root;
        Element child = re.Children[0];

        Element root2 = Reconciler.Reconcile(root, Box(Row, Leaf(99, 20)));

        Assert.Same(root, root2);
        Assert.Same(child, ((RenderElement)root2).Children[0]); // 同一実体を再利用
        Assert.Equal(99f, child.LayoutNode.Style.Width.Value); // Style は更新される
    }

    [Fact]
    public void Update_KeyMismatch_ReplacesElement()
    {
        Element root = Reconciler.Reconcile(null, new BoxWidget { Key = "a", Style = Row });
        Element root2 = Reconciler.Reconcile(root, new BoxWidget { Key = "b", Style = Row });

        Assert.NotSame(root, root2);
        Assert.False(root.IsMounted);
        Assert.True(root2.IsMounted);
    }

    [Fact]
    public void KeyedChildren_Reorder_PreservesInstances()
    {
        Element root = Reconciler.Reconcile(null, Box(Row, Leaf(10, 10, "a"), Leaf(10, 10, "b")));
        var re = (RenderElement)root;
        Element ea = re.Children[0];
        Element eb = re.Children[1];

        Reconciler.Reconcile(root, Box(Row, Leaf(10, 10, "b"), Leaf(10, 10, "a")));

        Assert.Same(eb, re.Children[0]);
        Assert.Same(ea, re.Children[1]);
    }

    [Fact]
    public void KeyedChildren_Insert_PreservesOthersAndAddsNew()
    {
        Element root = Reconciler.Reconcile(null, Box(Row, Leaf(10, 10, "a"), Leaf(10, 10, "b")));
        var re = (RenderElement)root;
        Element ea = re.Children[0];
        Element eb = re.Children[1];

        Reconciler.Reconcile(root, Box(Row, Leaf(10, 10, "a"), Leaf(10, 10, "c"), Leaf(10, 10, "b")));

        Assert.Equal(3, re.Children.Count);
        Assert.Same(ea, re.Children[0]);
        Assert.Same(eb, re.Children[2]);
        Assert.Equal("c", re.Children[1].Widget.Key);
        Assert.True(re.Children[1].IsMounted);
    }

    [Fact]
    public void KeyedChildren_Remove_UnmountsDropped()
    {
        Element root = Reconciler.Reconcile(null, Box(Row, Leaf(10, 10, "a"), Leaf(10, 10, "b"), Leaf(10, 10, "c")));
        var re = (RenderElement)root;
        Element eb = re.Children[1];

        Reconciler.Reconcile(root, Box(Row, Leaf(10, 10, "a"), Leaf(10, 10, "c")));

        Assert.Equal(2, re.Children.Count);
        Assert.False(eb.IsMounted); // 落とされた子は Unmount される
        Assert.DoesNotContain(eb, re.Children);
    }
}
