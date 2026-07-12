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
        if (fps != _lastFps)
        {
            _lastFps = fps;
            _fpsLine = $"fps:      {fps}";
        }

        int elementCount = host.ElementCount;
        if (elementCount != _lastElementCount)
        {
            _lastElementCount = elementCount;
            _elementsLine = $"elements: {elementCount}";
        }

        int tickerCount = host.ActiveTickerCount;
        if (tickerCount != _lastTickerCount)
        {
            _lastTickerCount = tickerCount;
            _tickersLine = $"tickers:  {tickerCount}";
        }

        int overlayCount = host.OverlayCount;
        if (overlayCount != _lastOverlayCount)
        {
            _lastOverlayCount = overlayCount;
            _overlaysLine = $"overlays: {overlayCount}";
        }

        FocusNode? focused = host.Focus.Focused;
        if (!ReferenceEquals(focused, _lastFocusNode) || focused?.SemanticLabel != _lastFocusLabel || (focused?.Id ?? -1) != _lastFocusId)
        {
            _lastFocusNode = focused;
            _lastFocusLabel = focused?.SemanticLabel;
            _lastFocusId = focused?.Id ?? -1;
            string focus = focused is FocusNode f ? (f.SemanticLabel ?? $"id={f.Id}") : "(none)";
            _focusLine = $"focus:    {focus}";
        }

        MouseCursor cursor = host.CurrentCursor;
        if (cursor != _lastCursor)
        {
            _lastCursor = cursor;
            _cursorLine = $"cursor:   {cursor}";
        }

        DrawLine(context, text, color, b, 0, _fpsLine);
        DrawLine(context, text, color, b, 1, _elementsLine);
        DrawLine(context, text, color, b, 2, _tickersLine);
        DrawLine(context, text, color, b, 3, _overlaysLine);
        DrawLine(context, text, color, b, 4, _focusLine);
        DrawLine(context, text, color, b, 5, _cursorLine);
    }

    private int _lastFps = -1;
    private string _fpsLine = "fps:      0";

    private int _lastElementCount = -1;
    private string _elementsLine = "elements: 0";

    private int _lastTickerCount = -1;
    private string _tickersLine = "tickers:  0";

    private int _lastOverlayCount = -1;
    private string _overlaysLine = "overlays: 0";

    private FocusNode? _lastFocusNode;
    private string? _lastFocusLabel;
    private int _lastFocusId = -1;
    private string _focusLine = "focus:    (none)";

    private MouseCursor _lastCursor = MouseCursor.Basic;
    private string _cursorLine = "cursor:   Basic";

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
