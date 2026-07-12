namespace Hamon.Layout;

/// <summary>The main axis direction for flex layout (equivalent to Flutter's <c>Axis</c>).</summary>
public enum Axis : byte
{
    Horizontal,
    Vertical,
}

/// <summary>Placement of children along the main axis (equivalent to Flutter's <c>MainAxisAlignment</c>).</summary>
public enum MainAxisAlignment : byte
{
    Start,
    End,
    Center,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

/// <summary>Placement of children along the cross axis (equivalent to Flutter's <c>CrossAxisAlignment</c>). The default is <see cref="Center"/> (same as Flutter).</summary>
public enum CrossAxisAlignment : byte
{
    /// <summary>Center of cross axis (default = same as Flutter).</summary>
    Center,

    Start,

    End,

    /// <summary>Stretch the cross axis to the full extent of the parent (only for children whose cross axis size is Auto).</summary>
    Stretch,
}

/// <summary>Size along the main axis (equivalent to Flutter's <c>MainAxisSize</c>). The default is <see cref="Max"/>.</summary>
public enum MainAxisSize : byte
{
    /// <summary>Fill the available main-axis space (default).</summary>
    Max,

    /// <summary>Shrinks to fit the content.</summary>
    Min,
}

/// <summary>The node's layout method. The default is <see cref="Flex"/> (Row/Column style).</summary>
public enum LayoutKind : byte
{
    /// <summary>Flexbox subset (main axis/cross axis flow).</summary>
    Flex,

    /// <summary>Overlapping children (equivalent to Flutter's <c>Stack</c>). Children marked <c>Positioned</c> are placed absolutely.</summary>
    Stack,

    /// <summary>A viewport that scrolls along the main axis (<c>ScrollView</c>).</summary>
    Scroll,

    /// <summary>
    /// A single-child box (equivalent to Flutter's <c>Container</c>/<c>Padding</c>/a proxy widget). <b>Deflates the incoming
    /// constraints by the padding before passing them to the child</b> (if the constraints are tight, they remain
    /// tight, so the child fills the available space). The final size clamps child size + padding to the constraints.
    /// </summary>
    Box,

    /// <summary>
    /// A wrapping flow layout (equivalent to Flutter's <c>Wrap</c>).
    /// Children are measured loosely (their natural size). <see cref="Style.Spacing"/> is the spacing along the main
    /// axis, and <see cref="Style.RunSpacing"/> is the spacing between lines (runs).
    /// </summary>
    Wrap,
}

/// <summary>A 2D anchor point (equivalent to Flutter's 9-point <c>Alignment</c>). Used for non-positioned children of <see cref="LayoutKind.Stack"/>.</summary>
public enum Alignment : byte
{
    TopLeft,
    TopCenter,
    TopRight,
    CenterLeft,
    Center,
    CenterRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

/// <summary>
/// The layout style for a widget (a Flexbox subset using Flutter-compatible terminology).
/// A value type only (no allocations); <c>default</c> gives sensible defaults: Horizontal direction, Start
/// alignment, non-flex, Auto dimensions, and <see cref="MainAxisSize.Max"/>.
/// </summary>
public readonly struct Style
{
    public Axis Direction { get; init; }
    public MainAxisAlignment MainAxisAlignment { get; init; }
    public CrossAxisAlignment CrossAxisAlignment { get; init; }
    public MainAxisSize MainAxisSize { get; init; }

    /// <summary>The layout method (default is <see cref="LayoutKind.Flex"/>). Use <see cref="LayoutKind.Stack"/> for layered/overlapping arrangement.</summary>
    public LayoutKind Kind { get; init; }

    /// <summary>Alignment for non-positioned children within a <see cref="LayoutKind.Stack"/> (default <see cref="Alignment.TopLeft"/>).</summary>
    public Alignment StackAlignment { get; init; }

    /// <summary>For a <see cref="LayoutKind.Stack"/>, expands non-positioned children to fill the parent (equivalent to Flutter's <c>StackFit.expand</c>).</summary>
    public bool StackExpandChildren { get; init; }

    /// <summary>
    /// For a <see cref="LayoutKind.Box"/>, aligns the child within it (equivalent to Flutter's <c>Align</c>/<c>Center</c>/<c>Container.alignment</c>).
    /// When specified: the box expands to fill the available size where possible, and the child is measured loosely
    /// and placed at this anchor. When unspecified (null): the child is measured tight (filling), and the box
    /// shrinks to the child's size plus padding (equivalent to a proxy/padding widget).
    /// </summary>
    public Alignment? ChildAlign { get; init; }

    /// <summary>Whether this is an absolutely positioned child within a parent <see cref="LayoutKind.Stack"/> (equivalent to <c>Positioned</c>).</summary>
    public bool Positioned { get; init; }

    /// <summary>Absolutely positioned left/top/right/bottom inset (Auto = unspecified). Used when <see cref="Positioned"/> is true.</summary>
    public Dimension Left { get; init; }

    public Dimension Top { get; init; }

    public Dimension Right { get; init; }

    public Dimension Bottom { get; init; }

    /// <summary>Fixed spacing between children (equivalent to Flutter 3.27's <c>Flex.spacing</c>). For <see cref="LayoutKind.Wrap"/>, this is the spacing along the main axis.</summary>
    public float Spacing { get; init; }

    /// <summary>For <see cref="LayoutKind.Wrap"/>, the spacing between rows (cross-axis spacing; equivalent to Flutter's <c>Wrap.runSpacing</c>).</summary>
    public float RunSpacing { get; init; }

    /// <summary>Share of extra main-axis space to occupy (0 = no stretch). Set via <c>Expanded</c>/<c>Flexible</c>.</summary>
    public float FlexGrow { get; init; }

    /// <summary>Shrink factor applied when there isn't enough main-axis space (0 = no shrinking).</summary>
    public float FlexShrink { get; init; }

    /// <summary>Reference main-axis size before growing/shrinking (Auto = derived from content or the main-axis dimension).</summary>
    public Dimension FlexBasis { get; init; }

    public EdgeInsets Margin { get; init; }
    public EdgeInsets Padding { get; init; }

    /// <summary>
    /// For a <see cref="LayoutKind.Box"/>, the aspect ratio (width/height, 0 = disabled).
    /// The box determines its own size from this ratio, then measures its child tight to that size (with padding
    /// deflated) — equivalent to Flutter's <c>AspectRatio</c>.
    /// </summary>
    public float AspectRatio { get; init; }

    public Dimension Width { get; init; }
    public Dimension Height { get; init; }
    public Dimension MinWidth { get; init; }
    public Dimension MaxWidth { get; init; }
    public Dimension MinHeight { get; init; }
    public Dimension MaxHeight { get; init; }
}
