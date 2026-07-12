namespace Hamon.Layout;

/// <summary>Flex principal axis direction (Flutter<c>Axis</c>equivalent).</summary>
public enum Axis : byte
{
    Horizontal,
    Vertical,
}

/// <summary>Placement of children along the main axis (Flutter<c>MainAxisAlignment</c>）。</summary>
public enum MainAxisAlignment : byte
{
    Start,
    End,
    Center,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

/// <summary>Placement of children along the cross axis (Flutter<c>CrossAxisAlignment</c>). <see cref="Center"/>(Same as Flutter).</summary>
public enum CrossAxisAlignment : byte
{
    /// <summary>Center of cross axis (default = same as Flutter).</summary>
    Center,

    Start,

    End,

    /// <summary>Stretch the cross axis to the full extent of the parent (only for children whose cross axis size is Auto).</summary>
    Stretch,
}

/// <summary>Size in the main axis direction (Flutter<c>MainAxisSize</c>). <see cref="Max"/>。</summary>
public enum MainAxisSize : byte
{
    /// <summary>Full available spindles (default).</summary>
    Max,

    /// <summary>Shrinks to fit the content.</summary>
    Min,
}

/// <summary>Node layout method. <see cref="Flex"/>(Row/Column type).</summary>
public enum LayoutKind : byte
{
    /// <summary>Flexbox subset (main axis/cross axis flow).</summary>
    Flex,

    /// <summary>Overlapping children (Flutter<c>Stack</c>). <c>Positioned</c>Absolute placement.</summary>
    Stack,

    /// <summary>A viewport that scrolls along the main axis (<c>ScrollView</c>). </summary>
    Scroll,

    /// <summary>
    /// Single Child Box (Flutter<c>Container</c>/<c>Padding</c>/proxy equivalent).<b>Deflate the incoming constraints by padding
    /// pass it on to the child</b>(If it is tight, it will remain tight = the child will spread out).
    /// Clamp child + padding with constraints.
    /// </summary>
    Box,

    /// <summary>
    /// Folded flow (Flutter<c>Wrap</c>).
    /// Children are measured loosely (natural size).<see cref="Style.Spacing"/>= spindle spacing,<see cref="Style.RunSpacing"/>= between lines.
    /// </summary>
    Wrap,
}

/// <summary>2D Anchor (Flutter<c>Alignment</c>9 points).<see cref="LayoutKind.Stack"/>Used for non-locators.</summary>
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
/// Widget layout style (Flexbox subset/Flutter compliant vocabulary).
/// Value types only (no allocations).<c>default</c>is the straightforward default for Horizontal/Start/non-flex/Auto dimensions and MainAxisSize.Max.
/// </summary>
public readonly struct Style
{
    public Axis Direction { get; init; }
    public MainAxisAlignment MainAxisAlignment { get; init; }
    public CrossAxisAlignment CrossAxisAlignment { get; init; }
    public MainAxisSize MainAxisSize { get; init; }

    /// <summary>Layout method (default<see cref="LayoutKind.Flex"/>）。<see cref="LayoutKind.Stack"/>Layered arrangement.</summary>
    public LayoutKind Kind { get; init; }

    /// <summary><see cref="LayoutKind.Stack"/>(default<see cref="Alignment.TopLeft"/>）。</summary>
    public Alignment StackAlignment { get; init; }

    /// <summary><see cref="LayoutKind.Stack"/>Expand the non-placer to fill the parent (Flutter<c>StackFit.expand</c>equivalent).</summary>
    public bool StackExpandChildren { get; init; }

    /// <summary>
    /// <see cref="LayoutKind.Box"/>Align children with (Flutter<c>Align</c>/<c>Center</c>/<c>Container.alignment</c>equivalent).
    /// When specified: The box will expand to fill the available size if possible, and the children will be loosely measured and placed at this anchor.
    /// Unspecified (null): Measures the child tight (filling), and the box shrinks to the child + padding (equivalent to proxy/padding).
    /// </summary>
    public Alignment? ChildAlign { get; init; }

    /// <summary>parent<see cref="LayoutKind.Stack"/>Is it an absolutely positioned child within (<c>Positioned</c>）。</summary>
    public bool Positioned { get; init; }

    /// <summary>Absolutely positioned left/top/right/bottom inset (Auto=unspecified).<see cref="Positioned"/>sometimes used</summary>
    public Dimension Left { get; init; }

    public Dimension Top { get; init; }

    public Dimension Right { get; init; }

    public Dimension Bottom { get; init; }

    /// <summary>Fixed spacing between children (equivalent to Flex.spacing in Flutter 3.27).<see cref="LayoutKind.Wrap"/>Then, the spacing in the main axis direction.</summary>
    public float Spacing { get; init; }

    /// <summary><see cref="LayoutKind.Wrap"/>The row spacing (the spacing in the cross-axis direction. Flutter<c>Wrap.runSpacing</c>）。</summary>
    public float RunSpacing { get; init; }

    /// <summary>Extra spindle share (0 = no stretch). <c>Expanded</c>/<c>Flexible</c>Set with .</summary>
    public float FlexGrow { get; init; }

    /// <summary>Shrinkage factor for the missing principal axis (0 = no shrinkage).</summary>
    public float FlexShrink { get; init; }

    /// <summary>Reference size of spindle (Auto = from content or spindle dimensions).</summary>
    public Dimension FlexBasis { get; init; }

    public EdgeInsets Margin { get; init; }
    public EdgeInsets Padding { get; init; }

    /// <summary>
    /// <see cref="LayoutKind.Box"/>Aspect ratio (width/height, 0=disabled).
    /// Decide on yourself and measure your child using that size (tight with padding deflated) (Flutter<c>AspectRatio</c>equivalent).
    /// </summary>
    public float AspectRatio { get; init; }

    public Dimension Width { get; init; }
    public Dimension Height { get; init; }
    public Dimension MinWidth { get; init; }
    public Dimension MaxWidth { get; init; }
    public Dimension MinHeight { get; init; }
    public Dimension MaxHeight { get; init; }
}
