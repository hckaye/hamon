using Hamon.Layout;

namespace Hamon;

/// <summary>
/// Abstraction of text measurement and drawing.
/// Handle text through this interface without relying on it.
/// </summary>
public interface ITextRenderer
{
    /// <summary>Text dimensions (px) at specified pixel size. </summary>
    Vec2 Measure(string text, float pixelSize);

    /// <summary>Draw text (position in device space, color in final color).</summary>
    void Draw(string text, Vec2 position, float pixelSize, Color color);

    /// <summary>
    /// Measure by specifying the font name.<paramref name="font"/>is a font name registered in the implementation (unregistered/null = default font).
    /// The default implementation ignores font specifications (this is sufficient for single font implementations).
    /// </summary>
    Vec2 Measure(string text, float pixelSize, string? font) => Measure(text, pixelSize);

    /// <summary>Draw by specifying the font name (<paramref name="font"/>The terms of<see cref="Measure(string,float,string?)"/>).</summary>
    void Draw(string text, Vec2 position, float pixelSize, Color color, string? font) => Draw(text, position, pixelSize, color);
}
