using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>A scrollable viewport entity (<see cref="ScrollView"/>/<see cref="ListView"/>/<see cref="GridView"/>) common operations.</summary>
internal interface IScrollable
{
    float ScrollOffset { get; }

    /// <summary>Scroll direction (determine main axis by gesture arbitration).</summary>
    Axis ScrollAxis { get; }

    void SetScroll(float offset);

    /// <summary>Scrolls the specified rectangle (such as the focus target) as much as necessary so that it is within the viewport (scroll-to-focus).</summary>
    void RevealRect(Rect target);

    /// <summary>Wheel/Trackpad = continuous inertial scrolling (adding speed).</summary>
    void ScrollByAnimated(float offsetDelta);

    /// <summary>Gamepad/scroll-to-focus = Glide smoothly to the specified offset (move the target amount).</summary>
    void AnimateScrollTo(float offset);

    /// <summary>Maximum scrollable offset (content − viewport, greater than or equal to 0).</summary>
    float MaxScroll { get; }

    /// <summary>Do you want to enable overscrolling (rubber band + return) at the edges?</summary>
    bool BounceEnabled { get; }

    /// <summary>Does it accept manual (drag/wheel) scrolling? <see cref="ScrollController"/>・Program control such as scroll-to-focus is possible.</summary>
    bool ManualScrollEnabled { get; }

    /// <summary>Scroll movement constants (sensitivity/following/rubber band/inertia). </summary>
    ScrollPhysics Physics { get; }
}

/// <summary>Common calculation for scroll-to-focus (move offset by the amount that target protrudes from viewport in the main axis direction).</summary>
internal static class ScrollReveal
{
    public static void Reveal(IScrollable scrollable, Rect viewport, Rect target)
    {
        bool vertical = scrollable.ScrollAxis == Axis.Vertical;
        float vpStart = vertical ? viewport.Y : viewport.X;
        float vpEnd = vertical ? viewport.Bottom : viewport.Right;
        float tStart = vertical ? target.Y : target.X;
        float tEnd = vertical ? target.Bottom : target.Right;

        if (tStart < vpStart)
        {
            scrollable.AnimateScrollTo(scrollable.ScrollOffset - (vpStart - tStart)); // 上/左へグライド
        }
        else if (tEnd > vpEnd)
        {
            scrollable.AnimateScrollTo(scrollable.ScrollOffset + (tEnd - vpEnd)); // 下/右へグライド
        }
    }
}

/// <summary>
/// Scroll position imperative controller (Flutter<c>ScrollController</c>thin version).
/// <see cref="ScrollView"/>/<see cref="ListView"/>give it to<see cref="JumpTo"/>Change position with /<see cref="Offset"/>Read at.
/// </summary>
public sealed class ScrollController
{
    internal IScrollable? Target { get; set; }

    /// <summary>Current scroll amount (main axis px, 0=start).</summary>
    public float Offset => Target?.ScrollOffset ?? 0f;

    /// <summary>Immediately sets the scroll amount (clamps outside the range).</summary>
    public void JumpTo(float offset) => Target?.SetScroll(offset);
}

/// <summary>
/// Viewport that scrolls along the main axis (Flutter<c>SingleChildScrollView</c>equivalent).
/// Measure and overhang<see cref="Axis"/>Scroll and clip in the direction. <see cref="Controller"/>
/// In programmatic.
/// </summary>
public sealed class ScrollView : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    /// <summary>Scroll direction (default<see cref="Axis.Vertical"/>）。</summary>
    public Axis Axis { get; init; } = Axis.Vertical;

    public ScrollController? Controller { get; init; }

    /// <summary>Overscroll at the edge (rubber band + return). </summary>
    public bool Bounce { get; init; } = true;

    /// <summary>
    /// Whether to enable manual scrolling (drag/wheel). <b>Disable user interaction scrolling</b>do
    /// （<see cref="Controller"/>of<see cref="ScrollController.JumpTo"/>・Scroll-to-focus etc.<b>Program control still possible</b>）。
    /// Flutter <c>NeverScrollableScrollPhysics</c> / iOS <c>isScrollEnabled=false</c>Quite a bit.
    /// </summary>
    public bool ManualScroll { get; init; } = true;

    /// <summary>Scroll movement constants (sensitivity/following/rubber band/inertia). <see cref="HamonTheme.ScrollPhysics"/>。</summary>
    public ScrollPhysics? Physics { get; init; }

    public Dimension Width { get; init; }

    public Dimension Height { get; init; }

    Style IRenderConfig.Style => new()
    {
        Kind = LayoutKind.Scroll,
        Direction = Axis,
        Width = Width,
        Height = Height,
    };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new ScrollViewElement(this);
}

