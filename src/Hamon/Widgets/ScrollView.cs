using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Common operations shared by scrollable viewport elements (<see cref="ScrollView"/>/<see cref="ListView"/>/<see cref="GridView"/>).</summary>
internal interface IScrollable
{
    float ScrollOffset { get; }

    /// <summary>Scroll direction, used to determine the main axis for gesture arbitration.</summary>
    Axis ScrollAxis { get; }

    void SetScroll(float offset);

    /// <summary>Scrolls just enough to bring the specified rectangle (e.g. a focus target) within the viewport (scroll-to-focus).</summary>
    void RevealRect(Rect target);

    /// <summary>Applies a wheel/trackpad delta as continuous inertial scrolling (adds to the current velocity).</summary>
    void ScrollByAnimated(float offsetDelta);

    /// <summary>Used for gamepad navigation/scroll-to-focus: glides smoothly to the specified offset.</summary>
    void AnimateScrollTo(float offset);

    /// <summary>Maximum scrollable offset (content − viewport, greater than or equal to 0).</summary>
    float MaxScroll { get; }

    /// <summary>Whether overscroll (rubber band + return) is enabled at the edges.</summary>
    bool BounceEnabled { get; }

    /// <summary>Whether manual (drag/wheel) scrolling is accepted. Programmatic control — via <see cref="ScrollController"/>, scroll-to-focus, etc. — remains possible either way.</summary>
    bool ManualScrollEnabled { get; }

    /// <summary>Scroll movement constants (sensitivity/follow speed/rubber band/inertia).</summary>
    ScrollPhysics Physics { get; }
}

/// <summary>Shared scroll-to-focus calculation: moves the offset by however much the target protrudes from the viewport along the main axis.</summary>
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
/// An imperative controller for scroll position (a thin version of Flutter's <c>ScrollController</c>). Pass it to
/// a <see cref="ScrollView"/> / <see cref="ListView"/>; change the position with <see cref="JumpTo"/> and read it
/// with <see cref="Offset"/>.
/// </summary>
public sealed class ScrollController
{
    internal IScrollable? Target { get; set; }

    /// <summary>Current scroll amount (main axis px, 0=start).</summary>
    public float Offset => Target?.ScrollOffset ?? 0f;

    /// <summary>Immediately sets the scroll amount (values outside the valid range are clamped).</summary>
    public void JumpTo(float offset) => Target?.SetScroll(offset);
}

/// <summary>
/// A viewport that scrolls along its main axis (equivalent to Flutter's <c>SingleChildScrollView</c>). The child is
/// measured, and any overhang is scrolled and clipped along <see cref="Axis"/>. Use <see cref="Controller"/> for
/// programmatic control.
/// </summary>
public sealed class ScrollView : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    /// <summary>Scroll direction (default <see cref="Axis.Vertical"/>).</summary>
    public Axis Axis { get; init; } = Axis.Vertical;

    public ScrollController? Controller { get; init; }

    /// <summary>Whether overscroll (rubber band + return) is enabled at the edges.</summary>
    public bool Bounce { get; init; } = true;

    /// <summary>
    /// Whether manual scrolling (drag/wheel) is enabled. Set to false to <b>disable user-interaction scrolling</b>
    /// (<see cref="ScrollController.JumpTo"/> on <see cref="Controller"/>, scroll-to-focus, etc. — <b>programmatic
    /// control is still possible</b>). Roughly equivalent to Flutter's <c>NeverScrollableScrollPhysics</c> / iOS's
    /// <c>isScrollEnabled = false</c>.
    /// </summary>
    public bool ManualScroll { get; init; } = true;

    /// <summary>Scroll movement constants (sensitivity/follow speed/rubber band/inertia). Defaults to <see cref="HamonTheme.ScrollPhysics"/> if unspecified.</summary>
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

/// <summary>The element that backs <see cref="ScrollView"/>.</summary>
internal sealed class ScrollViewElement : RenderElement, IScrollable
{
    private readonly DragScroller _drag;

    public ScrollViewElement(ScrollView widget)
        : base(widget)
    {
        _drag = new DragScroller(this);
    }

    /// <summary>Current scroll amount (main-axis px).</summary>
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
