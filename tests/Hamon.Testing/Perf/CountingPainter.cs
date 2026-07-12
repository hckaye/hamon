using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.Testing.Perf;

/// <summary>
/// No drawing,<see cref="IPainter"/>Each primitive call of<b>number of times</b>A stub backend that only records
/// ("API call count record" backend).
/// Detect spikes in draw calls.
/// </summary>
public sealed class CountingPainter : IPainter
{
    public int FillRectCalls { get; private set; }

    public int FillRoundedRectCalls { get; private set; }

    public int DrawTextureCalls { get; private set; }

    public int DrawLineCalls { get; private set; }

    public int FillCircleCalls { get; private set; }

    public int FillGradientCalls { get; private set; }

    public int FillShadowCalls { get; private set; }

    public int ClipCalls { get; private set; }

    public int BlendCalls { get; private set; }

    /// <summary>Total number of drawing primitives in the relevant frame (total of fill/drawing primitives excluding clip/blend).</summary>
    public int TotalDrawCalls =>
        FillRectCalls + FillRoundedRectCalls + DrawTextureCalls + DrawLineCalls +
        FillCircleCalls + FillGradientCalls + FillShadowCalls;

    public void Reset()
    {
        FillRectCalls = FillRoundedRectCalls = DrawTextureCalls = DrawLineCalls = 0;
        FillCircleCalls = FillGradientCalls = FillShadowCalls = ClipCalls = BlendCalls = 0;
    }

    public void BeginFrame()
    {
    }

    public void EndFrame()
    {
    }

    public void FillRect(Rect rect, Color color) => FillRectCalls++;

    public void FillRoundedRect(Rect rect, Color color, float radius) => FillRoundedRectCalls++;

    public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint) => DrawTextureCalls++;

    public void DrawLine(Vec2 a, Vec2 b, float thickness, Color color) => DrawLineCalls++;

    public void FillCircle(Vec2 center, float radius, Color color) => FillCircleCalls++;

    public void FillGradient(Rect rect, Color a, Color b, GradientAxis axis) => FillGradientCalls++;

    public void FillShadow(Rect rect, Color color, float radius, float blur) => FillShadowCalls++;

    public void FillRectRotated(Rect rect, Color color, float radians, Vec2 pivot) => FillRectCalls++;

    public void DrawTextureRotated(ITexture texture, Rect dest, RectInt source, Color tint, float radians, Vec2 pivot) => DrawTextureCalls++;

    public object? PushClip(Rect rect)
    {
        ClipCalls++;
        return null;
    }

    public void PopClip(object? token)
    {
    }

    public object? PushBlend(BlendMode mode)
    {
        BlendCalls++;
        return null;
    }

    public void PopBlend(object? token)
    {
    }
}
