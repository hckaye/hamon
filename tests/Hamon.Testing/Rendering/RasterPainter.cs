using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.Testing.Rendering;

/// <summary>
/// <see cref="IPainter"/>in a pure managed manner<see cref="RasterCanvas"/>(real pixel drawing without GPU).
/// <see cref="PaintContext"/>Since the function folds the transformation/opacity/rotation and calls it with a rectangle/color in the device space, here we use the raw primitive.
/// All you have to do is rasterize it correctly.
/// Line segments are drawn as capsules (round ends), so the "gap between the joints" of the core implementation, which draws arcs with segments + round joints, is
/// Appears as is in the pixel =<c>AssertStrokeContinuity</c>can detect seam bugs.
/// </summary>
public sealed class RasterPainter : IPainter
{
    private readonly RasterCanvas _canvas;
    private bool _additive;

    public RasterPainter(RasterCanvas canvas) => _canvas = canvas;

    public void BeginFrame()
    {
    }

    public void EndFrame()
    {
    }

    public void FillRect(Rect rect, Color color) => _canvas.FillRect(rect, color, _additive);

    public void FillRoundedRect(Rect rect, Color color, float radius)
    {
        // 整数ピクセル境界で「重なりも隙間もなく」敷き詰める：中央帯（全高）・左右帯（隅の間）・四隅。MonoGamePainter と同方式。
        // 領域が 1px でも重なると、半透明色のとき重複部が二重ブレンドして縦/横線に見えるのを防ぐ（角丸の縦シーム退行を固定）。
        int x0 = (int)MathF.Floor(rect.X);
        int y0 = (int)MathF.Floor(rect.Y);
        int x1 = (int)MathF.Ceiling(rect.Right);
        int y1 = (int)MathF.Ceiling(rect.Bottom);
        int w = x1 - x0;
        int h = y1 - y0;
        int r = (int)Math.Clamp(MathF.Round(radius), 0f, Math.Min(w, h) / 2f);
        if (r <= 0 || w <= 0 || h <= 0)
        {
            _canvas.FillRect(rect, color, _additive);
            return;
        }

        _canvas.FillRect(new Rect(x0 + r, y0, w - (2 * r), h), color, _additive);     // 中央帯（全高）
        _canvas.FillRect(new Rect(x0, y0 + r, r, h - (2 * r)), color, _additive);     // 左帯（隅の間）
        _canvas.FillRect(new Rect(x1 - r, y0 + r, r, h - (2 * r)), color, _additive); // 右帯（隅の間）

        Corner(x0, y0, x0 + r, y0 + r, x0 + r, y0 + r, r, color);             // 左上
        Corner(x1 - r, y0, x1, y0 + r, x1 - r, y0 + r, r, color);             // 右上
        Corner(x0, y1 - r, x0 + r, y1, x0 + r, y1 - r, r, color);             // 左下
        Corner(x1 - r, y1 - r, x1, y1, x1 - r, y1 - r, r, color);             // 右下
    }

