using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>How to expand non-locators (Flutter<c>StackFit</c>）。</summary>
public enum StackFit : byte
{
    /// <summary>Children are natural size (default). </summary>
    Loose,

    /// <summary>Spread non-placers to fill the Stack.</summary>
    Expand,
}

/// <summary>
/// Arranging children on top of each other (Flutter<c>Stack</c>). <see cref="Positioned"/>The child of
/// Absolute position with inset, otherwise<see cref="Alignment"/>Anchor with.
/// The default size shrinks to the largest non-locator (full available space under tight constraints, such as directly under the root).
/// </summary>
public sealed class Stack : Widget, IRenderConfig
{
    public IReadOnlyList<Widget>? Children { get; init; }

    /// <summary>Non-placer anchor (default<see cref="Alignment.TopLeft"/>）。</summary>
    public Alignment Alignment { get; init; }

    /// <summary>How to spread non-locators (default<see cref="StackFit.Loose"/>）。</summary>
    public StackFit Fit { get; init; }

    public Color? Background { get; init; }

    public Dimension Width { get; init; }

    public Dimension Height { get; init; }

    /// <summary>Background corner radius (px, default 0).</summary>
    public float Radius { get; init; }

    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Stack,
        StackAlignment = Alignment,
        StackExpandChildren = Fit == StackFit.Expand,
        Width = Width,
        Height = Height,
    };

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;
    float IRenderConfig.Radius => Radius;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>
/// <see cref="Stack"/>Absolute positioning of children within (Flutter<c>Positioned</c>）。<see cref="Left"/>/<see cref="Top"/>/
/// <see cref="Right"/>/<see cref="Bottom"/>with the inset of<see cref="Width"/>/<see cref="Height"/>determine the rectangle with
/// (Stretch by specifying both ends, fixed by specifying one end + size).
/// </summary>
public sealed class Positioned : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public Dimension Left { get; init; }

    public Dimension Top { get; init; }

    public Dimension Right { get; init; }

    public Dimension Bottom { get; init; }

    public Dimension Width { get; init; }

    public Dimension Height { get; init; }

    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Stack,
        StackExpandChildren = true, // 子を配置矩形いっぱいに広げる
        Positioned = true,
        Left = Left,
        Top = Top,
        Right = Right,
        Bottom = Bottom,
        Width = Width,
        Height = Height,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}
