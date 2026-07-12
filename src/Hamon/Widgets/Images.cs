using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>How to fit textures into boxes (Flutter<c>BoxFit</c>(minimum version).</summary>
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
/// Draw textures (icons/images/sprites) (Flutter<c>Image</c>）。<see cref="Source"/>in the sprite sheet
/// specify the part,<see cref="Fit"/>How to store it in<see cref="Tint"/>Color multiplication with. <see cref="Width"/>/<see cref="Height"/>、
/// If not specified, the original size of the source.<see cref="Texture"/>If is null, nothing is drawn (layout only).
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

/// <summary><see cref="Image"/>holding entity. <see cref="BoxFit"/>Draw according to the following.</summary>
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
/// A panel that draws texture with 9-slices (9-patch) (corners are original size, sides are stretchable, and center is both stretchable).
/// on the window/button background.<see cref="Border"/>is the texture frame width (px) and the inner margin of the content.<see cref="Child"/>is inside.
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

/// <summary><see cref="NineSlice"/>holding entity. </summary>
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
