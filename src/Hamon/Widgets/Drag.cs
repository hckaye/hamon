using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Tracks drag-and-drop progress: the active <see cref="Draggable{T}"/>'s data and position, and the
/// registered <see cref="DragTarget{T}"/> instances. Data is held as <see cref="object"/> (the public
/// generic type is erased via boxing/casting at the boundary).
/// </summary>
internal sealed class DragController
{
    private readonly List<DragTargetElement> _targets = new();

    /// <summary>Data being transported (null = no drag).</summary>
    public object? Data { get; private set; }

    /// <summary>Current pointer position (used for tracking feedback).</summary>
    public Vec2 Pos { get; private set; }

    /// <summary>The grabbed position (offset from the top left of the drag source).</summary>
    public Vec2 Grab { get; private set; }

    public bool Active => Data is not null;

    /// <summary>Appearance that follows the pointer (drawn by <see cref="DragLayer"/>).</summary>
    public Widget? Feedback { get; private set; }

    /// <summary>The receivable target currently under the pointer (for highlighting).</summary>
    public DragTargetElement? Over { get; private set; }

    /// <summary>Rebuild trigger registered by <see cref="DragLayer"/> (rebuilds only that layer when a drag starts/ends, avoiding a rebuild of the entire tree).</summary>
    internal Action? FeedbackChanged { get; set; }

    /// <summary>Whether a <see cref="DragLayer"/> is present in the tree (if so, the feedback is drawn by that layer, avoiding a full rebuild of the overlay).</summary>
    public bool HasLayer => FeedbackChanged is not null;

    public void Register(DragTargetElement target) => _targets.Add(target);

    public void Unregister(DragTargetElement target)
    {
        _targets.Remove(target);
        if (ReferenceEquals(Over, target))
        {
            Over = null; // 退場中の要素へ OnLeave は出さない
        }
    }

    public void Begin(object data, Vec2 pos, Vec2 grab, Widget? feedback)
    {
        Data = data;
        Pos = pos;
        Grab = grab;
        Feedback = feedback;
        UpdateOver();
        FeedbackChanged?.Invoke(); // 層がフィードバックを描くため局所再構築
    }

    public void Move(Vec2 pos)
    {
        if (!Active)
        {
            return;
        }

        Pos = pos;
        UpdateOver();
    }

    /// <summary>Confirms the drop: if the target directly underneath can accept it, calls its <c>OnAccept</c>.</summary>
    public bool Drop()
    {
        bool accepted = false;
        DragTargetElement? was = Over;
        if (Over is DragTargetElement t && Data is object d)
        {
            t.Accept(d);
            accepted = true;
        }

        Over = null; // 受理先には OnLeave を出さない（OnAccept のみ）
        ClearState();
        was?.OnOverChanged(); // Builder を inactive へ戻す
        return accepted;
    }

    /// <summary>Abort without dropping (issue OnLeave for current target).</summary>
    public void Cancel()
    {
        DragTargetElement? was = Over;
        if (Over is DragTargetElement t && Data is object d)
        {
            t.NotifyLeave(d);
        }

        Over = null;
        ClearState();
        was?.OnOverChanged(); // Builder を inactive へ戻す
    }

    private void ClearState()
    {
        Data = null;
        Feedback = null;
        FeedbackChanged?.Invoke(); // 層を空へ局所再構築
    }

    private void UpdateOver()
    {
        DragTargetElement? next = null;
        if (Data is object d)
        {
            for (int i = _targets.Count - 1; i >= 0; i--) // 後勝ち（最前面）
            {
                DragTargetElement t = _targets[i];
                if (t.IsMounted && t.Bounds.Contains(Pos.X, Pos.Y) && t.CanAccept(d))
                {
                    next = t;
                    break;
                }
            }
        }

        if (!ReferenceEquals(Over, next))
        {
            DragTargetElement? prev = Over;
            if (prev is DragTargetElement left && Data is object ld)
            {
                left.NotifyLeave(ld); // ターゲットから外れた＝OnLeave
            }

            Over = next;
            prev?.OnOverChanged(); // Builder を旧ターゲット=inactive へ
            next?.OnOverChanged(); // 新ターゲット=active へ
        }
    }
}

