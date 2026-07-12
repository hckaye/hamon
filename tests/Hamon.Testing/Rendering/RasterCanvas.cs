using Hamon.Layout;

namespace Hamon.Testing.Rendering;

/// <summary>
/// A pure managed RGBA8888 raster surface.<see cref="RasterPainter"/>and<see cref="HeadlessFontRenderer"/>shared,
/// Fill with device space (transformation applied) coordinates.
/// Compositing should be done strictly to avoid creating dark fringes at the edges (<c>AssertNoDarkHalo</c>(assuming the guard functions correctly).
/// </summary>
public sealed class RasterCanvas
{
    private readonly byte[] _pixels;
    private readonly List<ClipRect> _clipStack = new();
    private ClipRect _clip;

    public RasterCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new byte[width * height * 4];
        _clip = new ClipRect(0, 0, width, height);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Paint the entire surface with a single opaque color (background).</summary>
    public void Clear(Color color)
    {
        for (int i = 0; i < _pixels.Length; i += 4)
        {
            _pixels[i] = color.R;
            _pixels[i + 1] = color.G;
            _pixels[i + 2] = color.B;
            _pixels[i + 3] = color.A;
        }
    }

    public object? PushClip(Rect rect)
    {
        _clipStack.Add(_clip);
        ClipRect next = ClipRect.FromRect(rect).Intersect(_clip);
        _clip = next;
        return _clipStack.Count; // 復帰検証用トークン（深さ）
    }

    public void PopClip(object? token)
    {
        if (_clipStack.Count == 0)
        {
            return;
        }

        _clip = _clipStack[^1];
        _clipStack.RemoveAt(_clipStack.Count - 1);
    }

    /// <summary>Fill a rectangle (device space float) solidly. </summary>
    public void FillRect(Rect rect, Color color, bool additive = false)
    {
        if (color.A == 0)
        {
            return;
        }

        int x0 = Round(rect.X);
        int y0 = Round(rect.Y);
        int x1 = Round(rect.Right);
        int y1 = Round(rect.Bottom);
        ClampToClip(ref x0, ref y0, ref x1, ref y1);

        for (int y = y0; y < y1; y++)
        {
            int row = y * Width * 4;
            for (int x = x0; x < x1; x++)
            {
                Blend(row + (x * 4), color.R, color.G, color.B, color.A, additive);
            }
        }
    }

    /// <summary>Compositing to a single pixel with coverage (for rounded corners/circles/glyph edges). </summary>
    public void BlendPixel(int x, int y, Color color, int coverage255, bool additive = false)
    {
        if (coverage255 <= 0 || x < _clip.X0 || x >= _clip.X1 || y < _clip.Y0 || y >= _clip.Y1)
        {
            return;
        }

        int a = color.A * coverage255 / 255;
        if (a <= 0)
        {
            return;
        }

        Blend(((y * Width) + x) * 4, color.R, color.G, color.B, a, additive);
    }

    public RasterImage ToImage()
    {
        var copy = new byte[_pixels.Length];
        Array.Copy(_pixels, copy, _pixels.Length);
        return new RasterImage(Width, Height, copy);
    }

    private void Blend(int i, byte sr, byte sg, byte sb, int sa, bool additive)
    {
        if (additive)
        {
            _pixels[i] = (byte)Math.Min(255, _pixels[i] + (sr * sa / 255));
            _pixels[i + 1] = (byte)Math.Min(255, _pixels[i + 1] + (sg * sa / 255));
            _pixels[i + 2] = (byte)Math.Min(255, _pixels[i + 2] + (sb * sa / 255));
            _pixels[i + 3] = (byte)Math.Min(255, _pixels[i + 3] + sa);
            return;
        }

        if (sa >= 255)
        {
            _pixels[i] = sr;
            _pixels[i + 1] = sg;
            _pixels[i + 2] = sb;
            _pixels[i + 3] = 255;
            return;
        }

        int inv = 255 - sa;
        _pixels[i] = (byte)(((sr * sa) + (_pixels[i] * inv) + 127) / 255);
        _pixels[i + 1] = (byte)(((sg * sa) + (_pixels[i + 1] * inv) + 127) / 255);
        _pixels[i + 2] = (byte)(((sb * sa) + (_pixels[i + 2] * inv) + 127) / 255);
        _pixels[i + 3] = (byte)(sa + ((_pixels[i + 3] * inv) + 127) / 255);
    }

    private void ClampToClip(ref int x0, ref int y0, ref int x1, ref int y1)
    {
        if (x0 < _clip.X0)
        {
            x0 = _clip.X0;
        }

        if (y0 < _clip.Y0)
        {
            y0 = _clip.Y0;
        }

        if (x1 > _clip.X1)
        {
            x1 = _clip.X1;
        }

        if (y1 > _clip.Y1)
        {
            y1 = _clip.Y1;
        }
    }

    private static int Round(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

    /// <summary>Integer clip rectangle ([X0,X1)×[Y0,Y1)).</summary>
    private readonly struct ClipRect
    {
        public ClipRect(int x0, int y0, int x1, int y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public int X0 { get; }

        public int Y0 { get; }

        public int X1 { get; }

        public int Y1 { get; }

        public static ClipRect FromRect(Rect r) => new(
            (int)MathF.Floor(r.X),
            (int)MathF.Floor(r.Y),
            (int)MathF.Ceiling(r.Right),
            (int)MathF.Ceiling(r.Bottom));

        public ClipRect Intersect(ClipRect o) => new(
            Math.Max(X0, o.X0),
            Math.Max(Y0, o.Y0),
            Math.Min(X1, o.X1),
            Math.Min(Y1, o.Y1));
    }
}
