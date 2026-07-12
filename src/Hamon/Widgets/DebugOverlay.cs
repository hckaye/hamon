using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Runtime statistics panel for development (a lightweight equivalent to Flutter's performance overlay).
/// Draws the number of overlays, focused node, and average FPS every frame.
/// Expected to be removed in production build (debug only).
/// </summary>
public sealed class DebugOverlay : Widget
{
    public Color? Background { get; init; }

    public Color? TextColor { get; init; }

    public float FontSize { get; init; } = 12f;

    public float Width { get; init; } = 200f;

    public override Element CreateElement() => new DebugOverlayElement(this);
}

/// <summary>The element that holds a <see cref="DebugOverlay"/>.</summary>
internal sealed class DebugOverlayElement : Element
{
    private const float LineHeight = 16f;
    private const float Pad = 6f;
    private const int Lines = 6;

    private readonly LayoutNode _node;

    public DebugOverlayElement(DebugOverlay widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private DebugOverlay W => (DebugOverlay)Widget;

    public override LayoutNode LayoutNode => _node;

    public override void Paint(in PaintContext context)
    {
        if (Context.Owner is not HamonRoot host || Context.Text is not ITextRenderer text)
        {
            return;
        }

        Rect b = _node.Bounds;
        context.FillRoundedRect(b, W.Background ?? new Color(0, 0, 0, 180), 6f);

        Color color = W.TextColor ?? new Color(120, 255, 160);
        float avg = AvgFrame(host);
        int fps = avg > 0.0001f ? (int)(1f / avg) : 0;
        string focus = host.Focus.Focused is FocusNode f ? (f.SemanticLabel ?? $"id={f.Id}") : "(none)";

        DrawLine(context, text, color, b, 0, $"fps:      {fps}");
        DrawLine(context, text, color, b, 1, $"elements: {host.ElementCount}");
        DrawLine(context, text, color, b, 2, $"tickers:  {host.ActiveTickerCount}");
        DrawLine(context, text, color, b, 3, $"overlays: {host.OverlayCount}");
        DrawLine(context, text, color, b, 4, $"focus:    {focus}");
        DrawLine(context, text, color, b, 5, $"cursor:   {host.CurrentCursor}");
    }

    private float _lastElapsed;
    private float _avg = 1f / 60f;

    // 平均フレーム時間（指数移動平均）。Paint 間の経過から推定。
    private float AvgFrame(HamonRoot host)
    {
        float now = host.ElapsedSeconds;
        float dt = now - _lastElapsed;
        _lastElapsed = now;
        if (dt > 0f && dt < 1f)
        {
            _avg += (dt - _avg) * 0.1f;
        }

        return _avg;
    }

    private void DrawLine(in PaintContext context, ITextRenderer text, Color color, Rect b, int line, string s)
    {
        Vec2 pos = context.ApplyTransform(new Vec2(b.X + Pad, b.Y + Pad + (line * LineHeight)));
        text.Draw(s, pos, W.FontSize * context.ScaleY, context.ApplyOpacity(color));
    }

    private Size MeasureSelf(BoxConstraints constraints) => new(W.Width, (Lines * LineHeight) + (2f * Pad));
}
