using FontStashSharp;
using FontStashSharp.Interfaces;
using Hamon.Layout;
using DrawRect = System.Drawing.Rectangle;
using NumVector2 = System.Numerics.Vector2;

namespace Hamon.Testing.Rendering;

/// <summary>
/// Real font drawing without GPU.
/// <see cref="RasterCanvas"/>（<see cref="RasterPainter"/>α composite blit to the same plane)<see cref="ITextRenderer"/>。
/// existing<c>Hamon.Fonts/FontStashTextRenderer</c>The same type as , but the drawing destination is a pixel buffer instead of a SpriteBatch.
/// It does not depend on the premultiply setting (straight-alpha) because it combines atlas alpha as coverage and tint as color.
/// </summary>
public sealed class HeadlessFontRenderer : ITextRenderer, IFontStashRenderer
{
    /// <summary>Name of the default font (when font is not specified).</summary>
    public const string DefaultFont = "";

    private readonly RasterCanvas _canvas;
    private readonly RasterTextureManager _textureManager = new();
    private readonly Dictionary<string, FontSystem> _fonts = new();

    public HeadlessFontRenderer(RasterCanvas canvas, byte[] fontData)
    {
        _canvas = canvas;
        AddFont(DefaultFont, fontData);
    }

    public void AddFont(string name, byte[] fontData)
    {
        var system = new FontSystem(new FontSystemSettings
        {
            FontResolutionFactor = 1, // スーパーサンプリング無し＝1:1 blit で決定論を優先
            KernelWidth = 0,
            KernelHeight = 0,
            TextureWidth = 1024,
            TextureHeight = 1024,
        });
        system.AddFont(fontData);
        _fonts[name] = system;
    }

    private FontSystem Resolve(string? font) =>
        font is not null && _fonts.TryGetValue(font, out FontSystem? system) ? system : _fonts[DefaultFont];

    public Vec2 Measure(string text, float pixelSize) => Measure(text, pixelSize, null);

    public Vec2 Measure(string text, float pixelSize, string? font)
    {
        DynamicSpriteFont f = Resolve(font).GetFont(pixelSize);
        NumVector2 size = f.MeasureString(text);
        return new Vec2(size.X, size.Y);
    }

    public void Draw(string text, Vec2 position, float pixelSize, Color color) => Draw(text, position, pixelSize, color, null);

    public void Draw(string text, Vec2 position, float pixelSize, Color color, string? font)
    {
        DynamicSpriteFont f = Resolve(font).GetFont(pixelSize);
        f.DrawText(this, text, new NumVector2(position.X, position.Y), new FSColor(color.R, color.G, color.B, color.A));
    }

    ITexture2DManager IFontStashRenderer.TextureManager => _textureManager;

    void IFontStashRenderer.Draw(object texture, NumVector2 pos, DrawRect? src, FSColor color, float rotation, NumVector2 scale, float depth)
    {
        var atlas = (RasterTextureManager.Atlas)texture;
        DrawRect s = src ?? new DrawRect(0, 0, atlas.Width, atlas.Height);
        float sx = scale.X <= 0f ? 1f : scale.X;
        float sy = scale.Y <= 0f ? 1f : scale.Y;
        var tint = new Color(color.R, color.G, color.B, color.A);

        int destW = (int)MathF.Ceiling(s.Width * sx);
        int destH = (int)MathF.Ceiling(s.Height * sy);
        int baseX = (int)MathF.Round(pos.X);
        int baseY = (int)MathF.Round(pos.Y);

        for (int dy = 0; dy < destH; dy++)
        {
            int srcY = s.Y + (int)((dy + 0.5f) / sy);
            if (srcY < s.Y || srcY >= s.Y + s.Height)
            {
                continue;
            }

            int atlasRow = srcY * atlas.Width;
            for (int dx = 0; dx < destW; dx++)
            {
                int srcX = s.X + (int)((dx + 0.5f) / sx);
                if (srcX < s.X || srcX >= s.X + s.Width)
                {
                    continue;
                }

                byte coverage = atlas.Pixels[((atlasRow + srcX) * 4) + 3]; // アルファ＝グリフの被覆
                if (coverage != 0)
                {
                    _canvas.BlendPixel(baseX + dx, baseY + dy, tint, coverage);
                }
            }
        }
    }
}
