namespace Hamon.Layout;

/// <summary>
/// Custom measurement hook for a virtualized viewport (<c>ListView</c> etc.). Called on the node that has
/// <see cref="LayoutNode.Virtual"/> set, using the node's children and scroll offset to compute and
/// return the viewport size (the engine's subsequent Arrange pass positions children at absolute coordinates).
/// </summary>
public interface IVirtualLayout
{
    Size Measure(LayoutNode node, BoxConstraints constraints);
}

/// <summary>
/// A node to be laid out (the base unit of the retained tree). Has a <see cref="Style"/> and children;
/// leaf nodes use <see cref="Measure"/> to return the natural size of their content (e.g. text).
/// After layout, <see cref="Size"/> and <see cref="Bounds"/> (the absolute rectangle) are determined.
/// </summary>
public sealed class LayoutNode
{
    private readonly List<LayoutNode> _children = new();

    public LayoutNode(Style style = default, Func<BoxConstraints, Size>? measure = null)
    {
        Style = style;
        Measure = measure;
    }

    public Style Style { get; set; }

    /// <summary>A function that returns the natural content size of a leaf node (such as text measurement).</summary>
    public Func<BoxConstraints, Size>? Measure { get; set; }

    /// <summary>When set, this node is measured as a virtualized viewport using <see cref="IVirtualLayout.Measure"/> (e.g. <c>ListView</c>).</summary>
    public IVirtualLayout? Virtual { get; set; }

    public IReadOnlyList<LayoutNode> Children => _children;

    /// <summary>The size of the border box determined by the layout.</summary>
    public Size Size { get; internal set; }

    /// <summary>Absolute rectangle determined by Arrange.</summary>
    public Rect Bounds { get; internal set; }

    // --- レイアウト中の一時値（親が設定。フレーム毎の追加アロケを避けるためノードに保持） ---
    internal float OffsetX;
    internal float OffsetY;
    internal float BaseMain;
    internal float FinalMain;

    /// <summary>Scroll amount (main axis) for <see cref="LayoutKind.Scroll"/>; set by the <c>ScrollView</c> entity before layout.</summary>
    internal float ScrollOffset;

    /// <summary>Constraints from the last measurement (used for relayout of subtrees = targeted relayout).</summary>
    internal BoxConstraints LastConstraints;

    public LayoutNode Add(LayoutNode child)
    {
        _children.Add(child);
        return this;
    }

    public void Clear() => _children.Clear();
}