/// <summary>An internal, type-erased view of the public generic <see cref="Draggable{T}"/>, read by <see cref="DraggableElement"/>.</summary>
internal interface IDraggableConfig
{
    /// <summary>The transported data (boxed from <see cref="Draggable{T}.Data"/>).</summary>
    object Data { get; }

    Widget? Child { get; }

    Widget? Feedback { get; }

    Widget? ChildWhenDragging { get; }

    Action? OnTap { get; }

    Action? OnDragStarted { get; }

    Action<bool>? OnDragEnd { get; }

    float Slop { get; }
}

/// <summary>
/// A draggable element (equivalent to Flutter's <c>Draggable&lt;T&gt;</c>). <see cref="Child"/> is displayed
/// normally; while dragging, <see cref="Feedback"/> (falls back to <see cref="Child"/> if unspecified) follows
/// the pointer on top, and dropping onto a <see cref="DragTarget{T}"/> passes it <see cref="Data"/>.
/// <see cref="ChildWhenDragging"/> (falls back to <see cref="Child"/> if unspecified) is shown in the original
/// position while dragging. If released before moving past <c>Slop</c>, <see cref="OnTap"/> fires instead.
/// <b>The child is non-interactive</b> — the draggable itself captures the pointer.
/// </summary>
/// <typeparam name="T">Type of data being carried. Can only be received by a <see cref="DragTarget{T}"/> with the same <typeparamref name="T"/>.</typeparam>
public sealed class Draggable<T> : Widget, IRenderConfig, IDraggableConfig
{
    public Widget? Child { get; init; }

    public required T Data { get; init; }

    /// <summary>Appearance that follows the pointer while dragging (falls back to <see cref="Child"/> if unspecified).</summary>
    public Widget? Feedback { get; init; }

    /// <summary>Appearance shown in the original position while dragging (falls back to <see cref="Child"/> as-is if unspecified).</summary>
    public Widget? ChildWhenDragging { get; init; }

    /// <summary>Invoked when released before moving past the slop threshold (i.e. a tap).</summary>
    public Action? OnTap { get; init; }

    /// <summary>Invoked when dragging starts (movement exceeds the slop threshold).</summary>
    public Action? OnDragStarted { get; init; }

    /// <summary>Invoked when the drag ends, with a flag indicating whether the drop was accepted.</summary>
    public Action<bool>? OnDragEnd { get; init; }

    /// <summary>Movement threshold in pixels before a drag starts (defaults to <see cref="DraggableElement.DefaultSlop"/>).</summary>
    public float Slop { get; init; } = DraggableElement.DefaultSlop;

    object IDraggableConfig.Data => Data!; // T→object（参照型は box なし／値型は従来同様ここで1回 box）

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };

    Color? IRenderConfig.Background => null;

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new DraggableElement(this);
}

internal sealed class DraggableElement : RenderElement
{
    /// <summary>Default amount of movement (px) to determine when dragging starts.</summary>
    public const float DefaultSlop = 4f;

    private bool _dragging;
    private Vec2 _down;
    private OverlayEntry? _feedback;

    public DraggableElement(Widget widget)
        : base(widget)
    {
    }

    private IDraggableConfig W => (IDraggableConfig)Widget;

