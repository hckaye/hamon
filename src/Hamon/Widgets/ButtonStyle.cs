using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Border color and thickness (equivalent to Flutter's <c>BorderSide</c>). Resolved by state via <see cref="ButtonStyle.Side"/>.</summary>
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
/// Button state animation settings (equivalent to Flutter's <c>ButtonStyle.animationDuration</c> and related
/// properties, plus extensions). Specifies the scale at press/hover/focus and the tracking speed of the state layer.
/// </summary>
public sealed class ButtonAnimationStyle
{
    /// <summary>State layer/scale tracking speed in 1/sec (larger values track faster). Used for exponential tracking when <see cref="Curve"/> is unspecified.</summary>
    public float Rate { get; init; } = 18f;

    /// <summary>
    /// Easing curve for state transitions. If specified, the transition interpolates from the previous value
    /// to the target value over <see cref="Duration"/> seconds using this curve. If unspecified (null), the
    /// transition instead uses exponential tracking driven by <see cref="Rate"/>.
    /// </summary>
    public Curve? Curve { get; init; }

    /// <summary>Transition time in seconds, used when <see cref="Curve"/> is specified.</summary>
    public float Duration { get; init; } = 0.15f;

    /// <summary>Scale when pressed (1 = no change; e.g. 0.96 for a slight press-in effect).</summary>
    public float PressedScale { get; init; } = 1f;

    /// <summary>Scale when hovering.</summary>
    public float HoveredScale { get; init; } = 1f;

    /// <summary>Scale at focus.</summary>
    public float FocusedScale { get; init; } = 1f;

    /// <summary>Opacity when pressed (1 = no change; e.g. 0.9 for a slight dimming).</summary>
    public float PressedOpacity { get; init; } = 1f;

    /// <summary>Opacity when hovering.</summary>
    public float HoveredOpacity { get; init; } = 1f;

    /// <summary>Opacity at focus.</summary>
    public float FocusedOpacity { get; init; } = 1f;
}

/// <summary>
/// Button appearance by state (equivalent to Flutter's <c>ButtonStyle</c>). Each property is a
/// <see cref="WidgetStateProperty{T}"/> that resolves according to state (hover/focus/pressed/disabled/selected).
/// <para>For simple generation, see <see cref="Button.StyleFrom"/>. For full custom appearance, see
/// <see cref="Button.Builder"/> (a Hamon extension).</para>
/// </summary>
public sealed class ButtonStyle
{
    /// <summary>Background color (by state).</summary>
    public WidgetStateProperty<Color?>? BackgroundColor { get; init; }

    /// <summary>
    /// Background image skin (by state; supports 9-slice/sprite).
    /// When specified, this image is drawn instead of a solid color, assuming the sprite itself represents the state.
    /// </summary>
    public WidgetStateProperty<ImageSkin?>? BackgroundImage { get; init; }

    /// <summary>Foreground color (text/icon). <see cref="Button"/> supplies this as the child's default font color via <see cref="BuildContext"/>.</summary>
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
