using FontStashSharp.Interfaces;
using Microsoft.Xna.Framework.Graphics;
using DrawPoint = System.Drawing.Point;
using DrawRect = System.Drawing.Rectangle;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Hamon.Fonts;

/// <summary>
/// FontStashSharp's glyph atlas to MonoGame's<see cref="Texture2D"/>Line it with
/// Uses only MonoGame compatible API surface (compatible with both DesktopGL/KNI).
/// </summary>
internal sealed class MonoGameTextureManager : ITexture2DManager, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly List<Texture2D> _textures = new();

    public MonoGameTextureManager(GraphicsDevice graphicsDevice) => _graphicsDevice = graphicsDevice;

    /// <summary>Number of allocated atlas pages (for glyph atlas memory monitoring).</summary>
    internal int PageCount => _textures.Count;

    /// <summary>Total number of bytes of atlas texture (RGBA equivalent). </summary>
    internal long ByteSize
    {
        get
        {
            long sum = 0;
            for (int i = 0; i < _textures.Count; i++)
            {
                sum += (long)_textures[i].Width * _textures[i].Height * 4;
            }

            return sum;
        }
    }

    public object CreateTexture(int width, int height)
    {
        var texture = new Texture2D(_graphicsDevice, width, height);
        _textures.Add(texture);
        return texture;
    }

    public DrawPoint GetTextureSize(object texture)
    {
        var tex = (Texture2D)texture;
        return new DrawPoint(tex.Width, tex.Height);
    }

    public void SetTextureData(object texture, DrawRect bounds, byte[] data)
    {
        var tex = (Texture2D)texture;
        // data は bounds 領域より大きいバッファのことがある。RGBA(4byte/px) で bounds 分のみ転送する。
        tex.SetData(0, new XnaRect(bounds.X, bounds.Y, bounds.Width, bounds.Height), data, 0, bounds.Width * bounds.Height * 4);
    }

    /// <summary>Discard and clear the reserved atlas texture (for re-creation when resetting the device. Used in conjunction with Reset on the FontStash side).</summary>
    internal void Reset()
    {
        foreach (Texture2D texture in _textures)
        {
            texture.Dispose();
        }

        _textures.Clear();
    }

    public void Dispose() => Reset();
}
