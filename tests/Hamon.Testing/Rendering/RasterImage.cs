using Hamon.Layout;

namespace Hamon.Testing.Rendering;

/// <summary>
/// Invariant pixel image in RGBA8888 (row-major, first = top left).
/// (Hamon's<c>Image</c>To distinguish it from a widget<see cref="RasterImage"/>。）
/// </summary>
public sealed class RasterImage
{
    public RasterImage(int width, int height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException($"rgba length {rgba.Length} != {width}x{height}x4", nameof(rgba));
        }

        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Raw buffer (length = Width*Height*4・R,G,B,A order).</summary>
    public byte[] Rgba { get; }

    public Color GetPixel(int x, int y)
    {
        int i = ((y * Width) + x) * 4;
        return new Color(Rgba[i], Rgba[i + 1], Rgba[i + 2], Rgba[i + 3]);
    }
}
