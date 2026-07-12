using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// for games<b>image skin</b>(declarative data that draws a sprite/9-slice into a rectangle).
/// to parts<c>*Skin</c>If passed as a property, draw with this image instead of the built-in color drawing (unset =<see cref="HasValue"/>If false
/// Conventional color drawing = backward compatible).<see cref="Border"/>If is non-zero, 9-slice (scale without distorting the frame); if zero, simply stretch.
/// <see cref="Source"/>You can use partial areas of sprite sheets with .
///
/// <para>Just pass data declaratively such as "This part is this image" and do not write imperative drawing code (maintaining the idea of ​​declarative UI).
/// If you want to change the sprite depending on the state (pressed/selected, etc.), the widget should change the sprite according to the state.<see cref="ImageSkin"/>Select and hand it over.</para>
/// </summary>
public readonly struct ImageSkin
{
    public ImageSkin(ITexture texture, EdgeInsets border = default, RectInt? source = null, Color? tint = null)
    {
        Texture = texture;
        Border = border;
        Source = source;
        Tint = tint ?? Color.White;
    }

    /// <summary>Texture to draw (null = no skin = fallback to traditional color drawing).</summary>
    public ITexture? Texture { get; init; }

    /// <summary>9-slice frame width (px, if all zeros, simply stretch).</summary>
    public EdgeInsets Border { get; init; }

    /// <summary>Partial area of ​​sprite sheet (unspecified, entire texture).</summary>
    public RectInt? Source { get; init; }

    /// <summary>Multiply color (default white = leave as is).</summary>
    public Color Tint { get; init; }

    /// <summary>Is a skin set (if false, the caller falls back to color rendering)?</summary>
    public bool HasValue => Texture is not null;

    // init 構文（コンストラクタ未使用）で Tint 未指定だと default(Color)=透明になるため、その場合は白として描く。
    private Color EffectiveTint => Tint is { R: 0, G: 0, B: 0, A: 0 } ? Color.White : Tint;

    /// <summary><paramref name="rect"/>Draw a heskin (9-slice/sprite/enlarge automatically selected).</summary>
    public void Paint(in PaintContext context, Rect rect) => Paint(context, rect, EffectiveTint);

    /// <summary>Draw by overwriting the color (tone change/fade, etc. depending on the state).</summary>
    public void Paint(in PaintContext context, Rect rect, Color tint)
    {
        if (Texture is not ITexture tex)
        {
            return;
        }

        if (Border.Left != 0f || Border.Top != 0f || Border.Right != 0f || Border.Bottom != 0f)
        {
            context.DrawNineSlice(tex, rect, Border, tint, Source);
        }
        else if (Source is RectInt s)
        {
            context.DrawTexture(tex, rect, s, tint);
        }
        else
        {
            context.DrawTexture(tex, rect, tint);
        }
    }
}