    public override bool WantsPointer => true;

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget); // 通常の Child を適用
        if (_dragging && W.ChildWhenDragging is not null)
        {
            RebuildInPlace(); // ドラッグ中に親が reconcile しても ChildWhenDragging を保つ
        }
    }

    public override void HandlePointer(in PointerEvent pointer)
    {
        var host = Context.Owner as HamonRoot;
        switch (pointer.Phase)
        {
            case PointerPhase.Down:
                _down = pointer.Position;
                break;

            case PointerPhase.Move:
                if (!_dragging && host is not null && SqDist(pointer.Position, _down) > W.Slop * W.Slop)
                {
                    _dragging = true;
                    Rect b = LayoutNode.Bounds;
                    host.Drag.Begin(W.Data, pointer.Position, new Vec2(_down.X - b.X, _down.Y - b.Y), W.Feedback ?? W.Child);

                    // DragLayer があればフィードバックはそこが描く（その層だけ局所再構築＝開始時の全ツリー再構築/カクつきを回避）。
                    // 無ければ従来どおりオーバーレイで描く（互換）。
                    if (!host.Drag.HasLayer)
                    {
                        _feedback = host.PushOverlay(() => FeedbackOverlay(host));
                    }

                    SwapChildForDrag(host); // ChildWhenDragging 指定時のみ元位置を差し替え
                    W.OnDragStarted?.Invoke();
                }

                if (_dragging)
                {
                    host?.Drag.Move(pointer.Position);
                }

                break;

            case PointerPhase.Up:
                if (_dragging)
                {
                    bool accepted = host?.Drag.Drop() ?? false;
                    CloseFeedback(host);
                    _dragging = false;
                    SwapChildForDrag(host);
                    W.OnDragEnd?.Invoke(accepted);
                }
                else if (LayoutNode.Bounds.Contains(pointer.Position.X, pointer.Position.Y))
                {
                    W.OnTap?.Invoke();
                }

                break;

            case PointerPhase.Cancel:
                if (_dragging)
                {
                    host?.Drag.Cancel();
                    CloseFeedback(host);
                    _dragging = false;
                    SwapChildForDrag(host);
                    W.OnDragEnd?.Invoke(false);
                }

                break;
        }
    }

    // ChildWhenDragging があるときだけ、元位置の子を局所再構築で差し替える（次フレームに RebuildInPlace＋部分再レイアウト）。
    private void SwapChildForDrag(HamonRoot? host)
    {
        if (W.ChildWhenDragging is not null)
        {
            host?.MarkElementDirty(this);
        }
    }

    internal override void RebuildInPlace()
    {
        Widget shown = _dragging && W.ChildWhenDragging is Widget dragging ? dragging : W.Child ?? new SizedBox();
        UpdateChildren(new[] { shown });
    }

    private Widget FeedbackOverlay(HamonRoot host)
    {
        Widget fb = W.Feedback ?? W.Child ?? new SizedBox();
        return new Transform
        {
            Origin = Alignment.TopLeft,
            TranslateXGetter = () => host.Drag.Pos.X - host.Drag.Grab.X,
            TranslateYGetter = () => host.Drag.Pos.Y - host.Drag.Grab.Y,
            // Align(TopLeft) で loose 制約にして自然サイズにする（オーバーレイも tight 全画面に伸ばされ得るため）。
            Child = new Align { Alignment = Alignment.TopLeft, Child = new Opacity { Value = 0.85f, Child = fb } },
        };
    }

    private void CloseFeedback(HamonRoot? host)
    {
        if (_feedback is OverlayEntry entry)
        {
            host?.RemoveOverlay(entry);
            _feedback = null;
        }
    }

    public override void Unmount()
    {
        if (_dragging)
        {
            (Context.Owner as HamonRoot)?.Drag.Cancel();
        }

        CloseFeedback(Context.Owner as HamonRoot);
        base.Unmount();
    }

    private static float SqDist(Vec2 a, Vec2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
}

/// <summary>An internal, type-erased view of the public generic <see cref="DragTarget{T}"/>, read by <see cref="DragTargetElement"/>.</summary>
internal interface IDragTargetConfig
{
    /// <summary>Whether this data can be received. Always false on a type mismatch, since a <see cref="DragTarget{T}"/> only accepts its own <c>T</c> and ignores anything else.</summary>
    bool CanAcceptData(object data);

    void AcceptData(object data);

    void NotifyLeaveData(object data);

    Color? HighlightColor { get; }

