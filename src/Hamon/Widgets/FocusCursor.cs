using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Appearance and behavior settings for the focus cursor (single overlay).<see cref="HamonRoot.Cursor"/>Consists of.
/// <see cref="Enabled"/>, instead of each widget's immediate frame,<b>One cursor slides from the previous frame to the next</b>
/// （<see cref="GlideDuration"/>/<see cref="Curve"/>), drawn in the foreground. <see cref="PulseAmplitude"/>）。
/// To make the focus expression of gamepad/direction movement first class.
/// </summary>
public sealed class FocusCursor
{
    /// <summary>Enabled (default false = each widget draws a frame as before).</summary>
    public bool Enabled { get; set; }

    public Color Color { get; set; } = new(120, 180, 255);

    /// <summary>Border thickness (px).</summary>
    public float Thickness { get; set; } = 2f;

    /// <summary>Margin (px) to extend outside the focus rectangle.</summary>
    public float Padding { get; set; } = 3f;

    /// <summary>Number of seconds to slide from previous frame to next frame.</summary>
    public float GlideDuration { get; set; } = 0.12f;

    public Curve Curve { get; set; } = Curves.EaseOut;

    /// <summary>Blinking amplitude 0..1 (0 = no blinking, e.g. 0.4 = pulsating alpha between 60% and 100%).</summary>
    public float PulseAmplitude { get; set; }

    /// <summary>Blinking period (seconds).</summary>
    public float PulsePeriod { get; set; } = 1f;

    /// <summary>Background color to be placed inside the frame (null = no fill = frame line only). </summary>
    public Color? Background { get; set; }

    /// <summary>Background/frame corner radius (px).</summary>
    public float Radius { get; set; }

    /// <summary>Scale of the cursor rectangle (1 = same size; 1.05 etc. encloses one size larger than the focus target).</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>
    /// A delegate that completely replaces cursor drawing (equivalent to Flutter's focus highlight builder).
    /// Called instead of background + border drawing.
    /// </summary>
    public Action<PaintContext, Rect, float>? Renderer { get; set; }
}

/// <summary>
/// <see cref="Focus"/>Individual focus appearance (global<see cref="FocusCursor"/>(for widgets that do not use ).
/// In addition to the border, you can also specify the background and rounded corners.
/// </summary>
public readonly struct FocusDecoration
{
    public FocusDecoration(Color outline, float thickness = 2f, Color? background = null, float radius = 0f)
    {
        Outline = outline;
        Thickness = thickness;
        Background = background;
        Radius = radius;
    }

    public Color Outline { get; }

    public float Thickness { get; }

    public Color? Background { get; }

    public float Radius { get; }
}
