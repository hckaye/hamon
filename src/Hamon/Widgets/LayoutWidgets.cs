using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Fix child aspect ratio (Flutter<c>AspectRatio</c>). <see cref="Ratio"/>satisfy
/// Decide on a size and fill the child with that size.
/// </summary>
public sealed class AspectRatio : Widget, IRenderConfig
{
    /// <summary>Width ÷ height (e.g. 16/9).</summary>
    public float Ratio { get; init; } = 1f;

    public Widget? Child { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Box, AspectRatio = Ratio };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>
/// Folded flow (Flutter<c>Wrap</c>).
/// Used to insert a variable number of small elements, such as a list of tags/chips and a group of skill icons.
/// </summary>
public sealed class Wrap : Widget, IRenderConfig
{
    public IReadOnlyList<Widget> Children { get; init; } = Array.Empty<Widget>();

    /// <summary>Main axis direction (default = horizontal = wrap left to right side by side).</summary>
    public Axis Direction { get; init; } = Axis.Horizontal;

    /// <summary>Spacing of children along the main axis.</summary>
    public float Spacing { get; init; }

    /// <summary>Line spacing (cross-axis spacing).</summary>
    public float RunSpacing { get; init; }

    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Wrap,
        Direction = Direction,
        Spacing = Spacing,
        RunSpacing = RunSpacing,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Children;
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>
/// Positioning children to avoid notch/status bar/home indicator and soft keyboard (Flutter<c>SafeArea</c>）。
/// <see cref="HamonRoot.SafeAreaInsets"/>Give the child (+ bottom edge is the height of the soft keyboard) as the inner margin.
/// <see cref="Minimum"/>guarantees minimum margins.
/// </summary>
public sealed class SafeArea : StatelessWidget
{
    public Widget? Child { get; init; }

    public bool Top { get; init; } = true;

    public bool Bottom { get; init; } = true;

    public bool Left { get; init; } = true;

    public bool Right { get; init; } = true;

    /// <summary>Minimum margin for each side (this amount is reserved even if the safe inset is smaller than this).</summary>
    public EdgeInsets Minimum { get; init; }

    /// <summary>Should the height of the soft keyboard also be avoided at the bottom edge (default true)?</summary>
    public bool AvoidKeyboard { get; init; } = true;

    public override Widget Build(BuildContext context)
    {
        EdgeInsets insets = default;
        float keyboard = 0f;
        if (context.Owner is HamonRoot root)
        {
            insets = root.SafeAreaInsets;
            if (AvoidKeyboard)
            {
                keyboard = root.SoftKeyboardHeight;
            }
        }

        float left = Math.Max(Left ? insets.Left : 0f, Minimum.Left);
        float top = Math.Max(Top ? insets.Top : 0f, Minimum.Top);
        float right = Math.Max(Right ? insets.Right : 0f, Minimum.Right);
        float bottom = Math.Max(Bottom ? Math.Max(insets.Bottom, keyboard) : keyboard, Minimum.Bottom);

        return new Padding
        {
            Insets = new EdgeInsets(left, top, right, bottom),
            Child = Child,
        };
    }
}
