using FontStashSharp.Interfaces;
using DrawPoint = System.Drawing.Point;
using DrawRect = System.Drawing.Rectangle;

namespace Hamon.Testing.Rendering;

/// <summary>
/// Backing FontStashSharp's glyph atlas with managed RGBA byte[] (GPU independent).
/// existing<c>Hamon.Fonts/MonoGameTextureManager</c>The Texture2D version of is replaced with the byte[] version.
/// </summary>
internal sealed class RasterTextureManager : ITexture2DManager
{
    /// <summary>1 page managed glyph atlas (RGBA8888).</summary>
    internal sealed class Atlas
    {
        public Atlas(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 4];
        }

        public int Width { get; }

        public int Height { get; }

        public byte[] Pixels { get; }
    }

    public object CreateTexture(int width, int height) => new Atlas(width, height);

    public DrawPoint GetTextureSize(object texture)
    {
        var a = (Atlas)texture;
        return new DrawPoint(a.Width, a.Height);
    }

    public void SetTextureData(object texture, DrawRect bounds, byte[] data)
    {
        var a = (Atlas)texture;
        int stride = a.Width * 4;
        int srcStride = bounds.Width * 4;
        for (int row = 0; row < bounds.Height; row++)
        {
            int dst = ((bounds.Y + row) * stride) + (bounds.X * 4);
            int src = row * srcStride;
            Array.Copy(data, src, a.Pixels, dst, srcStride);
        }
    }
}
