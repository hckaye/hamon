using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Appearance and behavior settings for the focus cursor (a single overlay), exposed via
/// <see cref="HamonRoot.Cursor"/>. When <see cref="Enabled"/>, instead of each widget drawing its own frame,
/// <b>a single cursor glides from the previous frame to the next</b> (see <see cref="GlideDuration"/>/
/// <see cref="Curve"/>) and is drawn in the foreground, optionally pulsing (see <see cref="PulseAmplitude"/>).
/// This makes the focus experience for gamepad/directional movement a first-class feature.
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

    /// <summary>Background color to be placed inside the frame (null = no fill, outline only).</summary>
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
/// Per-widget focus appearance for <see cref="Focus"/>, for widgets that do not use the global
/// <see cref="FocusCursor"/>. In addition to the border, you can also specify the background and rounded
/// corners.
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
