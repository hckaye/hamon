using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Circular progress (Flutter<c>CircularProgressIndicator</c>equivalent).<see cref="Value"/>(0..1)<b>Confirmed</b>
/// (track circle + progress arc), if unspecified (null)<b>Uncertain</b>(Spinner whose arc keeps spinning) = Same as Flutter Switches depending on the presence or absence of value.
/// <see cref="ValueGetter"/>Read the progress when drawing with (Bind/anime binding/ZeroAlloc extension = fixed when setting<see cref="Value"/>priority).
/// Cooldown arc/donut display (<see cref="StartRadians"/>).
/// </summary>
public sealed class CircularProgressIndicator : Widget
{
    /// <summary>Progress 0..1. <c>value</c>Same as.</summary>
    public float? Value { get; init; }

    /// <summary>Read the progress when drawing (when setting = confirmed/<see cref="Value"/>priority). </summary>
    public Func<float>? ValueGetter { get; init; }

    /// <summary>Progress arc color (Flutter<c>color</c>/<c>valueColor</c>・Unspecified theme<see cref="HamonTheme.Primary"/>）。</summary>
    public Color? Color { get; init; }

    /// <summary>Track (background circle) color (Flutter<c>backgroundColor</c>・Unspecified theme<see cref="HamonTheme.SurfaceVariant"/>）。</summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>Line thickness (Flutter<c>strokeWidth</c>・Default 4).</summary>
    public float StrokeWidth { get; init; } = 4f;

    /// <summary>Outer diameter (px). </summary>
    public float Diameter { get; init; } = 36f;

    /// <summary>The starting angle of the definite arc (in radians, default = directly above -90°). </summary>
    public float StartRadians { get; init; } = -MathF.PI / 2f;

    /// <summary>Image skin to spin when uncertain (for games = any spinner sprite). </summary>
    public ImageSkin Sprite { get; init; }

    /// <summary>Indeterminate Number of seconds the spinner takes to complete one revolution.</summary>
    public float PeriodSeconds { get; init; } = 1.1f;

    public override Element CreateElement() => new CircularProgressIndicatorElement(this);
}

internal sealed class CircularProgressIndicatorElement : Element, ITicker
{
    private const float Sweep = MathF.PI * 1.5f; // 不確定の弧（約3/4周）
    private readonly LayoutNode _node;
    private float _phase; // 0..1（不確定の回転位相）

    public CircularProgressIndicatorElement(CircularProgressIndicator widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private CircularProgressIndicator W => (CircularProgressIndicator)Widget;

    public override LayoutNode LayoutNode => _node;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        Context.Owner?.RegisterTicker(this); // 不確定の回転用（確定でも位相を進めるだけ＝無害・アロケなし）
    }

    public override void Unmount()
    {
        Context.Owner?.UnregisterTicker(this);
        base.Unmount();
    }

    public bool Tick(float dt)
    {
        float period = W.PeriodSeconds <= 0f ? 1f : W.PeriodSeconds;
        _phase += dt / period;
        if (_phase >= 1f)
        {
            _phase -= MathF.Floor(_phase);
        }

        return true; // 回り続ける
    }

    public override void Paint(in PaintContext context)
    {
        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;
        var center = new Vec2(b.X + (b.Width / 2f), b.Y + (b.Height / 2f));
        float r = (Math.Min(b.Width, b.Height) - W.StrokeWidth) / 2f;

        float? value = W.ValueGetter is not null ? W.ValueGetter() : W.Value;
        if (value is null)
        {
            // 不確定（スピナー）。
            if (W.Sprite.HasValue)
            {
                W.Sprite.Paint(context.WithRotation(_phase * MathF.PI * 2f, center), b);
                return;
            }

            float start = _phase * MathF.PI * 2f;
            context.Arc(center, r, start, start + Sweep, W.StrokeWidth, W.Color ?? theme.Primary, 24);
            return;
        }

        // 確定（トラック円＋進捗弧）。
        float v = Math.Clamp(value.Value, 0f, 1f);
        context.Arc(center, r, 0f, MathF.PI * 2f, W.StrokeWidth, W.BackgroundColor ?? theme.SurfaceVariant, 48);
        if (v > 0f)
        {
            context.Arc(center, r, W.StartRadians, W.StartRadians + (MathF.PI * 2f * v), W.StrokeWidth, W.Color ?? theme.Primary, Math.Max(2, (int)(48 * v)));
        }
    }

    private Size MeasureSelf(BoxConstraints constraints) => new(W.Diameter, W.Diameter);
}
