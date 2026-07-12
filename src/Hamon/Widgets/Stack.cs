using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>How non-positioned children are sized (equivalent to Flutter's <c>StackFit</c>).</summary>
public enum StackFit : byte
{
    /// <summary>Children keep their natural size (default).</summary>
    Loose,

    /// <summary>Non-positioned children are stretched to fill the Stack.</summary>
    Expand,
}

/// <summary>
/// Arranges children on top of each other (equivalent to Flutter's <c>Stack</c>). A child wrapped in
/// <see cref="Positioned"/> is placed absolutely using its insets; other children are anchored using
/// <see cref="Alignment"/>. By default, the Stack sizes itself to its largest non-positioned child (or fills the
/// available space under tight constraints, e.g. directly under the root).
/// </summary>
public sealed class Stack : Widget, IRenderConfig
{
    public IReadOnlyList<Widget>? Children { get; init; }

    /// <summary>Anchor for non-positioned children (default <see cref="Alignment.TopLeft"/>).</summary>
    public Alignment Alignment { get; init; }

    /// <summary>How non-positioned children are sized (default <see cref="StackFit.Loose"/>).</summary>
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
/// Absolute positioning of a child within a <see cref="Stack"/> (equivalent to Flutter's <c>Positioned</c>). The
/// rectangle is determined by the insets <see cref="Left"/> / <see cref="Top"/> / <see cref="Right"/> /
/// <see cref="Bottom"/> together with <see cref="Width"/> / <see cref="Height"/> (specify both ends to stretch, or
/// one end plus a size for a fixed rectangle).
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
