using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Direction of a two-color gradient (used by <see cref="IPainter.FillGradient"/>).</summary>
public enum GradientAxis : byte
{
    /// <summary>Top → bottom (a=top, b=bottom).</summary>
    Vertical,

    /// <summary>Left → right (a=left, b=right).</summary>
    Horizontal,
}

/// <summary>A stop in a multi-stage gradient (used by <see cref="PaintContext.FillGradientStops"/>). <see cref="Position"/> is in the range 0..1.</summary>
public readonly struct GradientStop
{
    public GradientStop(float position, Color color)
    {
        Position = position;
        Color = color;
    }

    public float Position { get; }

    public Color Color { get; }
}

/// <summary>Compositing mode (used by <see cref="IPainter.PushBlend"/>).</summary>
public enum BlendMode : byte
{
    /// <summary>Regular alpha compositing (default).</summary>
    Normal,

    /// <summary>Additive compositing, for glow effects such as light, flame, or magic — the more layers overlap, the brighter the result.</summary>
    Additive,
}

/// <summary>
/// An abstract drawing backend (implemented for MonoGame, Godot, Unity, etc.).
/// Coordinates are already in <b>device space</b> (the transform has been applied),
/// and colors are already final colors — transform and opacity compositing happens
/// on the <see cref="PaintContext"/> side, so the backend only needs to handle bare
/// primitives. Because rounded-corner rendering is engine-dependent,
/// <see cref="FillRoundedRect"/> is also exposed as a primitive.
///
/// The richer drawing primitives (<see cref="DrawLine"/>, <see cref="FillCircle"/>,
/// <see cref="FillGradient"/>, <see cref="FillShadow"/>, and rotated drawing) all
/// have <b>default implementations</b>, so an existing or minimal backend degrades
/// gracefully without any changes (circle → rounded rect, gradient → the midpoint
/// color, shadow → not drawn, rotation → ignored).
/// </summary>
public interface IPainter
{
    /// <summary>Start of frame drawing (batch start, initial clip settings, etc.).</summary>
    void BeginFrame();

    /// <summary>End of frame drawing (batch confirmation, etc.).</summary>
    void EndFrame();

    void FillRect(Rect rect, Color color);

    void FillRoundedRect(Rect rect, Color color, float radius);

    void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint);

    /// <summary>Pushes a rectangular clip onto the stack (release with <see cref="PopClip"/>).</summary>
    object? PushClip(Rect rect);

    void PopClip(object? token);

    /// <summary>Draws a line segment from <paramref name="a"/> to <paramref name="b"/> with the given <paramref name="thickness"/>.</summary>
    void DrawLine(Vec2 a, Vec2 b, float thickness, Color color)
    {
        float half = thickness * 0.5f;
        float minX = MathF.Min(a.X, b.X) - half;
        float minY = MathF.Min(a.Y, b.Y) - half;
        float w = MathF.Abs(b.X - a.X) + thickness;
        float h = MathF.Abs(b.Y - a.Y) + thickness;
        FillRect(new Rect(minX, minY, w, h), color);
    }

    /// <summary>Draws a filled circle centered at <paramref name="center"/> with the given <paramref name="radius"/>.</summary>
    void FillCircle(Vec2 center, float radius, Color color) =>
        FillRoundedRect(new Rect(center.X - radius, center.Y - radius, radius * 2f, radius * 2f), color, radius);

    /// <summary>Paints a rectangle with a two-color gradient from <paramref name="a"/> to <paramref name="b"/>.</summary>
    void FillGradient(Rect rect, Color a, Color b, GradientAxis axis) =>
        FillRect(rect, Color.Lerp(a, b, 0.5f));

    /// <summary>Casts a soft shadow (elevation) behind the rectangle. <paramref name="blur"/> is the width the shadow bleeds outward.</summary>
    void FillShadow(Rect rect, Color color, float radius, float blur)
    {
    }

    /// <summary>Draws a filled rectangle rotated by <paramref name="radians"/> around <paramref name="pivot"/> (in device space).</summary>
    void FillRectRotated(Rect rect, Color color, float radians, Vec2 pivot) => FillRect(rect, color);

    /// <summary>Draws a texture rotated by <paramref name="radians"/> around <paramref name="pivot"/> (in device space).</summary>
    void DrawTextureRotated(ITexture texture, Rect dest, RectInt source, Color tint, float radians, Vec2 pivot) =>
        DrawTexture(texture, dest, source, tint);

    /// <summary>Pushes a compositing mode that applies to subsequent draw calls (release with <see cref="PopBlend"/>).</summary>
    object? PushBlend(BlendMode mode) => null;

    /// <summary>Restores the previous compositing mode.</summary>
    void PopBlend(object? token)
    {
    }
}

/// <summary>An opaque handle to a texture; the backend at least exposes <see cref="Width"/> and <see cref="Height"/>.</summary>
public interface ITexture
{
    int Width { get; }

    int Height { get; }
}
