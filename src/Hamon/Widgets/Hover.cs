using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// The mouse pointer entered the area (equivalent to Flutter's <c>PointerEnterEvent</c>). Delivered to <see cref="MouseRegion.OnEnter"/>.
/// </summary>
public readonly struct PointerEnterEvent
{
    public PointerEnterEvent(Vec2 position) => Position = position;

    /// <summary>UI coordinates (px).</summary>
    public Vec2 Position { get; }
}

/// <summary>
/// The mouse pointer moved within the area (equivalent to Flutter's <c>PointerHoverEvent</c>). Delivered to <see cref="MouseRegion.OnHover"/>.
/// Movement without a button pressed counts as hover; this does not occur with touch input (mouse only).
/// </summary>
public readonly struct PointerHoverEvent
{
    public PointerHoverEvent(Vec2 position) => Position = position;

    /// <summary>UI coordinates (px).</summary>
    public Vec2 Position { get; }
}

/// <summary>
/// The mouse pointer left the area (equivalent to Flutter's <c>PointerExitEvent</c>). Delivered to <see cref="MouseRegion.OnExit"/>.
/// </summary>
public readonly struct PointerExitEvent
{
    public PointerExitEvent(Vec2 position) => Position = position;

    /// <summary>UI coordinates (px) at the time the exit was detected.</summary>
    public Vec2 Position { get; }
}

/// <summary>
/// The mouse cursor shape to display (a minimal equivalent of Flutter's <c>SystemMouseCursors</c> set).
/// <see cref="HamonRoot.CurrentCursor"/> reflects the value from the topmost <see cref="MouseRegion"/> currently
/// being hovered. The backend (e.g. MonoGame) maps it to the actual OS cursor; as with other physical input, the
/// mapping is the user's responsibility.
/// </summary>
public enum MouseCursor : byte
{
    /// <summary>Default (arrow).</summary>
    Basic,

    /// <summary>Clickable (hand shape).</summary>
    Click,

    /// <summary>Text selection (I-beam).</summary>
    Text,

    /// <summary>Not operable.</summary>
    Forbidden,

    /// <summary>Can be grabbed (open hand).</summary>
    Grab,

    /// <summary>Being grabbed (closed hand).</summary>
    Grabbing,

    /// <summary>Left and right resize.</summary>
    ResizeLeftRight,

    /// <summary>Resize up and down.</summary>
    ResizeUpDown,

    /// <summary>Cursor hidden.</summary>
    None,
}

/// <summary>
/// Area behavior during hit testing (equivalent to Flutter's <c>HitTestBehavior</c>).
/// Determines whether hover is occluded (blocked from passing through to the area behind it) and whether an empty
/// area can be hit at all.
/// </summary>
public enum HitTestBehavior : byte
{
    /// <summary>Only hit when a child is hit (empty areas with no children are transparent).</summary>
    DeferToChild,

    /// <summary>The entire area is hit and occludes what is behind it (default, opaque).</summary>
    Opaque,

    /// <summary>The area is hit but also passes through to what is behind it (translucent).</summary>
    Translucent,
}

/// <summary>
/// Detects mouse hover (equivalent to Flutter's <c>MouseRegion</c>).
/// Fires <see cref="OnEnter"/>/<see cref="OnHover"/>/<see cref="OnExit"/>, and presents <see cref="Cursor"/>.
/// <para>
/// <b>Touch/hover separation</b>: hover is driven only by <see cref="HamonRoot.DispatchHover"/> (mouse movement); it
/// does not fire from touch <see cref="HamonRoot.DispatchPointer"/> (press/drag). Hover never occurs on touch input
/// at all, so do not build interactions that assume hover will fire (use press-based widgets such as
/// <see cref="GestureDetector"/>/<see cref="Button"/> instead).
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
    /// True (default) blocks hover from reaching the area behind this one (opaque); false lets hover pass through to
    /// the <see cref="MouseRegion"/> behind it as well. Equivalent to Flutter's <c>MouseRegion.opaque</c>; switches
    /// between <see cref="HitTestBehavior"/> Opaque and Translucent.
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
/// A holding entity that can accept hover (the entity for <see cref="MouseRegion"/> or <see cref="FocusableActionDetector"/>).
/// <see cref="HamonRoot"/> collects the objects directly under the pointer, front-to-back, and delivers enter/hover/exit.
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
/// The holding entity for <see cref="MouseRegion"/>. <see cref="HamonRoot"/> calls enter/hover/exit on this entity
/// as part of <see cref="HamonRoot.DispatchHover"/>.
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
