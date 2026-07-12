using Hamon.Layout;
using Hamon.Widgets;
using Microsoft.Xna.Framework.Graphics;
using Xna = Microsoft.Xna.Framework;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRect = Microsoft.Xna.Framework.Rectangle;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace Hamon.MonoGame;

/// <summary>
/// <see cref="IPainter"/> implementation for MonoGame, built on top of <see cref="SpriteBatch"/>.
/// Generates and maintains a white 1px texture and a circle texture used for rounded corners.
/// <see cref="Batch"/> and <see cref="Device"/> are exposed for <see cref="Hamon.Widgets.SceneView"/>
/// (the destination batch is the same <see cref="SpriteBatch"/> instance shared with the text layer,
/// <c>FontStashTextRenderer</c>).
/// </summary>
public sealed class MonoGamePainter : IPainter, IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _batch;
    private Texture2D _white;
    private Texture2D _corner;
    private Texture2D _shadow;
    private readonly RasterizerState _clip;
    private readonly Dictionary<(uint A, uint B, GradientAxis Axis), Texture2D> _gradCache = new(); // 2色グラデの 1×N ストリップをキャッシュ（通常の SpriteBatch 描画で済む＝バッチ分割なし）
    private readonly List<(XnaRect Previous, bool Flushed)> _clipStack = new(); // クリップ毎の前シザーと flush 有無（LIFO）
    private readonly List<BlendState> _blendStack = new();
    private BlendState _blend = BlendState.NonPremultiplied; // 現在の合成モード
    private XnaRect _outerScissor;

    private const int ShadowSize = 64;       // ソフトシャドウ texture の一辺
    private const int ShadowSrcBorder = 28;  // 9-slice の角ソース（丸み＋フェザー）。中央 src = 64 - 2*28 = 8px

    public MonoGamePainter(GraphicsDevice device, SpriteBatch batch)
    {
        _device = device;
        _batch = batch;
        _white = CreateWhitePixel(device);
        _corner = CreateCornerTexture(device);
        _shadow = CreateShadowTexture(device);
        _clip = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.CullCounterClockwiseFace };
        _device.DeviceReset += OnDeviceReset; // 端末リセット/ロスト時にテクスチャを作り直す
    }

    // GraphicsDevice のリセット（フルスクリーン切替/Alt+Tab 等）で内容が失われたテクスチャを再生成する。
    private void OnDeviceReset(object? sender, System.EventArgs e)
    {
        _white.Dispose();
        _corner.Dispose();
        _shadow.Dispose();
        _white = CreateWhitePixel(_device);
        _corner = CreateCornerTexture(_device);
        _shadow = CreateShadowTexture(_device);
        ClearGradCache(); // グラデ・ストリップは次回 FillGradient で作り直す
    }

    private void ClearGradCache()
    {
        foreach (Texture2D tex in _gradCache.Values)
        {
            tex.Dispose();
        }

        _gradCache.Clear();
    }

    /// <summary>Published for live drawing (<see cref="Hamon.Widgets.SceneDrawContext.Painter"/>).</summary>
    public SpriteBatch Batch => _batch;

    public GraphicsDevice Device => _device;

    public void BeginFrame()
    {
        _clipStack.Clear();
        _blendStack.Clear();
        _blend = BlendState.NonPremultiplied;
        _outerScissor = _device.ScissorRectangle;
        _device.ScissorRectangle = _device.Viewport.Bounds; // 既定は全面
        BeginBatch();
    }

    public void EndFrame()
    {
        _batch.End();
        _device.ScissorRectangle = _outerScissor;
        _clipStack.Clear();
        _blendStack.Clear();
    }

    // 現在の合成モード＋シザー設定でバッチを開く（クリップ/ブレンド/グラデの往復で共通）。
    private void BeginBatch() => _batch.Begin(SpriteSortMode.Deferred, _blend, null, null, _clip);

    public object? PushBlend(BlendMode mode)
    {
        BlendState next = mode == BlendMode.Additive ? BlendState.Additive : BlendState.NonPremultiplied;
        _blendStack.Add(_blend);
        if (next != _blend)
        {
            _batch.End();
            _blend = next;
            BeginBatch();
        }

        return null;
    }

    public void PopBlend(object? token)
    {
        if (_blendStack.Count == 0)
        {
            return;
        }

        BlendState prev = _blendStack[_blendStack.Count - 1];
        _blendStack.RemoveAt(_blendStack.Count - 1);
        if (prev != _blend)
        {
            _batch.End();
            _blend = prev;
            BeginBatch();
        }
    }

    public void FillRect(Rect rect, Color color) => _batch.Draw(_white, ToRect(rect), ToXna(color));

    public void FillRoundedRect(Rect rect, Color color, float radius)
    {
        // 整数ピクセル境界で敷き詰める：中央帯・左右帯・四隅が「重なりも隙間もなく」接するようにする。
        // （float→int の丸めで 1px 重なると、半透明色のとき重複部が二重ブレンドして線に見えるのを防ぐ）
        int x0 = (int)MathF.Floor(rect.X);
        int y0 = (int)MathF.Floor(rect.Y);
        int x1 = (int)MathF.Ceiling(rect.Right);
        int y1 = (int)MathF.Ceiling(rect.Bottom);
        int w = x1 - x0;
        int h = y1 - y0;
        int r = (int)Math.Clamp(MathF.Round(radius), 0f, Math.Min(w, h) / 2f);

        XnaColor c = ToXna(color);
        if (r <= 0 || w <= 0 || h <= 0)
        {
            _batch.Draw(_white, new XnaRect(x0, y0, w, h), c);
            return;
        }

        _batch.Draw(_white, new XnaRect(x0 + r, y0, w - (2 * r), h), c);     // 中央帯（全高）
        _batch.Draw(_white, new XnaRect(x0, y0 + r, r, h - (2 * r)), c);     // 左帯（隅の間）
        _batch.Draw(_white, new XnaRect(x1 - r, y0 + r, r, h - (2 * r)), c); // 右帯（隅の間）

        int half = _corner.Width / 2;
        _batch.Draw(_corner, new XnaRect(x0, y0, r, r), new XnaRect(0, 0, half, half), c);
        _batch.Draw(_corner, new XnaRect(x1 - r, y0, r, r), new XnaRect(half, 0, half, half), c);
        _batch.Draw(_corner, new XnaRect(x0, y1 - r, r, r), new XnaRect(0, half, half, half), c);
        _batch.Draw(_corner, new XnaRect(x1 - r, y1 - r, r, r), new XnaRect(half, half, half, half), c);
    }

    public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
    {
        var tex = ((MonoGameTexture)texture).Texture;
        _batch.Draw(tex, ToRect(dest), new XnaRect(source.X, source.Y, source.Width, source.Height), ToXna(tint));
    }

    public void DrawLine(Vec2 a, Vec2 b, float thickness, Color color)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float len = MathF.Sqrt((dx * dx) + (dy * dy));
        if (len <= 0.0001f)
        {
            return;
        }

        float angle = MathF.Atan2(dy, dx);
        // 1px 白を「左端中央」を支点に長さ×太さへ拡縮し、線の角度へ回転（origin はテクセル[0..1]基準）。
        _batch.Draw(_white, new XnaVec2(a.X, a.Y), null, ToXna(color), angle, new XnaVec2(0f, 0.5f), new XnaVec2(len, thickness), SpriteEffects.None, 0f);
    }

    public void FillCircle(Vec2 center, float radius, Color color)
    {
        if (radius <= 0f)
        {
            return;
        }

        // 角丸用の AA 円テクスチャ全体を円矩形へ拡縮（縁1pxアンチエイリアス）。
        int d = (int)MathF.Round(radius * 2f);
        var dest = new XnaRect((int)MathF.Round(center.X - radius), (int)MathF.Round(center.Y - radius), d, d);
        _batch.Draw(_corner, dest, null, ToXna(color));
    }

    public void FillGradient(Rect rect, Color a, Color b, GradientAxis axis)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        // 2色を補間した 1×N（縦）/ N×1（横）ストリップを LinearClamp で引き伸ばす＝GPU が補間。
        // 通常の SpriteBatch.Draw なので<b>バッチ分割が起きない</b>（旧 DrawUserIndexedPrimitives 版は毎回 End/Begin が必要だった）。
        Texture2D strip = GetGradientTexture(a, b, axis);
        _batch.Draw(strip, ToRect(rect), XnaColor.White);
    }

    private Texture2D GetGradientTexture(Color a, Color b, GradientAxis axis)
    {
        (uint A, uint B, GradientAxis Axis) key = (Pack(a), Pack(b), axis);
        if (_gradCache.TryGetValue(key, out Texture2D? cached))
        {
            return cached;
        }

        const int n = 64;
        var data = new XnaColor[n];
        for (int i = 0; i < n; i++)
        {
            data[i] = ToXna(Color.Lerp(a, b, i / (float)(n - 1)));
        }

        var tex = axis == GradientAxis.Horizontal ? new Texture2D(_device, n, 1) : new Texture2D(_device, 1, n);
        tex.SetData(data);
        _gradCache[key] = tex;
        return tex;
    }

    private static uint Pack(Color c) => ((uint)c.R << 24) | ((uint)c.G << 16) | ((uint)c.B << 8) | c.A;

    public void FillShadow(Rect rect, Color color, float radius, float blur)
    {
        if (blur <= 0f || color.A == 0)
        {
            return;
        }

        float bl = MathF.Max(1f, blur);
        float r = Math.Clamp(radius, 0f, MathF.Min(rect.Width, rect.Height) * 0.5f);

        // 共有する整数の切れ目（隣接スライスが同じ境界を使う＝隙間/重なりの seam=縦横線 を出さない）。
        // 角の dest 幅 = r + bl（角丸テクスチャを敷くので要素の丸みに追従し、ピル端から四角い影がはみ出さない）。
        int x0 = (int)MathF.Round(rect.X - bl);
        int x3 = (int)MathF.Round(rect.Right + bl);
        int y0 = (int)MathF.Round(rect.Y - bl);
        int y3 = (int)MathF.Round(rect.Bottom + bl);
        int x1 = (int)MathF.Round(rect.X + r);
        int x2 = (int)MathF.Round(rect.Right - r);
        int y1 = (int)MathF.Round(rect.Y + r);
        int y2 = (int)MathF.Round(rect.Bottom - r);
        if (x1 > x2)
        {
            x1 = x2 = (x1 + x2) / 2;
        }

        if (y1 > y2)
        {
            y1 = y2 = (y1 + y2) / 2;
        }

        XnaColor c = ToXna(color);
        const int b = ShadowSrcBorder;
        const int mid = ShadowSize - (2 * b); // 中央 src（フラット部）

        SliceRect(x0, y0, x1, y1, 0, 0, b, b, c);                              // 左上
        SliceRect(x2, y0, x3, y1, ShadowSize - b, 0, b, b, c);                 // 右上
        SliceRect(x0, y2, x1, y3, 0, ShadowSize - b, b, b, c);                 // 左下
        SliceRect(x2, y2, x3, y3, ShadowSize - b, ShadowSize - b, b, b, c);    // 右下
        SliceRect(x1, y0, x2, y1, b, 0, mid, b, c);                            // 上辺
        SliceRect(x1, y2, x2, y3, b, ShadowSize - b, mid, b, c);               // 下辺
        SliceRect(x0, y1, x1, y2, 0, b, b, mid, c);                            // 左辺
        SliceRect(x2, y1, x3, y2, ShadowSize - b, b, b, mid, c);              // 右辺
        SliceRect(x1, y1, x2, y2, b, b, mid, mid, c);                          // 中央
    }

    public void FillRectRotated(Rect rect, Color color, float radians, Vec2 pivot)
    {
        if (radians == 0f)
        {
            FillRect(rect, color);
            return;
        }

        float w = rect.Width;
        float h = rect.Height;
        // origin（テクセル[0..1]）×scale(w,h) が「pivot からの矩形左上オフセット」になるよう設定して pivot で回す。
        var origin = new XnaVec2(w <= 0f ? 0f : (pivot.X - rect.X) / w, h <= 0f ? 0f : (pivot.Y - rect.Y) / h);
        _batch.Draw(_white, new XnaVec2(pivot.X, pivot.Y), null, ToXna(color), radians, origin, new XnaVec2(w, h), SpriteEffects.None, 0f);
    }

    public void DrawTextureRotated(ITexture texture, Rect dest, RectInt source, Color tint, float radians, Vec2 pivot)
    {
        if (radians == 0f)
        {
            DrawTexture(texture, dest, source, tint);
            return;
        }

        var tex = ((MonoGameTexture)texture).Texture;
        var src = new XnaRect(source.X, source.Y, source.Width, source.Height);
        float sx = source.Width <= 0 ? 0f : dest.Width / source.Width;
        float sy = source.Height <= 0 ? 0f : dest.Height / source.Height;
        var origin = new XnaVec2(sx <= 0f ? 0f : (pivot.X - dest.X) / sx, sy <= 0f ? 0f : (pivot.Y - dest.Y) / sy);
        _batch.Draw(tex, new XnaVec2(pivot.X, pivot.Y), src, ToXna(tint), radians, origin, new XnaVec2(sx, sy), SpriteEffects.None, 0f);
    }

    // ソフトシャドウ texture の 9-slice 1枚（dest 端=整数で隣接共有、src=テクセル）。退化スライスは描かない。
    private void SliceRect(int dx0, int dy0, int dx1, int dy1, int sx, int sy, int sw, int sh, XnaColor c)
    {
        int w = dx1 - dx0;
        int h = dy1 - dy0;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        _batch.Draw(_shadow, new XnaRect(dx0, dy0, w, h), new XnaRect(sx, sy, sw, sh), c);
    }

    public object? PushClip(Rect rect)
    {
        XnaRect previous = _device.ScissorRectangle;
        XnaRect next = XnaRect.Intersect(previous, ToRect(rect));

        // 実際にシザーが狭まらないなら SpriteBatch を flush しない（Begin/End の往復＝ドローコール分割を節約）。
        if (next == previous)
        {
            _clipStack.Add((previous, false));
            return null;
        }

        _batch.End();
        _device.ScissorRectangle = next;
        BeginBatch();
        _clipStack.Add((previous, true));
        return null; // トークンは内部スタックで管理（XnaRect のボックス化を避ける＝ZeroAlloc）
    }

    public void PopClip(object? token)
    {
        if (_clipStack.Count == 0)
        {
            return;
        }

        (XnaRect previous, bool flushed) = _clipStack[_clipStack.Count - 1];
        _clipStack.RemoveAt(_clipStack.Count - 1);
        if (!flushed)
        {
            return; // push 時に flush していなければ何もしない
        }

        _batch.End();
        _device.ScissorRectangle = previous;
        BeginBatch();
    }

    public void Dispose()
    {
        _device.DeviceReset -= OnDeviceReset;
        _white.Dispose();
        _corner.Dispose();
        _shadow.Dispose();
        _clip.Dispose();
        ClearGradCache();
    }

    private static XnaRect ToRect(Rect r) => new((int)r.X, (int)r.Y, (int)MathF.Ceiling(r.Width), (int)MathF.Ceiling(r.Height));

    private static XnaColor ToXna(Color c) => new(c.R, c.G, c.B, c.A);

    private static Texture2D CreateWhitePixel(GraphicsDevice device)
    {
        var texture = new Texture2D(device, 1, 1);
        texture.SetData(new[] { XnaColor.White });
        return texture;
    }

    /// <summary>Circle texture for drawing rounded corners (white + alpha, edges 1px anti-aliased). </summary>
    private static Texture2D CreateCornerTexture(GraphicsDevice device)
    {
        const int diameter = 192;
        var texture = new Texture2D(device, diameter, diameter);
        var data = new XnaColor[diameter * diameter];
        float center = diameter / 2f;
        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x + 0.5f - center;
                float dy = y + 0.5f - center;
                float dist = MathF.Sqrt((dx * dx) + (dy * dy));
                float alpha = Math.Clamp(center - dist + 0.5f, 0f, 1f); // 円内=1, 縁でAA, 外=0
                data[(y * diameter) + x] = new XnaColor((byte)255, (byte)255, (byte)255, (byte)(alpha * 255f));
            }
        }

        texture.SetData(data);
        return texture;
    }

    /// <summary>
    /// A <b>rounded-corner</b> blob texture (white + alpha) with soft, feathered edges, used for soft
    /// shadows. The border region (<see cref="ShadowSrcBorder"/>) encodes both the roundness and the
    /// feather, so tiling it as a 9-slice produces a rounded shadow.
    /// </summary>
    private static Texture2D CreateShadowTexture(GraphicsDevice device)
    {
        const int size = ShadowSize;       // 64
        const float feather = 16f;          // にじみ幅
        const float cornerRadius = 12f;     // 影の角丸
        const float half = size / 2f;
        const float ext = half - feather;   // 実体（フェザーの内側）の半径 = 16
        var texture = new Texture2D(device, size, size);
        var data = new XnaColor[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;
                float d = RoundedBoxSdf(px, py, ext, ext, cornerRadius);
                float t = Math.Clamp(d / feather, 0f, 1f);
                float alpha = 1f - (t * t * (3f - (2f * t))); // 内側=1・縁で smoothstep 減衰・外=0
                data[(y * size) + x] = new XnaColor((byte)255, (byte)255, (byte)255, (byte)(alpha * 255f));
            }
        }

        texture.SetData(data);
        return texture;
    }

    // 角丸矩形の符号付き距離（中心基準・半径 bx/by・角丸 r）。負=内側。
    private static float RoundedBoxSdf(float px, float py, float bx, float by, float r)
    {
        float qx = MathF.Abs(px) - bx + r;
        float qy = MathF.Abs(py) - by + r;
        float ax = MathF.Max(qx, 0f);
        float ay = MathF.Max(qy, 0f);
        return MathF.Sqrt((ax * ax) + (ay * ay)) + MathF.Min(MathF.Max(qx, qy), 0f) - r;
    }
}
