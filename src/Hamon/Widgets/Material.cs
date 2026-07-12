using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A Material-like surface (the equivalent of Flutter's <c>Material</c>). Uses
/// <see cref="Elevation"/> to cast a drop shadow behind the surface, paints
/// <see cref="Color"/> (or a gradient via <see cref="GradientTo"/>), and layers the
/// child on top (filling it if the constraints are tight). Painting order is
/// <b>shadow → surface → child</b>.
/// </summary>
public sealed class Material : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    /// <summary>Surface color (falls back to the theme's <see cref="HamonTheme.Surface"/> if unspecified).</summary>
    public Color? Color { get; init; }

    /// <summary>Reads the surface color on every paint (useful for blink/flash animations without rebuilding the widget). Takes priority over <see cref="Color"/> when set.</summary>
    public Func<Color>? ColorGetter { get; init; }

    /// <summary>Corner radius (px, default 0 = rectangle).</summary>
    public float Radius { get; init; }

    /// <summary>Shadow elevation level (0 = no shadow). The blur and offset are determined by <see cref="HamonTheme.ShadowBlurPerElevation"/> and <see cref="HamonTheme.ShadowOffsetPerElevation"/>.</summary>
    public float Elevation { get; init; }

    /// <summary>Shadow color (falls back to the theme's <see cref="HamonTheme.Shadow"/> if unspecified).</summary>
    public Color? ShadowColor { get; init; }

    /// <summary>When set, paints the surface as a two-color gradient from <see cref="Color"/> to this color.</summary>
    public Color? GradientTo { get; init; }

    /// <summary>Gradient direction (default = top → bottom).</summary>
    public GradientAxis GradientAxis { get; init; } = GradientAxis.Vertical;

    /// <summary>Surface image skin (a 9-slice frame/panel for sprite games), painted instead of the color or gradient fill. The shadow from <see cref="Elevation"/> is still applied.</summary>
    public ImageSkin Skin { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };

    Color? IRenderConfig.Background => null; // 背景は自前で描く（影→グラデ/単色の順）

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new MaterialElement(this);
}

/// <summary>The <see cref="Element"/> that backs a <see cref="Material"/> widget.</summary>
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
/// A Material card (the equivalent of Flutter's <c>Card</c>). A convenience wrapper
/// around <see cref="Material"/> with default rounded corners, a shadow, and inner padding.
/// </summary>
public sealed class Card : StatelessWidget
{
    public Widget? Child { get; init; }

    public Color? Color { get; init; }

    /// <summary>Shadow elevation level (default 1).</summary>
    public float Elevation { get; init; } = 1f;

    /// <summary>Corner radius (falls back to the theme's <see cref="HamonTheme.Radius"/> if unspecified).</summary>
    public float? Radius { get; init; }

    /// <summary>Inner padding (defaults to the theme's <see cref="HamonTheme.SpacingM"/> on all sides).</summary>
    public EdgeInsets? Padding { get; init; }

    /// <summary>Surface image skin (a 9-slice frame/panel for games).</summary>
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

/// <summary>A surface painted with a multi-stop linear gradient (see <see cref="PaintContext.FillGradientStops"/>).</summary>
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

/// <summary>A surface painted with a two-color gradient (a convenience wrapper around <see cref="Material"/>'s gradient support).</summary>
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
