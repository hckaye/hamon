using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Two-color gradient direction (<see cref="IPainter.FillGradient"/>）。</summary>
public enum GradientAxis : byte
{
    /// <summary>Top → bottom (a=top, b=bottom).</summary>
    Vertical,

    /// <summary>Left → right (a=left, b=right).</summary>
    Horizontal,
}

/// <summary>Multi-stage gradation color stopping point (<see cref="PaintContext.FillGradientStops"/>）。<see cref="Position"/>is 0..1.</summary>
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

/// <summary>Composite mode (<see cref="IPainter.PushBlend"/>）。</summary>
public enum BlendMode : byte
{
    /// <summary>Regular alpha compositing (default).</summary>
    Normal,

    /// <summary>Additive synthesis (to the glow of light/flame/magic effects. The more they overlap, the brighter it becomes).</summary>
    Additive,
}

/// <summary>
/// Abstract drawing backend (implemented in MonoGame/Godot/Unity, etc.). <b>device space</b>(Conversion applied) - Color is final color
/// Passed (transform/opacity composition is<see cref="PaintContext"/>It can be done on the side = the back end is only bare primitives).
/// Rounded corners depend on the engine<see cref="FillRoundedRect"/>also has as a primitive.
///
/// Rich drawing primitives (<see cref="DrawLine"/>/<see cref="FillCircle"/>/<see cref="FillGradient"/>/<see cref="FillShadow"/>/
/// rotation drawing) is<b>With default implementation</b>= Existing/simple backend will "appropriately deteriorate" without modification (circle → rounded corners/gradation → intermediate color/shadow → no drawing/
/// rotation → ignored).
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

    /// <summary>Stack rectangular clips. <see cref="PopClip"/>).</summary>
    object? PushClip(Rect rect);

    void PopClip(object? token);

    /// <summary>Thickness<paramref name="thickness"/>line segment<paramref name="a"/>→<paramref name="b"/>draw </summary>
    void DrawLine(Vec2 a, Vec2 b, float thickness, Color color)
    {
        float half = thickness * 0.5f;
        float minX = MathF.Min(a.X, b.X) - half;
        float minY = MathF.Min(a.Y, b.Y) - half;
        float w = MathF.Abs(b.X - a.X) + thickness;
        float h = MathF.Abs(b.Y - a.Y) + thickness;
        FillRect(new Rect(minX, minY, w, h), color);
    }

    /// <summary>center<paramref name="center"/>·radius<paramref name="radius"/>filled circle. </summary>
    void FillCircle(Vec2 center, float radius, Color color) =>
        FillRoundedRect(new Rect(center.X - radius, center.Y - radius, radius * 2f, radius * 2f), color, radius);

    /// <summary>a rectangle<paramref name="a"/>→<paramref name="b"/>Paint with a two-color gradation. </summary>
    void FillGradient(Rect rect, Color a, Color b, GradientAxis axis) =>
        FillRect(rect, Color.Lerp(a, b, 0.5f));

    /// <summary>A soft shadow (elevation) cast behind the rectangle.<paramref name="blur"/>is the width that bleeds outward. </summary>
    void FillShadow(Rect rect, Color color, float radius, float blur)
    {
    }

    /// <summary><paramref name="pivot"/>(device space)<paramref name="radians"/>Rotated filled rectangle. </summary>
    void FillRectRotated(Rect rect, Color color, float radians, Vec2 pivot) => FillRect(rect, color);

    /// <summary><paramref name="pivot"/>(device space)<paramref name="radians"/>Rotated texture drawing. </summary>
    void DrawTextureRotated(ITexture texture, Rect dest, RectInt source, Color tint, float radians, Vec2 pivot) =>
        DrawTexture(texture, dest, source, tint);

    /// <summary>Loads the composition mode (applies to subsequent drawings). <see cref="PopBlend"/>fart). </summary>
    object? PushBlend(BlendMode mode) => null;

    /// <summary>Return to compositing mode. </summary>
    void PopBlend(object? token)
    {
    }
}

/// <summary>The texture's opaque handle (backend is<see cref="Width"/>/<see cref="Height"/>).</summary>
public interface ITexture
{
    int Width { get; }

    int Height { get; }
}