    /// <summary>Whether <see cref="DragTarget{T}.Builder"/> is specified (if so, the appearance is fully controlled by the user and automatic highlighting is disabled).</summary>
    bool HasBuilder { get; }

    /// <summary>Builds the appearance for the current hover state via <see cref="DragTarget{T}.Builder"/> (<paramref name="candidate"/> is the transported data when active).</summary>
    Widget BuildContent(bool active, object? candidate);
}

/// <summary>The hover state passed to <see cref="DragTarget{T}.Builder"/>.</summary>
/// <typeparam name="T">Type of data to receive.</typeparam>
public readonly struct DragTargetState<T>
{
    internal DragTargetState(bool isActive, T? candidate)
    {
        IsActive = isActive;
        Candidate = candidate;
    }

    /// <summary>Whether a drag currently over this target is acceptable (i.e. a drop is expected to be accepted).</summary>
    public bool IsActive { get; }

    /// <summary>The candidate data (valid only when <see cref="IsActive"/> is true; otherwise <c>default</c>).</summary>
    public T? Candidate { get; }
}

/// <summary>
/// A drop destination (equivalent to Flutter's <c>DragTarget&lt;T&gt;</c>). While a drag carrying data of type
/// <typeparamref name="T"/> is over it and acceptable, the frame is highlighted, and on drop
/// <see cref="OnAccept"/> receives the data. <see cref="CanAccept"/> determines whether the data can be
/// received (always accepted if unspecified).
/// <b>The framework automatically ignores drags whose data isn't <typeparamref name="T"/></b> (no manual type
/// guard is needed). For a custom hover appearance, use <see cref="Builder"/> to assemble the entire look
/// (an escape hatch); in that case <see cref="Child"/> and automatic highlighting are not used.
/// </summary>
/// <typeparam name="T">Type of data to receive.</typeparam>
public sealed class DragTarget<T> : Widget, IRenderConfig, IDragTargetConfig
{
    public Widget? Child { get; init; }

    /// <summary>
    /// An escape hatch that builds the appearance from the hover state (<see cref="DragTargetState{T}"/>),
    /// equivalent to Flutter's <c>DragTarget.builder</c>. When specified, <see cref="Child"/> and automatic
    /// highlighting (<see cref="HighlightColor"/>) are disabled, and the appearance is entirely under the user's
    /// control. <b>Only</b> this element is rebuilt whenever the state changes (without rebuilding the full tree).
    /// </summary>
    public Func<DragTargetState<T>, Widget>? Builder { get; init; }

    public Action<T>? OnAccept { get; init; }

    public Func<T, bool>? CanAccept { get; init; }

    /// <summary>Invoked when an acceptable drag leaves this target without dropping (or is aborted).</summary>
    public Action<T>? OnLeave { get; init; }

    /// <summary>The highlight border color shown when an acceptable drag is over this target (defaults to the theme's primary color if unspecified). Ignored when <see cref="Builder"/> is specified.</summary>
    public Color? HighlightColor { get; init; }

    bool IDragTargetConfig.CanAcceptData(object data) => data is T value && (CanAccept?.Invoke(value) ?? true);

    void IDragTargetConfig.AcceptData(object data)
    {
        if (data is T value)
        {
            OnAccept?.Invoke(value);
        }
    }

    void IDragTargetConfig.NotifyLeaveData(object data)
    {
        if (data is T value)
        {
            OnLeave?.Invoke(value);
        }
    }

    Color? IDragTargetConfig.HighlightColor => HighlightColor;

    bool IDragTargetConfig.HasBuilder => Builder is not null;

    Widget IDragTargetConfig.BuildContent(bool active, object? candidate) =>
        Builder!(new DragTargetState<T>(active, active && candidate is T value ? value : default));

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };

    IReadOnlyList<Widget>? IRenderConfig.Children =>
        Builder is not null ? new[] { Builder(default) } : Child is null ? null : new[] { Child };

    Color? IRenderConfig.Background => null;

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new DragTargetElement(this);
}