/// <summary><see cref="ScrollView"/>holding entity. </summary>
internal sealed class ScrollViewElement : RenderElement, IScrollable
{
    private readonly DragScroller _drag;

    public ScrollViewElement(ScrollView widget)
        : base(widget)
    {
        _drag = new DragScroller(this);
    }

    /// <summary>Current scroll amount (principal px).</summary>
    public float ScrollOffset { get; private set; }

    Axis IScrollable.ScrollAxis => ((ScrollView)Widget).Axis;

    ScrollPhysics IScrollable.Physics => ((ScrollView)Widget).Physics ?? Context.Theme.ScrollPhysics;

    void IScrollable.SetScroll(float offset) => SetScroll(offset);

    void IScrollable.RevealRect(Rect target) => ScrollReveal.Reveal(this, LayoutNode.Bounds, target);

    void IScrollable.ScrollByAnimated(float offsetDelta) => _drag.ScrollBy(offsetDelta);

    void IScrollable.AnimateScrollTo(float offset) => _drag.GlideTo(offset);

    float IScrollable.MaxScroll => ComputeMaxScroll();

    bool IScrollable.BounceEnabled => ((ScrollView)Widget).Bounce;

    bool IScrollable.ManualScrollEnabled => ((ScrollView)Widget).ManualScroll;

    /// <summary>Current overscroll amount (for inspection/testing; 0=within bounds).</summary>
    internal float Overscroll => _drag.Overscroll;

    public override bool WantsPointer => true;

    private bool Vertical => ((ScrollView)Widget).Axis == Axis.Vertical;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        _drag.Owner = Context.Owner;
        Attach((ScrollView)Widget);
    }

    public override void Update(Widget newWidget)
    {
        var old = (ScrollView)Widget;
        base.Update(newWidget);
        var updated = (ScrollView)newWidget;
        if (!ReferenceEquals(old.Controller, updated.Controller))
        {
            if (old.Controller is not null && ReferenceEquals(old.Controller.Target, this))
            {
                old.Controller.Target = null;
            }

            Attach(updated);
        }

        LayoutNode.ScrollOffset = ScrollOffset; // 再構築後もスクロール量を保つ
    }

    public override void Unmount()
    {
        var widget = (ScrollView)Widget;
        if (widget.Controller is not null && ReferenceEquals(widget.Controller.Target, this))
        {
            widget.Controller.Target = null;
        }

        base.Unmount();
    }

    public override void HandlePointer(in PointerEvent pointer)
    {
        if (!((ScrollView)Widget).ManualScroll)
        {
            return; // 手動スクロール無効＝ドラッグを無視（プログラム制御は別経路で可能）
        }

        _drag.HandlePointer(pointer, Vertical ? pointer.Position.Y : pointer.Position.X);
    }

    public override void Paint(in PaintContext context)
    {
        object? previous = context.PushClip(LayoutNode.Bounds);
        float over = _drag.Overscroll;
        PaintContext inner = over == 0f
            ? context
            : context.WithTransform(Transform2D.Translation(Vertical ? new Vec2(0f, -over) : new Vec2(-over, 0f)));
        base.Paint(inner);
        context.PopClip(previous);
    }

    /// <summary>Set scroll amount (clamped to range [0, content-viewport]). </summary>
    internal void SetScroll(float offset)
    {
        float clamped = Math.Clamp(offset, 0f, ComputeMaxScroll());
        if (clamped == ScrollOffset)
        {
            return;
        }

        ScrollOffset = clamped;
        LayoutNode.ScrollOffset = clamped;
        // スクロールはウィジェットを作り変えない＝全ツリー再構築（MarkDirty）ではなく、この部分木の再レイアウトだけ要求する。
        // 全子を実体化済みの ScrollView では、再 Measure で ScrollOffset が反映されれば十分（ListView/GridView は仮想化で別経路）。
        // これで重いページ（ダッシュボード等）の慣性スクロール中も Build を回さずゼロアロケで滑らかに動く。
        Context.Owner?.MarkElementDirty(this);
    }

    private float ComputeMaxScroll()
    {
        IReadOnlyList<LayoutNode> children = LayoutNode.Children;
        if (children.Count == 0)
        {
            return 0f;
        }

        float content = Vertical ? children[0].Size.Height : children[0].Size.Width;
        float viewport = Vertical ? LayoutNode.Size.Height : LayoutNode.Size.Width;
        return Math.Max(0f, content - viewport);
    }

    private void Attach(ScrollView widget)
    {
        if (widget.Controller is not null)
        {
            widget.Controller.Target = this;
        }
    }
}
