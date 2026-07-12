using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Mouse pointer enters area (Flutter<c>PointerEnterEvent</c>equivalent).<see cref="MouseRegion.OnEnter"/>Delivery to.
/// </summary>
public readonly struct PointerEnterEvent
{
    public PointerEnterEvent(Vec2 position) => Position = position;

    /// <summary>UI coordinates (px).</summary>
    public Vec2 Position { get; }
}

/// <summary>
/// The mouse pointer moved within the area (Flutter<c>PointerHoverEvent</c>equivalent).<see cref="MouseRegion.OnHover"/>Delivery to.
/// Movement without pressing = hover (does not occur with touch = mouse only).
/// </summary>
public readonly struct PointerHoverEvent
{
    public PointerHoverEvent(Vec2 position) => Position = position;

    /// <summary>UI coordinates (px).</summary>
    public Vec2 Position { get; }
}

/// <summary>
/// Mouse pointer leaves area (Flutter<c>PointerExitEvent</c>equivalent).<see cref="MouseRegion.OnExit"/>Delivery to.
/// </summary>
public readonly struct PointerExitEvent
{
    public PointerExitEvent(Vec2 position) => Position = position;

    /// <summary>UI coordinates (px) at the time the departure was detected. </summary>
    public Vec2 Position { get; }
}

/// <summary>
/// Mouse cursor shape to display (Flutter<c>SystemMouseCursors</c>equivalent minimum set).
/// <see cref="HamonRoot.CurrentCursor"/>is on top while hovering<see cref="MouseRegion"/>Since it reflects the value of
/// The backend (MonoGame, etc.) maps it to the actual OS cursor (as with physical input, the mapping is on the user side).
/// </summary>
public enum MouseCursor : byte
{
    /// <summary>Default (arrow).</summary>
    Basic,

    /// <summary>Clickable (hand shape).</summary>
    Click,

    /// <summary>Text selection (I-beam).</summary>
    Text,

    /// <summary>Cannot be operated.</summary>
    Forbidden,

    /// <summary>I can grab it.</summary>
    Grab,

    /// <summary>I'm grabbing it.</summary>
    Grabbing,

    /// <summary>Left and right resize.</summary>
    ResizeLeftRight,

    /// <summary>Resize up and down.</summary>
    ResizeUpDown,

    /// <summary>Cursor hidden.</summary>
    None,
}

/// <summary>
/// Area behavior during hit testing (Flutter<c>HitTestBehavior</c>equivalent).
/// Determines hover occlusion (does it pass through to the area behind it) and whether or not it is possible to hit in an empty area.
/// </summary>
public enum HitTestBehavior : byte
{
    /// <summary>Only when the child is hit, will it also be hit (empty areas with no children are transparent).</summary>
    DeferToChild,

    /// <summary>Entire area hit = occluding behind (default opaque).</summary>
    Opaque,

    /// <summary>It also hits you and passes behind you (semi-transparent).</summary>
    Translucent,
}

/// <summary>
/// Detect mouse hover (Flutter<c>MouseRegion</c>).
/// <see cref="OnEnter"/>/<see cref="OnHover"/>/<see cref="OnExit"/>ignite,<see cref="Cursor"/>present.
/// <para>
/// <b>touch/hover separation</b>:hover is<see cref="HamonRoot.DispatchHover"/>Drives just by (mouse movement),
/// of touch<see cref="HamonRoot.DispatchPointer"/>(Press/Drag) does not fire.
/// Hover does not occur at all, so do not set up operations assuming hover (press-type is<see cref="GestureDetector"/>/<see cref="Button"/>).
/// </para>
/// </summary>
public sealed class MouseRegion : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    /// <summary>When the pointer enters the area (hover starts).</summary>
    public Action<PointerEnterEvent>? OnEnter { get; init; }

    /// <summary>When the pointer moves within the area (while hovering or not pressed).</summary>
    public Action<PointerHoverEvent>? OnHover { get; init; }

    /// <summary>When the pointer leaves the area (hover ends).</summary>
    public Action<PointerExitEvent>? OnExit { get; init; }

    /// <summary>Cursor shape to present while hovering.</summary>
    public MouseCursor Cursor { get; init; } = MouseCursor.Basic;

    /// <summary>
    /// true (default) to block hovering to the area behind (opaque). <see cref="MouseRegion"/>Also passes hover.
    /// Flutter <c>MouseRegion.opaque</c>Quite a bit.<see cref="HitTestBehavior"/>Opaque/Translucent switching.
    /// </summary>
    public bool Opaque { get; init; } = true;

    // レイアウトは透過 Box（子をそのまま測る）。背景は描かない＝hover 検出専用。
    Style IRenderConfig.Style => new() { Kind = LayoutKind.Box };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new MouseRegionElement(this);
}

/// <summary>
/// A holding entity that can accept hover (<see cref="MouseRegion"/>or<see cref="FocusableActionDetector"/>entity).
/// <see cref="HamonRoot"/>collects the objects directly under the pointer front-to-back and delivers enter/hover/exit.
/// </summary>
internal interface IHoverTarget
{
    /// <summary>Does it block hovering behind you (opaque)?</summary>
    bool HoverOpaque { get; }

    /// <summary>Cursor to present while hovering.</summary>
    MouseCursor HoverCursor { get; }

    void HoverEnter(Vec2 position);

    void HoverMove(Vec2 position);

    void HoverExit(Vec2 position);
}

/// <summary>
/// <see cref="MouseRegion"/>holding entity. <see cref="HamonRoot"/>but<see cref="HamonRoot.DispatchHover"/>Do it with
/// Call enter/hover/exit on this entity.
/// </summary>
internal sealed class MouseRegionElement : RenderElement, IHoverTarget
{
    public MouseRegionElement(MouseRegion widget)
        : base(widget)
    {
    }

    private MouseRegion W => (MouseRegion)Widget;

    bool IHoverTarget.HoverOpaque => W.Opaque;

    MouseCursor IHoverTarget.HoverCursor => W.Cursor;

    void IHoverTarget.HoverEnter(Vec2 position) => W.OnEnter?.Invoke(new PointerEnterEvent(position));

    void IHoverTarget.HoverMove(Vec2 position) => W.OnHover?.Invoke(new PointerHoverEvent(position));

    void IHoverTarget.HoverExit(Vec2 position) => W.OnExit?.Invoke(new PointerExitEvent(position));

    public override void Unmount()
    {
        // hover 中に消える（パネル閉じ等）なら exit を保証してから外す。
        (Context.Owner as HamonRoot)?.NotifyHoverTargetUnmounted(this);
        base.Unmount();
    }
}
