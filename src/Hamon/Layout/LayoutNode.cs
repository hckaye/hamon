namespace Hamon.Layout;

/// <summary>
/// Virtualized viewport (<c>ListView</c>etc.) custom measurement hooks. <see cref="LayoutNode.Virtual"/>but
/// Call this on the configured node. <see cref="LayoutNode"/>children and offset of
/// Set and return viewport size (subsequent engine Arrange positions to absolute coordinates).
/// </summary>
public interface IVirtualLayout
{
    Size Measure(LayoutNode node, BoxConstraints constraints);
}

/// <summary>
/// The node to be laid out (the base of the retention tree).<see cref="Style"/>have a child,
/// The leaf nodes are<see cref="Measure"/>returns the natural size of the content (text, etc.).
/// after layout<see cref="Size"/>／<see cref="Bounds"/>(absolute rectangle) is determined.
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

    /// <summary>A function that returns the content natural size of a leaf node (such as text measurement). </summary>
    public Func<BoxConstraints, Size>? Measure { get; set; }

    /// <summary>When set, as a virtualized viewport<see cref="IVirtualLayout.Measure"/>Measure with (<c>ListView</c>etc.).</summary>
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

    /// <summary><see cref="LayoutKind.Scroll"/>Scroll amount (main axis).<c>ScrollView</c>The entity is set before layout.</summary>
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
