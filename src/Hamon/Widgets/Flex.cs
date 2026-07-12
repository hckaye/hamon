using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// <see cref="Row"/>/<see cref="Column"/>**Occupy extra main axis** within Flutter and spread the children to fill that area (Flutter<c>Expanded</c>）。
/// If there are multiple<see cref="Flex"/>Distribute by ratio.
/// </summary>
public sealed class Expanded : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    /// <summary>Surplus share (default 1).</summary>
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
/// <see cref="Expanded"/>A loose version of Flutter<c>Flexible(fit: loose)</c>). <see cref="Flex"/>in comparison
/// It is set aside, but if the child is less than the allocation, it remains at its natural size (does not expand to fill the area).
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

/// <summary><see cref="Row"/>/<see cref="Column"/>Insert a space that stretches (Flutter<c>Spacer</c>）。<see cref="Flex"/>Occupy surplus in ratio.</summary>
public sealed class Spacer : Widget, IRenderConfig
{
    public int Flex { get; init; } = 1;

    Style IRenderConfig.Style => new() { FlexGrow = Flex };

    IReadOnlyList<Widget>? IRenderConfig.Children => null;
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}
