using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Occupies the extra main-axis space within a <see cref="Row"/>/<see cref="Column"/> and stretches the child
/// to fill that area (equivalent to Flutter's <c>Expanded</c>). If there are multiple such widgets, the extra
/// space is distributed according to their <see cref="Flex"/> ratio.
/// </summary>
public sealed class Expanded : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    /// <summary>Share of the extra space to occupy, relative to siblings (default 1).</summary>
    public int Flex { get; init; } = 1;

    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Stack,
        StackExpandChildren = true, // 割り当てられた領域いっぱいに子を広げる（tight fit）
        FlexGrow = Flex,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>
/// A "loose" version of <see cref="Expanded"/> (equivalent to Flutter's <c>Flexible(fit: loose)</c>). Space is
/// still allocated in proportion to <see cref="Flex"/>, but if the child is smaller than its allocation, it
/// remains at its natural size (it does not stretch to fill the area).
/// </summary>
public sealed class Flexible : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public int Flex { get; init; } = 1;

    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Stack,
        StackExpandChildren = false, // 子は自然サイズ（割り当ての範囲内）
        FlexGrow = Flex,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>Inserts a stretchy space within a <see cref="Row"/>/<see cref="Column"/> (equivalent to Flutter's <c>Spacer</c>). Occupies extra space in proportion to <see cref="Flex"/>.</summary>
public sealed class Spacer : Widget, IRenderConfig
{
    public int Flex { get; init; } = 1;

    Style IRenderConfig.Style => new() { FlexGrow = Flex };

    IReadOnlyList<Widget>? IRenderConfig.Children => null;
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}
