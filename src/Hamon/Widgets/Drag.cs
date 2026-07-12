using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Drag and drop progress (active<see cref="Draggable{T}"/>data/location and registered<see cref="DragTarget{T}"/>）。
/// The data is<see cref="object"/>(The public generic is closed with box/cast at the boundary).
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

    /// <summary>Appearance to follow (<see cref="DragLayer"/>).</summary>
    public Widget? Feedback { get; private set; }

    /// <summary>The receivable target currently under the pointer (for highlighting).</summary>
    public DragTargetElement? Over { get; private set; }

    /// <summary><see cref="DragLayer"/>Reconstruction trigger at registration (local reconstruction of only that layer when drag starts/ends = avoids rebuilding the entire tree).</summary>
    internal Action? FeedbackChanged { get; set; }

    /// <summary><see cref="DragLayer"/>is in the tree (if so, the feedback is drawn by the layer = avoiding a full rebuild of the overlay).</summary>
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

    /// <summary>Drop confirmed: If the target directly below receives it.<c>OnAccept</c>call. </summary>
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

/// <summary>public generic<see cref="Draggable{T}"/>An internal view for handling type erasure (<see cref="DraggableElement"/>read).</summary>
internal interface IDraggableConfig
{
    /// <summary>Transportation data (<see cref="Draggable{T}.Data"/>box).</summary>
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
/// Draggable elements (Flutter<c>Draggable&lt;T&gt;</c>equivalent).<see cref="Child"/>looks normal, follows the pointer while dragging
/// <see cref="Feedback"/>(Unspecified<see cref="Child"/>) to the front, and then move the<see cref="DragTarget{T}"/>fart
/// <see cref="Data"/>pass. <see cref="ChildWhenDragging"/>(unspecified)<see cref="Child"/>).
/// If you let go less than slop<see cref="OnTap"/>。<b>Child is non-interactive</b>(self takes the pointer).
/// </summary>
/// <typeparam name="T">Type of data to be carried.<see cref="DragTarget{T}"/>are the same<typeparamref name="T"/>It can only be received when</typeparam>
public sealed class Draggable<T> : Widget, IRenderConfig, IDraggableConfig
{
    public Widget? Child { get; init; }

    public required T Data { get; init; }

    /// <summary>Appearance that follows the pointer while dragging (unspecified)<see cref="Child"/>）。</summary>
    public Widget? Feedback { get; init; }

    /// <summary>The appearance of returning to the original position while dragging (if not specified)<see cref="Child"/>As it is. </summary>
    public Widget? ChildWhenDragging { get; init; }

    /// <summary>Released (= tap) less than slop.</summary>
    public Action? OnTap { get; init; }

    /// <summary>At the start of the drag (beyond slop). </summary>
    public Action? OnDragStarted { get; init; }

    /// <summary>When the drag ends. </summary>
    public Action<bool>? OnDragEnd { get; init; }

    /// <summary>Amount of movement (px, default<see cref="DraggableElement.DefaultSlop"/>). </summary>
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

/// <summary>public generic<see cref="DragTarget{T}"/>An internal view for handling type erasure (<see cref="DragTargetElement"/>read).</summary>
internal interface IDragTargetConfig
{
    /// <summary>Can I receive this data? (Type mismatch is always false =<see cref="DragTarget{T}"/>teeth<typeparamref name="T"/>(Ignore everything else).</summary>
    bool CanAcceptData(object data);

    void AcceptData(object data);

    void NotifyLeaveData(object data);

    Color? HighlightColor { get; }

    /// <summary><see cref="DragTarget{T}.Builder"/>Is it specified (if so, the appearance is determined by the user = no automatic highlighting)?</summary>
    bool HasBuilder { get; }

    /// <summary><see cref="DragTarget{T}.Builder"/>Build the appearance of the current hover state with (<paramref name="candidate"/>is transportation data when active).</summary>
    Widget BuildContent(bool active, object? candidate);
}

/// <summary>
/// <see cref="DragTarget{T}.Builder"/>The hover state to pass to.
/// </summary>
/// <typeparam name="T">Type of data to receive.</typeparam>
public readonly struct DragTargetState<T>
{
    internal DragTargetState(bool isActive, T? candidate)
    {
        IsActive = isActive;
        Candidate = candidate;
    }

    /// <summary>Is there a drag currently available for pickup (drop acceptance expected)?</summary>
    public bool IsActive { get; }

    /// <summary>The candidate data (<see cref="IsActive"/>Valid only when . <c>default</c>）。</summary>
    public T? Candidate { get; }
}

/// <summary>
/// Drop destination (Flutter<c>DragTarget&lt;T&gt;</c>equivalent).<typeparamref name="T"/>When the drag carrying the item is on top and can be received, the frame will be
/// Highlight and drop<see cref="OnAccept"/>Pass the data to.<see cref="CanAccept"/>to determine whether or not it can be received (always accepted if unspecified).
/// <b>Transportation data<typeparamref name="T"/>The framework automatically ignores drags other than</b>(No manual type guard required).
/// When you need a fancy hover expression<see cref="Builder"/>Assemble the entire appearance with (escape hatch) (in that case<see cref="Child"/>/Do not use automatic highlighting).
/// </summary>
/// <typeparam name="T">Type of data to receive.</typeparam>
public sealed class DragTarget<T> : Widget, IRenderConfig, IDragTargetConfig
{
    public Widget? Child { get; init; }

    /// <summary>
    /// hover state (<see cref="DragTargetState{T}"/>) The escape hatch (Flutter<c>DragTarget.builder</c>equivalent).
    /// If specified<see cref="Child"/>and automatic highlighting (<see cref="HighlightColor"/>) are disabled, and the appearance is completely under the user's control.
    /// This element whenever the state changes<b>only</b>(without full tree reconstruction).
    /// </summary>
    public Func<DragTargetState<T>, Widget>? Builder { get; init; }

    public Action<T>? OnAccept { get; init; }

    public Func<T, bool>? CanAccept { get; init; }

    /// <summary>When the drag that can be received is off the top (leaving without dropping/aborting). </summary>
    public Action<T>? OnLeave { get; init; }

    /// <summary>The highlight frame color when the drag that can be received is on top (the theme is Primary if not specified).<see cref="Builder"/>(invalid when specified).</summary>
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

    /// <summary>This target's hover state has changed (controller calls enter/leave/drop). </summary>
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
/// Feedback while dragging (<see cref="Draggable{T}.Feedback"/>) is drawn on top by following the pointer.
/// （<c>FxLayer</c>). <b>only</b>To locally reconstruct<see cref="Draggable{T}"/>at the start
/// You can avoid rebuilding the entire tree (stutter). <b>D&D works even when unplaced</b>
/// (In that case, draw the feedback as an overlay = full rebuild at the start).
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
