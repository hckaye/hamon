using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Border color and thickness (Flutter<c>BorderSide</c>equivalent).<see cref="ButtonStyle.Side"/>Solve the problem by state.</summary>
public readonly struct BorderSide
{
    public BorderSide(Color color, float width)
    {
        Color = color;
        Width = width;
    }

    public Color Color { get; }

    public float Width { get; }
}

/// <summary>
/// Button state animation settings (Flutter<c>ButtonStyle.animationDuration</c>etc. + expansion).
/// Specify the scale at press/hover/focus and the tracking speed of the state layer.
/// </summary>
public sealed class ButtonAnimationStyle
{
    /// <summary>State layer/scale tracking speed (1/sec, the larger the speed, the faster it is).<see cref="Curve"/>Used for exponential tracking when unspecified.</summary>
    public float Rate { get; init; } = 18f;

    /// <summary>
    /// Easing curve of state transition (if specified, the easing curve will be<see cref="Duration"/>curve interpolation from→to in seconds).
    /// If unspecified (null)<see cref="Rate"/>exponential tracking.
    /// </summary>
    public Curve? Curve { get; init; }

    /// <summary><see cref="Curve"/>Transition time (seconds) at specified time.</summary>
    public float Duration { get; init; } = 0.15f;

    /// <summary>Scale when pressed (1 = no change, slightly depressed at 0.96 etc.).</summary>
    public float PressedScale { get; init; } = 1f;

    /// <summary>Scale when hovering.</summary>
    public float HoveredScale { get; init; } = 1f;

    /// <summary>Scale at focus.</summary>
    public float FocusedScale { get; init; } = 1f;

    /// <summary>Opacity when pressed (1=no change; 0.9 etc. slightly sinks).</summary>
    public float PressedOpacity { get; init; } = 1f;

    /// <summary>Opacity when hovering.</summary>
    public float HoveredOpacity { get; init; } = 1f;

    /// <summary>Opacity at focus.</summary>
    public float FocusedOpacity { get; init; } = 1f;
}

/// <summary>
/// Button appearance by state (Flutter<c>ButtonStyle</c>equivalent). <see cref="WidgetStateProperty{T}"/>in
/// Resolves according to state (hover/focus/pressed/disabled/selected).
/// <para>Simple generation is<see cref="Button.StyleFrom"/>. <see cref="Button.Builder"/>(Hamon extension).</para>
/// </summary>
public sealed class ButtonStyle
{
    /// <summary>Background color (by state).</summary>
    public WidgetStateProperty<Color?>? BackgroundColor { get; init; }

    /// <summary>
    /// Background image skin (by state/9-slice/sprite).
    /// Draw this image instead (assuming the sprite itself represents the state).
    /// </summary>
    public WidgetStateProperty<ImageSkin?>? BackgroundImage { get; init; }

    /// <summary>Foreground color (text/icon).<see cref="Button"/>but<see cref="BuildContext"/>Supplies the child's default font color via.</summary>
    public WidgetStateProperty<Color?>? ForegroundColor { get; init; }

    /// <summary>Color of state layer (hover/focus/pressed layer). </summary>
    public WidgetStateProperty<Color?>? OverlayColor { get; init; }

    /// <summary>Borders (by state).</summary>
    public WidgetStateProperty<BorderSide?>? Side { get; init; }

    /// <summary>Corner radius (by condition).</summary>
    public WidgetStateProperty<float?>? Radius { get; init; }

    /// <summary>Inner margin (state specific; layout resolved in base state).</summary>
    public WidgetStateProperty<EdgeInsets?>? Padding { get; init; }

    /// <summary>Mouse cursor (by state).</summary>
    public WidgetStateProperty<MouseCursor?>? MouseCursor { get; init; }

    /// <summary>Minimum size (px/lower limit of layout).</summary>
    public Size? MinimumSize { get; init; }

    /// <summary>Fixed size (px, if specified, also serves as min/max).</summary>
    public Size? FixedSize { get; init; }

    /// <summary>Maximum size (px/layout limit).</summary>
    public Size? MaximumSize { get; init; }

    /// <summary>Animation settings (if unspecified, theme default tracking speed).</summary>
    public ButtonAnimationStyle? Animation { get; init; }
}