internal sealed class DragTargetElement : RenderElement
{
    public DragTargetElement(Widget widget)
        : base(widget)
    {
    }

    private IDragTargetConfig W => (IDragTargetConfig)Widget;

    public Rect Bounds => LayoutNode.Bounds;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        (Context.Owner as HamonRoot)?.Drag.Register(this);
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget); // Builder 指定時は inactive 状態を適用
        if (W.HasBuilder && IsActiveNow())
        {
            RebuildInPlace(); // 親 reconcile 中でも現在の hover 状態を保つ
        }
    }

    public override void Unmount()
    {
        (Context.Owner as HamonRoot)?.Drag.Unregister(this);
        base.Unmount();
    }

    public bool CanAccept(object data) => W.CanAcceptData(data);

    public void Accept(object data) => W.AcceptData(data);

    public void NotifyLeave(object data) => W.NotifyLeaveData(data);

    /// <summary>Called when this target's hover state changes (invoked by the controller on enter/leave/drop).</summary>
    internal void OnOverChanged()
    {
        if (W.HasBuilder)
        {
            (Context.Owner as HamonRoot)?.MarkElementDirty(this);
        }
    }

    internal override void RebuildInPlace()
    {
        if (!W.HasBuilder)
        {
            return;
        }

        bool active = IsActiveNow();
        object? candidate = active && Context.Owner is HamonRoot host ? host.Drag.Data : null;
        UpdateChildren(new[] { W.BuildContent(active, candidate) });
    }

    private bool IsActiveNow() => Context.Owner is HamonRoot host && ReferenceEquals(host.Drag.Over, this);

    public override void Paint(in PaintContext context)
    {
        base.Paint(context); // 子（Builder 指定時はそれが状態別の見た目）
        // 自動ハイライトは Child パスのみ（Builder 指定時は見た目を利用側が完全制御）。
        if (!W.HasBuilder && Context.Owner is HamonRoot host && ReferenceEquals(host.Drag.Over, this))
        {
            context.DrawOutline(LayoutNode.Bounds, W.HighlightColor ?? Context.Theme.Primary, 2f);
        }
    }
}

/// <summary>
/// Draws the drag feedback (<see cref="Draggable{T}.Feedback"/>) on top, following the pointer (similar to an
/// <c>FxLayer</c>). Placing this near the root means <b>only</b> this layer is locally rebuilt when a
/// <see cref="Draggable{T}"/> starts dragging, avoiding a rebuild of the entire tree (which would stutter).
/// <b>Drag-and-drop still works even if this isn't placed anywhere</b> — in that case, the feedback is drawn
/// as an overlay instead, causing a full rebuild at the start.
/// </summary>
public sealed class DragLayer : HookWidget
{
    public override Widget Build(BuildContext context, Hooks hooks)
    {
        var host = context.Owner as HamonRoot;
        HookState<int> version = hooks.UseState(0);

        // 自分をドラッグ状態変化の再構築先として登録（開始/終了でこの層だけ作り直す）。
        hooks.UseEffect(() =>
        {
            if (host is not null)
            {
                host.Drag.FeedbackChanged = () => version.Value++;
            }

            return () =>
            {
                if (host is not null)
                {
                    host.Drag.FeedbackChanged = null;
                }
            };
        });

        if (host is null || !host.Drag.Active || host.Drag.Feedback is not Widget feedback)
        {
            return new SizedBox();
        }

        return new Transform
        {
            Origin = Alignment.TopLeft,
            TranslateXGetter = () => host.Drag.Pos.X - host.Drag.Grab.X,
            TranslateYGetter = () => host.Drag.Pos.Y - host.Drag.Grab.Y,
            // Align(TopLeft) で loose 制約にして feedback を自然サイズにする（層は StackFit.Expand で tight 全画面に
            // 引き伸ばされるため、包まないと feedback が画面いっぱいに広がる）。
            Child = new Align { Alignment = Alignment.TopLeft, Child = new Opacity { Value = 0.85f, Child = feedback } },
        };
    }
}
