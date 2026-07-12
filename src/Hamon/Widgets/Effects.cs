using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Draws the child with opacity in 0..1 (equivalent to Flutter's <c>Opacity</c>). If <see cref="ValueGetter"/>
/// is passed, it is read every frame at draw time (e.g. <c>() =&gt; ctrl.Curved</c>, enabling fades without
/// rebuilding); otherwise <see cref="Value"/> is used. Layout is unaffected (drawing only).
/// </summary>
public sealed class Opacity : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public float Value { get; init; } = 1f;

    /// <summary>Opacity read each time the widget is drawn (for animation). Takes priority over <see cref="Value"/> when set.</summary>
    public Func<float>? ValueGetter { get; init; }

    internal float Resolve() => ValueGetter?.Invoke() ?? Value;

    // 単一子パススルー（tight なら子を充填・loose なら子の自然サイズ）。レイアウトは透過し描画のみ変える。
    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new OpacityElement(this);
}

/// <summary>The element that holds an <see cref="Opacity"/>.</summary>
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
/// Draws the child with a transformation (scale + translation + rotation) applied, equivalent to Flutter's
/// <c>Transform</c>. The corresponding <c>*Getter</c> properties are read every frame at draw time, enabling
/// animation. <see cref="Origin"/> is the pivot for scaling/rotation (default: center).
/// <b>Layout and hit-testing remain based on the original rectangle</b> (only drawing is transformed), which
/// makes this suitable for transition effects, spinners, and dials.
/// Rotation is intended for single pieces of content (icon/image/shape); rotating text, rotated clipping, and
/// nested non-uniform scale + rotation are not supported (the result is only an approximation).
/// </summary>
public sealed class Transform : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public float Scale { get; init; } = 1f;

    public Func<float>? ScaleGetter { get; init; }

    /// <summary>Rotation in radians (clockwise).</summary>
    public float Rotation { get; init; }

    /// <summary>Rotation read each time the widget is drawn (for animation). Takes priority over <see cref="Rotation"/> when set.</summary>
    public Func<float>? RotationGetter { get; init; }

    internal float ResolveRotation() => RotationGetter?.Invoke() ?? Rotation;

    /// <summary>Scale getter for the X axis only (falls back to uniform scaling via <see cref="Scale"/>/<see cref="ScaleGetter"/> if unspecified).</summary>
    public Func<float>? ScaleXGetter { get; init; }

    /// <summary>Scale getter for the Y axis only (falls back to uniform scaling if unspecified).</summary>
    public Func<float>? ScaleYGetter { get; init; }

    public float TranslateX { get; init; }

    public Func<float>? TranslateXGetter { get; init; }

    public float TranslateY { get; init; }

    public Func<float>? TranslateYGetter { get; init; }

    /// <summary>Pivot for scaling/rotation (defaults to <see cref="Alignment.Center"/>).</summary>
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

/// <summary>The element that holds a <see cref="Transform"/>.</summary>
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
