using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Draw children with opacity 0..1 (Flutter<c>Opacity</c>）。<see cref="ValueGetter"/>If you pass , it will be read every frame when drawing
/// （<c>() =&gt; ctrl.Curved</c>= fade without reconstruction). <see cref="Value"/>。
/// Layout remains unchanged (drawing only).
/// </summary>
public sealed class Opacity : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public float Value { get; init; } = 1f;

    /// <summary>Opacity to be read each time drawing (for anime). <see cref="Value"/>More priority.</summary>
    public Func<float>? ValueGetter { get; init; }

    internal float Resolve() => ValueGetter?.Invoke() ?? Value;

    // 単一子パススルー（tight なら子を充填・loose なら子の自然サイズ）。レイアウトは透過し描画のみ変える。
    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new OpacityElement(this);
}

/// <summary><see cref="Opacity"/>holding entity. </summary>
internal sealed class OpacityElement : RenderElement
{
    public OpacityElement(Opacity widget)
        : base(widget)
    {
    }

    public override void Paint(in PaintContext context)
    {
        PaintContext child = context.WithOpacity(((Opacity)Widget).Resolve());
        base.Paint(child);
    }
}

/// <summary>
/// Draw by applying transformation (scaling + parallel translation + rotation) to the child (Flutter<c>Transform</c>equivalent).
/// <c>*Getter</c>(Read each time you draw = animation).<see cref="Origin"/>is the scaling/rotation fulcrum (default center).
/// <b>Layout/hit test remains original rectangle</b>(Conversion of drawing only) = Suitable for transition effects/spinners/dials.
/// Rotation is for single content (icon/image/shape), and text being rotated, rotated clips, and non-uniform scale + rotation nesting are not applicable (approximation).
/// </summary>
public sealed class Transform : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public float Scale { get; init; } = 1f;

    public Func<float>? ScaleGetter { get; init; }

    /// <summary>Rotation (radian/clockwise). </summary>
    public float Rotation { get; init; }

    /// <summary>Read rotation each time when drawing (for animation. When setting<see cref="Rotation"/>priority).</summary>
    public Func<float>? RotationGetter { get; init; }

    internal float ResolveRotation() => RotationGetter?.Invoke() ?? Rotation;

    /// <summary>Scale getter for X axis only (uniform if unspecified)<see cref="Scale"/>/<see cref="ScaleGetter"/>).</summary>
    public Func<float>? ScaleXGetter { get; init; }

    /// <summary>Scale getter for Y axis only (uniform if unspecified).</summary>
    public Func<float>? ScaleYGetter { get; init; }

    public float TranslateX { get; init; }

    public Func<float>? TranslateXGetter { get; init; }

    public float TranslateY { get; init; }

    public Func<float>? TranslateYGetter { get; init; }

    /// <summary>Scaling fulcrum (default<see cref="Alignment.Center"/>）。</summary>
    public Alignment Origin { get; init; } = Alignment.Center;

    internal float ResolveScale() => ScaleGetter?.Invoke() ?? Scale;

    internal float ResolveScaleX() => ScaleXGetter?.Invoke() ?? ResolveScale();

    internal float ResolveScaleY() => ScaleYGetter?.Invoke() ?? ResolveScale();

    internal float ResolveTranslateX() => TranslateXGetter?.Invoke() ?? TranslateX;

    internal float ResolveTranslateY() => TranslateYGetter?.Invoke() ?? TranslateY;

    // 単一子パススルー（tight なら子を充填・loose なら子の自然サイズ）。レイアウトは透過し描画のみ変える。
    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new TransformElement(this);
}

/// <summary><see cref="Transform"/>holding entity. </summary>
internal sealed class TransformElement : RenderElement
{
    public TransformElement(Transform widget)
        : base(widget)
    {
    }

    public override void Paint(in PaintContext context)
    {
        var widget = (Transform)Widget;
        Rect bounds = LayoutNode.Bounds;
        Vec2 pivot = AnchorOf(bounds, widget.Origin);
        var translate = new Vec2(widget.ResolveTranslateX(), widget.ResolveTranslateY());
        Transform2D local = Transform2D.ScaleAbout(new Vec2(widget.ResolveScaleX(), widget.ResolveScaleY()), pivot, translate);
        PaintContext child = context.WithTransform(local);
        float rotation = widget.ResolveRotation();
        if (rotation != 0f)
        {
            child = child.WithRotation(rotation, pivot); // pivot（絶対座標）は WithTransform 後の変換でデバイス空間へ写る
        }

        base.Paint(child);
    }

    private static Vec2 AnchorOf(Rect r, Alignment a)
    {
        float fx = a switch
        {
            Alignment.TopLeft or Alignment.CenterLeft or Alignment.BottomLeft => 0f,
            Alignment.TopRight or Alignment.CenterRight or Alignment.BottomRight => 1f,
            _ => 0.5f,
        };
        float fy = a switch
        {
            Alignment.TopLeft or Alignment.TopCenter or Alignment.TopRight => 0f,
            Alignment.BottomLeft or Alignment.BottomCenter or Alignment.BottomRight => 1f,
            _ => 0.5f,
        };
        return new Vec2(r.X + (r.Width * fx), r.Y + (r.Height * fy));
    }
}
