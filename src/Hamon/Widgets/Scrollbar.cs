using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A drawing delegate that replaces the appearance of the scrollbar knob/track (for games: 9-slice, sprite, or
/// custom drawing). <paramref name="rect"/> is the target rectangle (the thumb's position/size are already
/// resolved), <paramref name="opacity"/> is the fade opacity, and <paramref name="active"/> indicates whether it is
/// being hovered/dragged. You can draw it in one line with <see cref="PaintContext.DrawNineSlice"/>.
/// </summary>
public delegate void ScrollbarPartRenderer(in PaintContext context, Rect rect, float opacity, bool active);

/// <summary>
/// A scrollbar (equivalent to Flutter's <c>Scrollbar</c>). Wraps <see cref="Child"/> (a <see cref="ScrollView"/> /
/// <see cref="ListView"/> / <see cref="GridView"/>) and overlays the knob on the trailing edge (vertical = right,
/// horizontal = bottom). The knob can be dragged to scroll (<see cref="Interactive"/>), and it automatically fades
/// out when idle unless <see cref="ThumbVisibility"/> keeps it always visible.
///
/// <para>The API mirrors Flutter's (<c>thumbVisibility</c> / <c>trackVisibility</c> / <c>interactive</c> /
/// <c>thickness</c> / <c>radius</c>). However, since there is no equivalent to Flutter's
/// <c>PrimaryScrollController</c> / ScrollNotification mechanism, <see cref="Controller"/> is <b>required</b>: it
/// must be the same <see cref="ScrollController"/> that wraps the scrollable being tracked (this is also a common
/// pattern in Flutter).</para>
///
/// The thumb's drawing and fade state read and write <see cref="Controller"/> at paint/ticker time (zero allocation
/// on steady frames).
/// </summary>
public sealed class Scrollbar : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    /// <summary>The <see cref="ScrollController"/> shared with the wrapped scrollable (required).</summary>
    public required ScrollController Controller { get; init; }

    /// <summary>Bar orientation (default <see cref="Axis.Vertical"/> = right edge). Corresponds to Flutter's <c>axis</c> parameter.</summary>
    public Axis Axis { get; init; } = Axis.Vertical;

    /// <summary>Whether to always display the knob (default false = auto-fade after interaction). Corresponds to Flutter's <c>thumbVisibility</c>.</summary>
    public bool ThumbVisibility { get; init; }

    /// <summary>Whether to display the track (background groove) (default false). Corresponds to Flutter's <c>trackVisibility</c>.</summary>
    public bool TrackVisibility { get; init; }

    /// <summary>Whether the knob can be operated by dragging or tapping the track (default true). Corresponds to Flutter's <c>interactive</c>.</summary>
    public bool Interactive { get; init; } = true;

    /// <summary>Bar thickness in px (default 8). Corresponds to Flutter's <c>thickness</c>.</summary>
    public float Thickness { get; init; } = 8f;

    /// <summary>Corner radius of the knob (defaults to half the thickness = a rounded pill shape). Corresponds to Flutter's <c>radius</c>.</summary>
    public float? Radius { get; init; }

    /// <summary>Thumb color (defaults to theme <see cref="HamonTheme.OnSurfaceVariant"/> if unspecified).</summary>
    public Color? ThumbColor { get; init; }

    /// <summary>Track color (defaults to theme <see cref="HamonTheme.SurfaceVariant"/> if unspecified).</summary>
    public Color? TrackColor { get; init; }

    /// <summary>
    /// Fixes the length of the knob along the main axis, in px. <b>Null (the default) makes it proportional to the
    /// scrollable content.</b> Setting a value gives it a <b>fixed size regardless of content</b>, useful for a
    /// static handle (e.g. a game-style scroll knob).
    /// </summary>
    public float? ThumbExtent { get; init; }

    /// <summary>Replaces the appearance of the knob (9-slice/sprite/custom drawing). Takes priority over <see cref="ThumbColor"/> / <see cref="Radius"/>.</summary>
    public ScrollbarPartRenderer? ThumbRenderer { get; init; }

    /// <summary>Replaces the appearance of the track (9-slice/image/custom drawing). Takes priority over <see cref="TrackVisibility"/> / <see cref="TrackColor"/> (always drawn when set).</summary>
    public ScrollbarPartRenderer? TrackRenderer { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };

    IReadOnlyList<Widget>? IRenderConfig.Children
    {
        get
        {
            // 末尾辺へ固定幅/高のストリップを重ねる（HitTest は末尾子＝最前面から走るので、つまみ領域だけポインタを奪える）。
            var bar = new ScrollbarBar(this);
            Widget strip = Axis == Axis.Vertical
                ? new Positioned { Top = Dimension.Px(0f), Bottom = Dimension.Px(0f), Right = Dimension.Px(0f), Width = Dimension.Px(Thickness), Child = bar }
                : new Positioned { Left = Dimension.Px(0f), Right = Dimension.Px(0f), Bottom = Dimension.Px(0f), Height = Dimension.Px(Thickness), Child = bar };
            return Child is null ? new[] { strip } : new[] { Child, strip };
        }
    }

    Color? IRenderConfig.Background => null;

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>The trailing-edge strip element (internal to <see cref="Scrollbar"/>).</summary>
internal sealed class ScrollbarBar : Widget
{
    public ScrollbarBar(Scrollbar config) => Config = config;

    public Scrollbar Config { get; }

    public override Element CreateElement() => new ScrollbarBarElement(this);
}

internal sealed class ScrollbarBarElement : Element, ITicker, IHoverTarget
{
    private const float MinThumb = 24f;       // つまみ最小長
    private const float Inset = 2f;           // ストリップ内のつまみ余白
    private const float ShowSeconds = 1.1f;   // 操作後に完全表示を保つ秒数
    private const float FadeSeconds = 0.35f;  // フェードに要する秒数

    private readonly LayoutNode _node;
    private float _visibleTimer;
    private float _lastOffset;
    private bool _dragging;
    private float _grab;
    private bool _hovered;

    public ScrollbarBarElement(ScrollbarBar widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private ScrollbarBar W => (ScrollbarBar)Widget;

    private Scrollbar C => W.Config;

    private IScrollable? Scrollable => C.Controller.Target;

    public override LayoutNode LayoutNode => _node;

    // スクロール余地があり操作可能なときだけポインタを奪う（content が収まるなら細いストリップも素通り）。
    public override bool WantsPointer => C.Interactive && (Scrollable?.MaxScroll ?? 0f) > 0f;

    bool IHoverTarget.HoverOpaque => false; // 細いので背後の hover は遮らない

    MouseCursor IHoverTarget.HoverCursor => MouseCursor.Basic;

    void IHoverTarget.HoverEnter(Vec2 position) => _hovered = true;

    void IHoverTarget.HoverMove(Vec2 position)
    {
    }

    void IHoverTarget.HoverExit(Vec2 position) => _hovered = false;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        _lastOffset = Scrollable?.ScrollOffset ?? 0f;
        _visibleTimer = C.ThumbVisibility ? ShowSeconds + FadeSeconds : 0f;
        Context.Owner?.RegisterTicker(this);
    }

    public override void Unmount()
    {
        Context.Owner?.UnregisterTicker(this);
        (Context.Owner as HamonRoot)?.NotifyHoverTargetUnmounted(this);
        base.Unmount();
    }

    public bool Tick(float dtSeconds)
    {
        float offset = Scrollable?.ScrollOffset ?? 0f;
        if (offset != _lastOffset)
        {
            _lastOffset = offset;
            _visibleTimer = ShowSeconds + FadeSeconds; // スクロールしたら再表示
        }
        else if (!_dragging && !_hovered && !C.ThumbVisibility)
        {
            _visibleTimer = MathF.Max(0f, _visibleTimer - dtSeconds);
        }

        return true; // フェード/操作を追い続ける
    }

    private float CurrentAlpha()
    {
        if (C.ThumbVisibility || _dragging || _hovered)
        {
            return 1f;
        }

        return Math.Clamp(_visibleTimer / FadeSeconds, 0f, 1f);
    }

    public override void HandlePointer(in PointerEvent pointer)
    {
        if (Scrollable is not IScrollable sc)
        {
            return;
        }

        float maxScroll = sc.MaxScroll;
        if (maxScroll <= 0f)
        {
            return;
        }

        Rect b = LayoutNode.Bounds;
        bool vertical = C.Axis == Axis.Vertical;
        float track = vertical ? b.Height : b.Width;
        float thumbExtent = ComputeThumbExtent(track, maxScroll);
        float span = MathF.Max(1f, track - thumbExtent);
        float along = (vertical ? pointer.Position.Y - b.Y : pointer.Position.X - b.X);
        float thumbStart = (sc.ScrollOffset / maxScroll) * span;

        switch (pointer.Phase)
        {
            case PointerPhase.Down:
                _visibleTimer = ShowSeconds + FadeSeconds;
                if (along >= thumbStart && along <= thumbStart + thumbExtent)
                {
                    _dragging = true;
                    _grab = along - thumbStart; // つまみ内の掴み位置を保つ（ドラッグで相対移動）
                }
                else
                {
                    _dragging = true;
                    _grab = thumbExtent / 2f; // トラックタップ＝つまみ中心をそこへ
                    SetFrom(sc, along, span, maxScroll);
                }

                break;

            case PointerPhase.Move when _dragging:
                _visibleTimer = ShowSeconds + FadeSeconds;
                SetFrom(sc, along, span, maxScroll);
                break;

            case PointerPhase.Up:
            case PointerPhase.Cancel:
                _dragging = false;
                break;
        }
    }

    // 掴み位置 _grab を保ったままポインタ位置から offset を決める（つまみドラッグ＝相対・トラックタップ＝中心合わせ）。
    private void SetFrom(IScrollable sc, float along, float span, float maxScroll)
    {
        float start = Math.Clamp(along - _grab, 0f, span);
        sc.SetScroll(start / span * maxScroll);
    }

    public override void Paint(in PaintContext context)
    {
        if (Scrollable is not IScrollable sc)
        {
            return;
        }

        float maxScroll = sc.MaxScroll;
        if (maxScroll <= 0f)
        {
            return; // スクロール余地なし＝バーを出さない
        }

        float alpha = CurrentAlpha();
        if (alpha <= 0f)
        {
            return;
        }

        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;
        bool vertical = C.Axis == Axis.Vertical;
        bool active = _hovered || _dragging;
        float track = vertical ? b.Height : b.Width;
        float thumbExtent = ComputeThumbExtent(track, maxScroll);
        float span = MathF.Max(1f, track - thumbExtent);
        float thumbStart = (sc.ScrollOffset / maxScroll) * span;
        float thickness = vertical ? b.Width : b.Height;
        float radius = C.Radius ?? thickness / 2f;

        // トラック：Renderer 指定なら任意描画（9-slice/画像）、なければ TrackVisibility で角丸を塗る。
        if (C.TrackRenderer is ScrollbarPartRenderer trackRenderer)
        {
            trackRenderer(context, b, alpha, active);
        }
        else if (C.TrackVisibility)
        {
            context.FillRoundedRect(b, WithAlpha(C.TrackColor ?? theme.SurfaceVariant, alpha * 0.6f), radius);
        }

        Rect thumb = vertical
            ? new Rect(b.X + Inset, b.Y + thumbStart, MathF.Max(1f, b.Width - (2f * Inset)), thumbExtent)
            : new Rect(b.X + thumbStart, b.Y + Inset, thumbExtent, MathF.Max(1f, b.Height - (2f * Inset)));

        // つまみ：Renderer 指定なら任意描画（9-slice/スプライト/固定ハンドル）、なければ角丸を塗る。
        if (C.ThumbRenderer is ScrollbarPartRenderer thumbRenderer)
        {
            thumbRenderer(context, thumb, alpha, active);
        }
        else
        {
            context.FillRoundedRect(thumb, WithAlpha(C.ThumbColor ?? theme.OnSurfaceVariant, alpha * (active ? 0.9f : 0.7f)), radius);
        }
    }

    private float ComputeThumbExtent(float track, float maxScroll)
    {
        if (C.ThumbExtent is float fixedExtent)
        {
            return Math.Clamp(fixedExtent, MinThumb, track); // 内容量に依らず固定サイズ
        }

        float content = track + maxScroll;
        return Math.Clamp(track * track / content, MinThumb, track); // 比例（既定）
    }

    private static Color WithAlpha(Color c, float factor) => new(c.R, c.G, c.B, (int)Math.Clamp(c.A * factor, 0f, 255f));

    private Size MeasureSelf(BoxConstraints constraints)
    {
        float w = float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : C.Thickness;
        float h = float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight : C.Thickness;
        return new Size(w, h);
    }
}
