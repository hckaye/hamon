using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Material-like surface (Flutter<c>Material</c>equivalent).<see cref="Elevation"/>to cast a drop shadow behind,
/// <see cref="Color"/>(or grade<see cref="GradientTo"/>) and layer the child inside.
/// (If tight, fill the child).<b>shadow → face → child</b>Draw in this order.
/// </summary>
public sealed class Material : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    /// <summary>Surface color (unspecified and theme<see cref="HamonTheme.Surface"/>）。</summary>
    public Color? Color { get; init; }

    /// <summary>Read the color of the surface each time it is drawn (blinking/flashing animation = no reconstruction). <see cref="Color"/>More priority.</summary>
    public Func<Color>? ColorGetter { get; init; }

    /// <summary>Corner radius (px, default 0 = rectangle).</summary>
    public float Radius { get; init; }

    /// <summary>Shadow step (0=no shadow). <see cref="HamonTheme.ShadowBlurPerElevation"/>/<see cref="HamonTheme.ShadowOffsetPerElevation"/>The smudge/offset is determined.</summary>
    public float Elevation { get; init; }

    /// <summary>Shadow color (unspecified and theme<see cref="HamonTheme.Shadow"/>）。</summary>
    public Color? ShadowColor { get; init; }

    /// <summary>When set<see cref="Color"/>→Paint the surface with this two-color gradation.</summary>
    public Color? GradientTo { get; init; }

    /// <summary>Gradient direction (default = top → bottom).</summary>
    public GradientAxis GradientAxis { get; init; } = GradientAxis.Vertical;

    /// <summary>Surface image skin (9-slice frame/panel for sprite games). <see cref="Elevation"/>).</summary>
    public ImageSkin Skin { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };

    Color? IRenderConfig.Background => null; // 背景は自前で描く（影→グラデ/単色の順）

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new MaterialElement(this);
}

/// <summary><see cref="Material"/>holding entity. </summary>
internal sealed class MaterialElement : RenderElement
{
    public MaterialElement(Material widget)
        : base(widget)
    {
    }

    public override void Paint(in PaintContext context)
    {
        var w = (Material)Widget;
        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;

        if (w.Elevation > 0f)
        {
            float blur = 2f + (w.Elevation * theme.ShadowBlurPerElevation);
            float dy = w.Elevation * theme.ShadowOffsetPerElevation;
            context.DrawShadow(new Rect(b.X, b.Y + dy, b.Width, b.Height), w.ShadowColor ?? theme.Shadow, w.Radius, blur);
        }

        if (w.Skin.HasValue)
        {
            w.Skin.Paint(context, b); // 画像スキン（9-slice 枠）。色/グラデより優先
        }
        else
        {
            Color bg = w.ColorGetter?.Invoke() ?? w.Color ?? theme.Surface;
            if (w.GradientTo is Color to)
            {
                context.FillGradient(b, bg, to, w.GradientAxis);
            }
            else if (w.Radius > 0f)
            {
                context.FillRoundedRect(b, bg, w.Radius);
            }
            else
            {
                context.FillRect(b, bg);
            }
        }

        base.Paint(context); // 子（IRenderConfig.Background は null なので子のみ）
    }
}

/// <summary>
/// Material Card (Flutter<c>Card</c>equivalent).<see cref="Material"/>A convenient wrapper with default rounded corners, shadows, and inner margins.
/// </summary>
public sealed class Card : StatelessWidget
{
    public Widget? Child { get; init; }

    public Color? Color { get; init; }

    /// <summary>Shadow step (default 1).</summary>
    public float Elevation { get; init; } = 1f;

    /// <summary>Corner radius (unspecified and theme<see cref="HamonTheme.Radius"/>）。</summary>
    public float? Radius { get; init; }

    /// <summary>Inner margin (default = theme's<see cref="HamonTheme.SpacingM"/>equivalent).</summary>
    public EdgeInsets? Padding { get; init; }

    /// <summary>Surface image skin (9-slice frame/panel for games). </summary>
    public ImageSkin Skin { get; init; }

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        return new Material
        {
            Color = Color ?? theme.Surface,
            Radius = Radius ?? theme.Radius,
            Elevation = Elevation,
            Skin = Skin,
            Child = new Container
            {
                Padding = Padding ?? EdgeInsets.All(theme.SpacingM),
                Child = Child,
            },
        };
    }
}

/// <summary>Surface to be painted with multi-stop linear gradation (<see cref="PaintContext.FillGradientStops"/>). </summary>
public sealed class LinearGradientBox : Widget, IRenderConfig
{
    public required GradientStop[] Stops { get; init; }

    public GradientAxis Axis { get; init; } = GradientAxis.Vertical;

    public Widget? Child { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };

    Color? IRenderConfig.Background => null;

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new LinearGradientBoxElement(this);
}

internal sealed class LinearGradientBoxElement : RenderElement
{
    public LinearGradientBoxElement(LinearGradientBox widget)
        : base(widget)
    {
    }

    public override void Paint(in PaintContext context)
    {
        var w = (LinearGradientBox)Widget;
        context.FillGradientStops(LayoutNode.Bounds, w.Stops, w.Axis);
        base.Paint(context); // 子
    }
}

/// <summary>Surface to be painted with two-color gradation (<see cref="Material"/>Convenient wrapper for specifying gradation). </summary>
public sealed class GradientBox : StatelessWidget
{
    public Widget? Child { get; init; }

    public required Color From { get; init; }

    public required Color To { get; init; }

    public GradientAxis Axis { get; init; } = GradientAxis.Vertical;

    public float Elevation { get; init; }

    public override Widget Build(BuildContext context) => new Material
    {
        Color = From,
        GradientTo = To,
        GradientAxis = Axis,
        Elevation = Elevation,
        Child = Child,
    };
}
