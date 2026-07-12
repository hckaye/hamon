using FontStashSharp;
using FontStashSharp.Interfaces;
using Hamon.Layout;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using DrawRect = System.Drawing.Rectangle;
using NumVector2 = System.Numerics.Vector2;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Hamon.Fonts;

/// <summary>
/// FontStashSharp (TTF dynamic glyph atlas) in MonoGame<see cref="SpriteBatch"/>bridge to
/// <see cref="ITextRenderer"/>Implementation (text layer on MonoGame backend side). <see cref="MonoGamePainter"/>
/// (characters are also stacked in the same Begin batch).
/// </summary>
public sealed class FontStashTextRenderer : ITextRenderer, IFontStashRenderer, IDisposable
{
    /// <summary>Default font (<see cref="ITextRenderer"/>(if font is not specified).</summary>
    public const string DefaultFont = "";

    private readonly MonoGameTextureManager _textureManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _batch;
    private readonly int _resolutionFactor;
    private readonly Dictionary<string, FontSystem> _fonts = new();

    /// <param name="resolutionFactor">
    /// Glyph rasterization resolution magnification.
    /// Atlas/cost increase.
    /// </param>
    public FontStashTextRenderer(GraphicsDevice graphicsDevice, byte[] fontData, SpriteBatch batch, int resolutionFactor = 3)
    {
        _textureManager = new MonoGameTextureManager(graphicsDevice);
        _graphicsDevice = graphicsDevice;
        _batch = batch;
        _resolutionFactor = Math.Max(1, resolutionFactor);
        AddFont(DefaultFont, fontData);
        _graphicsDevice.DeviceReset += OnDeviceReset; // 端末リセットでグリフアトラスを作り直す
    }

    /// <summary>
    /// Rebuild glyph atlas after GraphicsDevice reset/lost (discard atlas texture, font
    /// (reset and re-rasterize on next drawing).<see cref="GraphicsDevice.DeviceReset"/>is automatically called from.
    /// </summary>
    public void OnDeviceReset()
    {
        _textureManager.Reset();
        foreach (FontSystem system in _fonts.Values)
        {
            system.Reset();
        }
    }

    private void OnDeviceReset(object? sender, System.EventArgs e) => OnDeviceReset();

    /// <summary>
    /// Number of pages in Glyph Atlas (secured by FontStash)<see cref="Texture2D"/>number).
    /// Monitor it and if it increases unexpectedly (memory pressure), use it to determine whether to warn/regenerate on the application side.
    /// </summary>
    public int AtlasPageCount => _textureManager.PageCount;

    /// <summary>Total number of bytes in the glyph atlas (RGBA equivalent). </summary>
    public long AtlasByteSize => _textureManager.ByteSize;

    /// <summary>Register a font with a name (<see cref="Text.Font"/>Select by etc. </summary>
    public void AddFont(string name, byte[] fontData)
    {
        var system = new FontSystem(new FontSystemSettings
        {
            FontResolutionFactor = _resolutionFactor, // スーパーサンプリングでエッジを滑らかに
            KernelWidth = 0,
            KernelHeight = 0,
            TextureWidth = 2048,
            TextureHeight = 2048,
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
        f.DrawText(this, text, new NumVector2(position.X, position.Y), FontStashInterop.ToFs(color));
    }

    // --- IFontStashRenderer（FontStashSharp からのグリフ矩形コールバック） ---

    ITexture2DManager IFontStashRenderer.TextureManager => _textureManager;

    void IFontStashRenderer.Draw(object texture, NumVector2 pos, DrawRect? src, FSColor color, float rotation, NumVector2 scale, float depth)
    {
        var tex = (Texture2D)texture;
        Microsoft.Xna.Framework.Rectangle? source = src.HasValue ? FontStashInterop.ToXna(src.Value) : null;
        _batch.Draw(
            tex,
            FontStashInterop.ToXna(pos),
            source,
            new XnaColor(color.R, color.G, color.B, color.A),
            rotation,
            XnaVector2.Zero,
            FontStashInterop.ToXna(scale),
            SpriteEffects.None,
            depth);
    }

    public void Dispose()
    {
        _graphicsDevice.DeviceReset -= OnDeviceReset;
        foreach (FontSystem system in _fonts.Values)
        {
            system.Dispose();
        }

        _fonts.Clear();
        _textureManager.Dispose();
    }
}
