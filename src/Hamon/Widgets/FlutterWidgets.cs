using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Arrange children horizontally (Flutter<c>Row</c>）。</summary>
public sealed class Row : Widget, IRenderConfig
{
    public IReadOnlyList<Widget> Children { get; init; } = Array.Empty<Widget>();
    public MainAxisAlignment MainAxisAlignment { get; init; }
    public CrossAxisAlignment CrossAxisAlignment { get; init; }
    public MainAxisSize MainAxisSize { get; init; }

    /// <summary>Child spacing (equivalent to Flex.spacing in Flutter 3.27).</summary>
    public float Spacing { get; init; }

    Style IRenderConfig.Style => new()
    {
        Direction = Axis.Horizontal,
        MainAxisAlignment = MainAxisAlignment,
        CrossAxisAlignment = CrossAxisAlignment,
        MainAxisSize = MainAxisSize,
        Spacing = Spacing,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Children;
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>Arrange children vertically (Flutter<c>Column</c>）。</summary>
public sealed class Column : Widget, IRenderConfig
{
    public IReadOnlyList<Widget> Children { get; init; } = Array.Empty<Widget>();
    public MainAxisAlignment MainAxisAlignment { get; init; }
    public CrossAxisAlignment CrossAxisAlignment { get; init; }
    public MainAxisSize MainAxisSize { get; init; }
    public float Spacing { get; init; }

    Style IRenderConfig.Style => new()
    {
        Direction = Axis.Vertical,
        MainAxisAlignment = MainAxisAlignment,
        CrossAxisAlignment = CrossAxisAlignment,
        MainAxisSize = MainAxisSize,
        Spacing = Spacing,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Children;
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>Single child container with margins, background color, and size (Flutter<c>Container</c>).</summary>
public sealed class Container : Widget, IRenderConfig
{
    public Widget? Child { get; init; }
    public EdgeInsets Padding { get; init; }
    public EdgeInsets Margin { get; init; }
    public Color? Color { get; init; }
    public Dimension Width { get; init; }
    public Dimension Height { get; init; }

    /// <summary>Background corner radius (px, default 0 = rectangle).</summary>
    public float Radius { get; init; }

    /// <summary>Child alignment (Flutter<c>Container.alignment</c>). </summary>
    public Alignment? Alignment { get; init; }

    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Box,
        Padding = Padding,
        Margin = Margin,
        // Flutter: alignment 指定時は有界なら充填（未指定寸法を 100% に）。
        Width = Alignment is not null && Width.IsAuto ? Dimension.Percent(100f) : Width,
        Height = Alignment is not null && Height.IsAuto ? Dimension.Percent(100f) : Height,
        ChildAlign = Alignment,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => Color;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;
    float IRenderConfig.Radius => Radius;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>Add inner margin to child (Flutter<c>Padding</c>）。</summary>
public sealed class Padding : Widget, IRenderConfig
{
    public EdgeInsets Insets { get; init; }
    public Widget? Child { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Box, Padding = Insets };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>Move children to specified anchor (Flutter<c>Align</c>). </summary>
public sealed class Align : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public Alignment Alignment { get; init; } = Layout.Alignment.Center;

    // 有界なら充填（Width/Height=100%）し、その中で子をアンカー配置。
    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Box,
        Width = Dimension.Percent(100f),
        Height = Dimension.Percent(100f),
        ChildAlign = Alignment,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>Align children to center (Flutter<c>Center</c> ＝ <see cref="Align"/>center). </summary>
public sealed class Center : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Box,
        Width = Dimension.Percent(100f),
        Height = Dimension.Percent(100f),
        ChildAlign = Layout.Alignment.Center,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>Fixed size box (Flutter<c>SizedBox</c>). </summary>
public sealed class SizedBox : Widget, IRenderConfig
{
    public Dimension Width { get; init; }
    public Dimension Height { get; init; }
    public Widget? Child { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Box, Width = Width, Height = Height };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}
