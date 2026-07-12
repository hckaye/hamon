using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>How to fit a texture into a box (a minimal version of Flutter's <c>BoxFit</c>).</summary>
public enum BoxFit : byte
{
    /// <summary>Stretch it to fill the box (ignoring aspect ratio).</summary>
    Fill,

    /// <summary>Reduced to fit proportionately (letterbox).</summary>
    Contain,

    /// <summary>Cover the box while maintaining the aspect ratio (trimming any excess on the source side).</summary>
    Cover,

    /// <summary>Actual size (center of box; protrusion depends on parent clip).</summary>
    None,
}

/// <summary>
/// Draws a texture (icon/image/sprite), equivalent to Flutter's <c>Image</c>. Use <see cref="Source"/> to specify a
/// region within a sprite sheet, <see cref="Fit"/> for how it is fit into the box, and <see cref="Tint"/> for a
/// color multiply. If <see cref="Width"/>/<see cref="Height"/> are not specified, the source's original size is
/// used. If <see cref="Texture"/> is null, nothing is drawn (layout only).
/// </summary>
public sealed class Image : Widget
{
    public ITexture? Texture { get; init; }

    /// <summary>Partial rectangle in texture (sprite sheet cell). </summary>
    public RectInt? Source { get; init; }

    public Color Tint { get; init; } = Color.White;

    public BoxFit Fit { get; init; } = BoxFit.Fill;

    public Dimension Width { get; init; }

    public Dimension Height { get; init; }

    public override Element CreateElement() => new ImageElement(this);
}

/// <summary>The holding entity for <see cref="Image"/>. Draws according to <see cref="BoxFit"/>.</summary>
internal sealed class ImageElement : Element
{
    private readonly LayoutNode _node;

    public ImageElement(Image widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    public override LayoutNode LayoutNode => _node;

    private Image W => (Image)Widget;

    public override void Paint(in PaintContext context)
    {
        if (W.Texture is not ITexture texture)
        {
            return;
        }

        Rect b = _node.Bounds;
        RectInt region = W.Source ?? new RectInt(0, 0, texture.Width, texture.Height);
        float srcW = region.Width;
        float srcH = region.Height;
        if (srcW <= 0f || srcH <= 0f)
        {
            return;
        }

        switch (W.Fit)
        {
            case BoxFit.Fill:
                context.DrawTexture(texture, b, region, W.Tint);
                break;
            case BoxFit.None:
                context.DrawTexture(texture, Centered(b, srcW, srcH), region, W.Tint);
                break;
            case BoxFit.Contain:
                {
                    float scale = MathF.Min(b.Width / srcW, b.Height / srcH);
                    context.DrawTexture(texture, Centered(b, srcW * scale, srcH * scale), region, W.Tint);
                    break;
                }

            case BoxFit.Cover:
                {
                    float scale = MathF.Max(b.Width / srcW, b.Height / srcH);
                    float visibleW = b.Width / scale;
                    float visibleH = b.Height / scale;
                    var cropped = new RectInt(
                        region.X + (int)((srcW - visibleW) / 2f),
                        region.Y + (int)((srcH - visibleH) / 2f),
                        (int)visibleW,
                        (int)visibleH);
                    context.DrawTexture(texture, b, cropped, W.Tint);
                    break;
                }
        }
    }

    private static Rect Centered(Rect box, float w, float h) =>
        new(box.X + ((box.Width - w) / 2f), box.Y + ((box.Height - h) / 2f), w, h);

    private Size MeasureSelf(BoxConstraints constraints)
    {
        float srcW = W.Source?.Width ?? W.Texture?.Width ?? 0;
        float srcH = W.Source?.Height ?? W.Texture?.Height ?? 0;
        float w = W.Width.Resolve(constraints.MaxWidth) ?? srcW;
        float h = W.Height.Resolve(constraints.MaxHeight) ?? srcH;
        return constraints.Constrain(new Size(w, h));
    }
}

/// <summary>
/// A panel that draws a texture as a 9-slice (9-patch): corners keep their original size, the edges stretch along
/// one axis, and the center stretches along both axes. Used for window/button backgrounds. <see cref="Border"/> is
/// both the texture's frame width (px) and the content's inner margin. <see cref="Child"/> is placed inside.
/// </summary>
public sealed class NineSlice : Widget, IRenderConfig
{
    public ITexture? Texture { get; init; }

    /// <summary>Texture border width (px). </summary>
    public EdgeInsets Border { get; init; }

    public Color Tint { get; init; } = Color.White;

    public Widget? Child { get; init; }

    public Dimension Width { get; init; }

    public Dimension Height { get; init; }

    Style IRenderConfig.Style => new()
    {
        Direction = Axis.Vertical,
        MainAxisSize = MainAxisSize.Min,
        Padding = Border, // 子は枠の内側へ
        Width = Width,
        Height = Height,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new NineSliceElement(this);
}

/// <summary>The holding entity for <see cref="NineSlice"/>.</summary>
internal sealed class NineSliceElement : RenderElement
{
    public NineSliceElement(NineSlice widget)
        : base(widget)
    {
    }

    public override void Paint(in PaintContext context)
    {
        var widget = (NineSlice)Widget;
        if (widget.Texture is ITexture texture)
        {
            context.DrawNineSlice(texture, LayoutNode.Bounds, widget.Border, widget.Tint);
        }

        base.Paint(context); // 子（枠の内側）
    }
}