    public void FillCircle(Vec2 center, float radius, Color color)
    {
        if (radius <= 0f)
        {
            return;
        }

        int x0 = (int)MathF.Floor(center.X - radius);
        int y0 = (int)MathF.Floor(center.Y - radius);
        int x1 = (int)MathF.Ceiling(center.X + radius);
        int y1 = (int)MathF.Ceiling(center.Y + radius);
        float r2 = radius * radius;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                float dx = (x + 0.5f) - center.X;
                float dy = (y + 0.5f) - center.Y;
                if ((dx * dx) + (dy * dy) <= r2)
                {
                    _canvas.BlendPixel(x, y, color, 255, _additive);
                }
            }
        }
    }

    public void DrawLine(Vec2 a, Vec2 b, float thickness, Color color)
    {
        float half = MathF.Max(thickness, 1f) * 0.5f;
        int x0 = (int)MathF.Floor(MathF.Min(a.X, b.X) - half);
        int y0 = (int)MathF.Floor(MathF.Min(a.Y, b.Y) - half);
        int x1 = (int)MathF.Ceiling(MathF.Max(a.X, b.X) + half);
        int y1 = (int)MathF.Ceiling(MathF.Max(a.Y, b.Y) + half);
        float h2 = half * half;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (DistanceSqToSegment(x + 0.5f, y + 0.5f, a, b) <= h2)
                {
                    _canvas.BlendPixel(x, y, color, 255, _additive);
                }
            }
        }
    }

    public void FillGradient(Rect rect, Color a, Color b, GradientAxis axis)
    {
        int x0 = (int)MathF.Round(rect.X);
        int y0 = (int)MathF.Round(rect.Y);
        int x1 = (int)MathF.Round(rect.Right);
        int y1 = (int)MathF.Round(rect.Bottom);
        bool vertical = axis == GradientAxis.Vertical;
        float span = MathF.Max(1f, vertical ? rect.Height : rect.Width);
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                float t = vertical ? (y + 0.5f - rect.Y) / span : (x + 0.5f - rect.X) / span;
                _canvas.BlendPixel(x, y, Color.Lerp(a, b, Math.Clamp(t, 0f, 1f)), 255, _additive);
            }
        }
    }

    /// <summary>Soft shadows that smoothly attenuate outward with distance from the rectangle (no hard dark rings at the edges = standard image for hazy regression).</summary>
    public void FillShadow(Rect rect, Color color, float radius, float blur)
    {
        float b = MathF.Max(blur, 1f);
        int x0 = (int)MathF.Floor(rect.X - b);
        int y0 = (int)MathF.Floor(rect.Y - b);
        int x1 = (int)MathF.Ceiling(rect.Right + b);
        int y1 = (int)MathF.Ceiling(rect.Bottom + b);
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                float dx = MathF.Max(MathF.Max(rect.X - (x + 0.5f), (x + 0.5f) - rect.Right), 0f);
                float dy = MathF.Max(MathF.Max(rect.Y - (y + 0.5f), (y + 0.5f) - rect.Bottom), 0f);
                float d = MathF.Sqrt((dx * dx) + (dy * dy));
                float fall = 1f - Math.Clamp(d / b, 0f, 1f);
                int cov = (int)(fall * fall * 255f); // smooth な減衰
                _canvas.BlendPixel(x, y, color, cov, _additive);
            }
        }
    }

    public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint) =>
        _canvas.FillRect(dest, tint, _additive); // テスト用途：テクスチャは tint のベタ塗りで代用（グリフは別経路）

    public object? PushClip(Rect rect) => _canvas.PushClip(rect);

    public void PopClip(object? token) => _canvas.PopClip(token);

    public object? PushBlend(BlendMode mode)
    {
        bool prev = _additive;
        _additive = mode == BlendMode.Additive;
        return prev;
    }

    public void PopBlend(object? token) => _additive = token is bool b && b;

    // 隅の r×r ボックス [bx0,bx1)×[by0,by1) を円中心 (cx,cy)・半径 r で丸める（中央/左右帯と重ならない整数ボックス）。
    private void Corner(int bx0, int by0, int bx1, int by1, float cx, float cy, float r, Color color)
    {
        float r2 = r * r;
        for (int y = by0; y < by1; y++)
        {
            for (int x = bx0; x < bx1; x++)
            {
                float dx = (x + 0.5f) - cx;
                float dy = (y + 0.5f) - cy;
                if ((dx * dx) + (dy * dy) <= r2)
                {
                    _canvas.BlendPixel(x, y, color, 255, _additive);
                }
            }
        }
    }

    private static float DistanceSqToSegment(float px, float py, Vec2 a, Vec2 b)
    {
        float vx = b.X - a.X;
        float vy = b.Y - a.Y;
        float wx = px - a.X;
        float wy = py - a.Y;
        float len2 = (vx * vx) + (vy * vy);
        float t = len2 <= 0f ? 0f : Math.Clamp(((wx * vx) + (wy * vy)) / len2, 0f, 1f);
        float dx = px - (a.X + (t * vx));
        float dy = py - (a.Y + (t * vy));
        return (dx * dx) + (dy * dy);
    }
}
