using Hamon.Layout;

namespace Hamon.Testing.Perf;

/// <summary>
/// Deterministic text renderer for performance measurement (font independent, no GPU required). <c>text.Length * pixelSize</c>etc.
/// Returns a stable approximation, recording the number of Measure/Draw calls and total number of characters.
/// </summary>
public sealed class CountingTextRenderer : ITextRenderer
{
    public int MeasureCalls { get; private set; }

    public int DrawCalls { get; private set; }

    public long DrawnChars { get; private set; }

    public void Reset()
    {
        MeasureCalls = 0;
        DrawCalls = 0;
        DrawnChars = 0;
    }

    public Vec2 Measure(string text, float pixelSize)
    {
        MeasureCalls++;
        return new Vec2(text.Length * pixelSize * 0.5f, pixelSize);
    }

    public void Draw(string text, Vec2 position, float pixelSize, Color color)
    {
        DrawCalls++;
        DrawnChars += text.Length;
    }
}
