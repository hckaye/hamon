using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// An <b>image skin</b> for games — declarative data describing how to draw a sprite or 9-slice into a rectangle.
/// When passed as a widget's <c>*Skin</c> property, it is drawn instead of the built-in color rendering (when unset,
/// i.e. <see cref="HasValue"/> is false, the widget falls back to its conventional color rendering, preserving
/// backward compatibility). If <see cref="Border"/> is non-zero, the image is drawn as a 9-slice (scaling without
/// distorting the frame); if zero, it is simply stretched. <see cref="Source"/> lets you use a partial region of a
/// sprite sheet.
///
/// <para>Only declarative data such as "this part uses this image" is passed here — no imperative drawing code is
/// written, preserving the declarative-UI approach. If the sprite needs to change with state (pressed/selected,
/// etc.), the widget itself should select the appropriate <see cref="ImageSkin"/> for that state and hand it over.</para>
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

    /// <summary>The texture to draw (null means no skin, so the widget falls back to its traditional color drawing).</summary>
    public ITexture? Texture { get; init; }

    /// <summary>The 9-slice frame width (px); if all sides are zero, the texture is simply stretched.</summary>
    public EdgeInsets Border { get; init; }

    /// <summary>A partial region of the sprite sheet (if unspecified, the entire texture is used).</summary>
    public RectInt? Source { get; init; }

    /// <summary>Multiply color (default white = leave as is).</summary>
    public Color Tint { get; init; }

    /// <summary>Is a skin set (if false, the caller falls back to color rendering)?</summary>
    public bool HasValue => Texture is not null;

    // init 構文（コンストラクタ未使用）で Tint 未指定だと default(Color)=透明になるため、その場合は白として描く。
    private Color EffectiveTint => Tint is { R: 0, G: 0, B: 0, A: 0 } ? Color.White : Tint;

    /// <summary>Draws the skin into <paramref name="rect"/> (9-slice/sprite/stretch is chosen automatically).</summary>
    public void Paint(in PaintContext context, Rect rect) => Paint(context, rect, EffectiveTint);

    /// <summary>Draws with an overridden tint color (e.g. for tone changes/fades depending on state).</summary>
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
