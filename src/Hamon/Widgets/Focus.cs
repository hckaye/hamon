using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Direction of focus movement (D-pad/stick).</summary>
public enum FocusDirection : byte
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// Physical gamepad buttons (faithful to the actual layout).
/// Left/right distinction (see <see cref="GamepadSide"/>) is handled separately. The user is expected to map
/// this enum from their platform's input state (e.g. <c>GamePadState</c>).
/// </summary>
public enum GamepadButton : byte
{
    A,
    B,
    X,
    Y,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
    LeftShoulder,
    RightShoulder,
    LeftStick,   // スティック押し込み
    RightStick,
    Start,
    Back,
    Guide,
}

/// <summary>Left/right distinction (for trigger/stick analog input).</summary>
public enum GamepadSide : byte
{
    Left,
    Right,
}

/// <summary>
/// Scope of focus (equivalent to Flutter's <c>FocusScopeNode</c>). When <see cref="Trap"/> is true, this acts
/// as a modal, containing directional movement and initial focus within itself. Assigned to a
/// <see cref="FocusScope"/>.
/// </summary>
public sealed class FocusScopeNode
{
    /// <summary>true to keep focus within this scope (modal/dialog).</summary>
    public bool Trap { get; set; } = true;
}

/// <summary>
/// Focusable target (equivalent to Flutter's <c>FocusNode</c>). Assigned to a <see cref="Focus"/> widget.
/// OK/Cancel and gamepad/keyboard input are delivered through the <see cref="FocusManager"/>.
/// </summary>
public sealed class FocusNode
{
    public bool HasFocus { get; internal set; }

    public bool CanRequestFocus { get; set; } = true;

    /// <summary>
    /// Accessibility label (a description readable by screen readers, etc.). Set the widget's
    /// <c>SemanticLabel</c> here; it is read from <see cref="FocusManager.Focused"/>.
    /// </summary>
    public string? SemanticLabel { get; set; }

    /// <summary>Identifier for explicit link resolution (0 = none). Assign one by passing an <c>(int)</c> value.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Order for linear traversal (Tab/Shift+Tab = <see cref="FocusManager.MoveNext"/>/<see cref="FocusManager.MovePrevious"/>).
    /// Lower values come first (equivalent to <c>FocusTraversalOrder</c>).
    /// </summary>
    public int Order { get; set; }

    /// <summary>Explicit upward movement destination Id (0 = automatic neighbor resolution).</summary>
    public int NavUp { get; set; }

    public int NavDown { get; set; }

    public int NavLeft { get; set; }

    public int NavRight { get; set; }

    internal int LinkFor(FocusDirection direction) => direction switch
    {
        FocusDirection.Up => NavUp,
        FocusDirection.Down => NavDown,
        FocusDirection.Left => NavLeft,
        FocusDirection.Right => NavRight,
        _ => 0,
    };

    /// <summary>Press any physical button (all buttons).</summary>
    public Action<GamepadButton>? OnButtonDown { get; set; }

    /// <summary>Release any physical button.</summary>
    public Action<GamepadButton>? OnButtonUp { get; set; }

    /// <summary>
    /// Automatic repeat while held down (equivalent to key repeat), delivered by <see cref="HamonRoot"/>
    /// according to <see cref="GamepadHoldSettings"/>. Since directional buttons move focus by default, only
    /// non-directional button repeats arrive here.
    /// </summary>
    public Action<GamepadButton>? OnButtonRepeat { get; set; }

    /// <summary>Long press, held for <see cref="GamepadHoldSettings.LongPressDuration"/> seconds; fires only once.</summary>
    public Action<GamepadButton>? OnButtonLongPress { get; set; }

    /// <summary>Analog value of the trigger (0..1).</summary>
    public Action<GamepadSide, float>? OnTrigger { get; set; }

    /// <summary>Analog value of the stick (-1..1 for each axis).</summary>
    public Action<GamepadSide, Vec2>? OnStick { get; set; }

    /// <summary>Convenient shortcut when pressing OK (default binding Activate button).</summary>
    public Action? OnActivate { get; set; }

    /// <summary>Convenient shortcut when pressing Cancel (default binding Dismiss button).</summary>
    public Action? OnDismiss { get; set; }

    public Action<bool>? OnFocusChange { get; set; }

    /// <summary>Confirmed character input (e.g. <c>TextField</c>).</summary>
    public Action<char>? OnTextInput { get; set; }

    /// <summary>Edit keys (Backspace/Delete/cursor movement/Enter, etc.).</summary>
    public Action<TextEditKey>? OnEditKey { get; set; }

    /// <summary>
    /// IME converting text (preedit/composition).
    /// An empty string indicates that the composition has disappeared due to conversion cancellation/confirmation.
    /// </summary>
    public Action<string, int>? OnComposition { get; set; }
}

/// <summary>Control keys for text editing (non-character operations).</summary>
public enum TextEditKey : byte
{
    Backspace,
    Delete,
    Left,
    Right,
    Up,
    Down,
    Home,
    End,
    Enter,

    /// <summary>Move left while selecting (Shift+Left).</summary>
    SelectLeft,

    /// <summary>Select and move to the right (Shift+Right).</summary>
    SelectRight,

    /// <summary>Move to the beginning of the line while selecting (Shift+Home).</summary>
    SelectHome,

    /// <summary>Go to the end of the line while selecting (Shift+End).</summary>
    SelectEnd,

    /// <summary>Select all (Ctrl/Cmd+A).</summary>
    SelectAll,

    /// <summary>Copy (Ctrl/Cmd+C).</summary>
    Copy,

    /// <summary>Cut (Ctrl/Cmd+X).</summary>
    Cut,

    /// <summary>Paste (Ctrl/Cmd+V).</summary>
    Paste,

    /// <summary>Insert line break (Shift+Enter etc. for multiple lines).</summary>
    NewLine,
}

/// <summary>
/// Controls focus registration, current location, directional movement, and OK/Cancel delivery (equivalent to
/// Flutter's FocusManager plus directional traversal). The bounding rectangle is obtained on demand from the
/// provider supplied at registration time (post-layout <see cref="LayoutNode.Bounds"/>).
/// </summary>
public sealed class FocusManager
{
    private readonly List<Entry> _entries = new();
    private readonly List<FocusScopeNode> _trapStack = new();

    public FocusNode? Focused { get; private set; }

    /// <summary>
    /// Debug dump of registered focus nodes (focus marker, rectangle, traversability, trap affiliation), one
    /// node per row.
    /// </summary>
    public string DumpFocusTree()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("FocusTree (").Append(_entries.Count).Append(" nodes, ").Append(_trapStack.Count).Append(" traps)\n");
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            Rect b = e.Bounds();
            sb.Append(ReferenceEquals(e.Node, Focused) ? "  * " : "    ")
              .Append("id=").Append(e.Node.Id)
              .Append(" rect=(").Append(Round(b.X)).Append(',').Append(Round(b.Y)).Append(',').Append(Round(b.Width)).Append(',').Append(Round(b.Height)).Append(')')
              .Append(e.Node.CanRequestFocus ? string.Empty : " [disabled]")
              .Append(e.Traversable ? string.Empty : " [no-traverse]")
              .Append(e.Scope is not null ? " [scoped]" : string.Empty)
              .Append('\n');
        }

        return sb.ToString();
    }

    private static int Round(float v) => (int)MathF.Round(v);


    public void RequestFocus(FocusNode node)
    {
        if (Focused == node || !node.CanRequestFocus || !EligibleForFocus(node))
        {
            return;
        }

        if (Focused is not null)
        {
            Focused.HasFocus = false;
            Focused.OnFocusChange?.Invoke(false);
        }

        Focused = node;
        node.HasFocus = true;
        node.OnFocusChange?.Invoke(true);
    }

    /// <summary>Deliver physical button presses to the current focus (raw).</summary>
    public void DispatchButtonDown(GamepadButton button) => Focused?.OnButtonDown?.Invoke(button);

    /// <summary>Deliver physical button release to currently focused (raw).</summary>
    public void DispatchButtonUp(GamepadButton button) => Focused?.OnButtonUp?.Invoke(button);

    /// <summary>Deliver automatic repeat when pressed to the current focus.</summary>
    public void DispatchButtonRepeat(GamepadButton button) => Focused?.OnButtonRepeat?.Invoke(button);

    /// <summary>Deliver a long press to the current focus (once when the retention threshold is exceeded).</summary>
    public void DispatchButtonLongPress(GamepadButton button) => Focused?.OnButtonLongPress?.Invoke(button);

    /// <summary>Deliver the analog value of the trigger to the current focus.</summary>
    public void DispatchTrigger(GamepadSide side, float value) => Focused?.OnTrigger?.Invoke(side, value);

    /// <summary>Deliver the analog value of the stick to the current focus.</summary>
    public void DispatchStick(GamepadSide side, Vec2 value) => Focused?.OnStick?.Invoke(side, value);

    /// <summary>Fires a convenience shortcut of OK (Activate).</summary>
    public void Activate() => Focused?.OnActivate?.Invoke();

    /// <summary>Fires a convenience shortcut for Cancel (Dismiss).</summary>
    public void Dismiss() => Focused?.OnDismiss?.Invoke();

    /// <summary>Move to the nearest focusable element in the given direction.</summary>
    public bool MoveFocus(FocusDirection direction)
    {
        if (_entries.Count == 0)
        {
            return false;
        }

        // テキスト編集中（フォーカスが TextField 等＝OnEditKey を持つ）は、左右はキャレット移動に充て、
        // フォーカスは移さない（Flutter の TextField と同じ）。上下は通常どおりフォーカス移動＝欄から出る。
        if (Focused?.OnEditKey is { } editKey && (direction == FocusDirection.Left || direction == FocusDirection.Right))
        {
            editKey(direction == FocusDirection.Left ? TextEditKey.Left : TextEditKey.Right);
            return true;
        }

        if (Focused is null)
        {
            FocusNode? first = FirstFocusable();
            if (first is null)
            {
                return false;
            }

            RequestFocus(first);
            return true;
        }

        // 明示リンク優先（Id ベースの手動指定）。解決できなければオート近傍へフォールバック。
        int linkId = Focused.LinkFor(direction);
        if (linkId != 0)
        {
            FocusNode? linked = ResolveId(linkId);
            if (linked is not null && linked.CanRequestFocus)
            {
                RequestFocus(linked);
                return true;
            }
        }

        Rect fromRect = BoundsOf(Focused);
        FocusNode? best = null;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            if (e.Node == Focused || !e.Node.CanRequestFocus || !e.Traversable || !IsEligible(e.Scope))
            {
                continue;
            }

            float score = DirectionalScore(fromRect, e.Bounds(), direction);
            if (score >= 0f && score < bestScore)
            {
                bestScore = score;
                best = e.Node;
            }
        }

        if (best is null)
        {
            return false;
        }

        RequestFocus(best);
        return true;
    }

    /// <summary>
    /// Move to the next focus target using linear traversal (Tab), ordered by <see cref="FocusNode.Order"/> and
    /// then registration order. This makes it possible to navigate the entire UI with the keyboard alone, in
    /// environments without direction keys.
    /// </summary>
    public bool MoveNext() => MoveLinear(1);

    /// <summary>Move to the previous focus target using linear traversal (Shift+Tab).</summary>
    public bool MovePrevious() => MoveLinear(-1);

    private bool MoveLinear(int dir)
    {
        // 適格・トラバーサル可能なノードを (Order, 登録順) で整列。
        _ordered.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            if (e.Node.CanRequestFocus && e.Traversable && IsEligible(e.Scope))
            {
                _ordered.Add((e.Node, e.Node.Order, i));
            }
        }

        if (_ordered.Count == 0)
        {
            return false;
        }

        _ordered.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Index.CompareTo(b.Index));

        int current = -1;
        for (int i = 0; i < _ordered.Count; i++)
        {
            if (ReferenceEquals(_ordered[i].Node, Focused))
            {
                current = i;
                break;
            }
        }

        int next = current < 0
            ? (dir > 0 ? 0 : _ordered.Count - 1)
            : (((current + dir) % _ordered.Count) + _ordered.Count) % _ordered.Count;

        RequestFocus(_ordered[next].Node);
        return true;
    }

    private readonly List<(FocusNode Node, int Order, int Index)> _ordered = new(); // MoveLinear の作業バッファ（再利用）

    internal void Register(FocusNode node, Func<Rect> bounds, FocusScopeNode? scope = null, IScrollable? scrollable = null, bool traversable = true)
    {
        _entries.Add(new Entry(node, bounds, scope, scrollable, traversable));

        // トラップ中のスコープに属するノードが登録され、フォーカスがそのスコープ外にあるなら引き込む。
        FocusScopeNode? trap = ActiveTrap;
        if (trap is not null && scope == trap && (Focused is null || ScopeOf(Focused) != trap) && node.CanRequestFocus)
        {
            RequestFocus(node);
        }
    }

    /// <summary>Scrolls the currently focused node into the visible range of its scroll member (scroll-to-focus).</summary>
    internal void RevealFocused()
    {
        if (Focused is null)
        {
            return;
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Node == Focused && _entries[i].Scroll is IScrollable scroll)
            {
                scroll.RevealRect(_entries[i].Bounds());
                return;
            }
        }
    }

    internal void Unregister(FocusNode node)
    {
        _entries.RemoveAll(e => e.Node == node);
        if (Focused == node)
        {
            Focused.HasFocus = false;
            Focused.OnFocusChange?.Invoke(false);
            Focused = null;

            // フォーカス中の要素が消えたら、残った適格ノードへ移す（モーダル閉じ等で基底へ戻る）。
            FocusNode? next = FirstFocusable();
            if (next is not null)
            {
                RequestFocus(next);
            }
        }
    }

    /// <summary>Push a focus scope (modal, etc.). When <see cref="FocusScopeNode.Trap"/> is true, movement is confined inside it.</summary>
    internal void PushScope(FocusScopeNode scope)
    {
        if (scope.Trap)
        {
            _trapStack.Add(scope);
        }
    }

    /// <summary>Pop the focus scope.</summary>
    internal void PopScope(FocusScopeNode scope)
    {
        if (scope.Trap)
        {
            _trapStack.Remove(scope);
        }

        if (Focused is not null && !IsEligible(ScopeOf(Focused)))
        {
            Focused.HasFocus = false;
            Focused.OnFocusChange?.Invoke(false);
            Focused = null;

            FocusNode? first = FirstFocusable();
            if (first is not null)
            {
                RequestFocus(first);
            }
        }
    }

    internal void AutofocusIfNone(FocusNode node)
    {
        if (Focused is null && IsEligible(ScopeOf(node)))
        {
            RequestFocus(node);
        }
    }

    private FocusScopeNode? ActiveTrap => _trapStack.Count > 0 ? _trapStack[_trapStack.Count - 1] : null;

    private bool IsEligible(FocusScopeNode? scope)
    {
        FocusScopeNode? trap = ActiveTrap;
        return trap is null || scope == trap;
    }

    /// <summary>
    /// If there is an active trap (modal), focus must not move to nodes outside of it (this prevents focus
    /// from leaking out of the modal via tap or programmatic RequestFocus). Trap matching for newly registered
    /// nodes is handled separately, immediately in Register.
    /// </summary>
    private bool EligibleForFocus(FocusNode node)
    {
        if (ActiveTrap is null)
        {
            return true;
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Node == node)
            {
                return IsEligible(_entries[i].Scope);
            }
        }

        return true; // 未登録（テスト等）は許可
    }

    private FocusScopeNode? ScopeOf(FocusNode node)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Node == node)
            {
                return _entries[i].Scope;
            }
        }

        return null;
    }

    private FocusNode? ResolveId(int id)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Node.Id == id && IsEligible(_entries[i].Scope))
            {
                return _entries[i].Node;
            }
        }

        return null;
    }

    private FocusNode? FirstFocusable()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Node.CanRequestFocus && IsEligible(_entries[i].Scope))
            {
                return _entries[i].Node;
            }
        }

        return null;
    }

    /// <summary>The rectangle of the currently focused node (after layout).</summary>
    internal Rect? FocusedNodeBounds() => Focused is null ? null : BoundsOf(Focused);

    private Rect BoundsOf(FocusNode node)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Node == node)
            {
                return _entries[i].Bounds();
            }
        }

        return default;
    }

    private static float DirectionalScore(Rect from, Rect to, FocusDirection direction)
    {
        float fx = from.X + from.Width / 2f;
        float fy = from.Y + from.Height / 2f;
        float tx = to.X + to.Width / 2f;
        float ty = to.Y + to.Height / 2f;
        float dx = tx - fx;
        float dy = ty - fy;

        return direction switch
        {
            FocusDirection.Right => dx > 0f ? dx + (Math.Abs(dy) * 2f) : -1f,
            FocusDirection.Left => dx < 0f ? -dx + (Math.Abs(dy) * 2f) : -1f,
            FocusDirection.Down => dy > 0f ? dy + (Math.Abs(dx) * 2f) : -1f,
            FocusDirection.Up => dy < 0f ? -dy + (Math.Abs(dx) * 2f) : -1f,
            _ => -1f,
        };
    }

    private readonly struct Entry
    {
        public Entry(FocusNode node, Func<Rect> bounds, FocusScopeNode? scope, IScrollable? scroll, bool traversable)
        {
            Node = node;
            Bounds = bounds;
            Scope = scope;
            Scroll = scroll;
            Traversable = traversable;
        }

        public FocusNode Node { get; }

        public Func<Rect> Bounds { get; }

        /// <summary>The focus scope to which it belongs (null = global).</summary>
        public FocusScopeNode? Scope { get; }

        /// <summary>The nearest scroll element to which it belongs (for scroll-to-focus, otherwise null).</summary>
        public IScrollable? Scroll { get; }

        /// <summary>Whether this entry participates in directional traversal (even when false, explicit link navigation and tap-to-focus still work).</summary>
        public bool Traversable { get; }
    }
}

/// <summary>
/// Settings for which physical buttons are assigned to focus movement and OK/Cancel (specify during initial
/// setup). Defaults are D-pad up/down/left/right, A = Activate, B = Dismiss. If you are not using
/// <see cref="HamonRoot.HandleButtonDown"/>, call <see cref="FocusManager.MoveFocus"/>/Dispatch directly instead.
/// </summary>
public sealed class FocusBindings
{
    public Dictionary<GamepadButton, FocusDirection> Directional { get; } = new()
    {
        { GamepadButton.DpadUp, FocusDirection.Up },
        { GamepadButton.DpadDown, FocusDirection.Down },
        { GamepadButton.DpadLeft, FocusDirection.Left },
        { GamepadButton.DpadRight, FocusDirection.Right },
    };

    public GamepadButton ActivateButton { get; set; } = GamepadButton.A;

    public GamepadButton DismissButton { get; set; } = GamepadButton.B;
}

/// <summary>
/// Settings for gamepad button press-and-hold behavior (automatic repeat/long press); adjust via
/// <see cref="HamonRoot.Hold"/>. Repeat is equivalent to key repeat (initial delay, then a fixed interval).
/// </summary>
public sealed class GamepadHoldSettings
{
    /// <summary>Whether to enable auto-repeat (default true).</summary>
    public bool RepeatEnabled { get; set; } = true;

    /// <summary>Initial delay (in seconds) between press and start of repeat.</summary>
    public float RepeatDelay { get; set; } = 0.4f;

    /// <summary>Repeat interval (seconds).</summary>
    public float RepeatInterval { get; set; } = 0.08f;

    /// <summary>
    /// If true (default), auto-repeat only applies to directional buttons (i.e. focus auto-navigation),
    /// preventing <see cref="FocusNode.OnButtonRepeat"/> from firing accidentally for other buttons.
    /// </summary>
    public bool RepeatDirectionalOnly { get; set; } = true;

    /// <summary>Number of seconds the button must be held to register a long press (default 0.5).</summary>
    public float LongPressDuration { get; set; } = 0.5f;
}

/// <summary>
/// Makes its child focusable (equivalent to Flutter's <c>Focus</c>), registering with the
/// <see cref="FocusManager"/> and drawing a focus ring while focused. Set <see cref="Autofocus"/> to obtain
/// initial focus.
/// </summary>
public sealed class Focus : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public FocusNode Node { get; init; } = new();

    public bool Autofocus { get; init; }

    public Color RingColor { get; init; } = new(120, 180, 255);

    /// <summary>Individual focus appearance (border plus optional background/rounded corners); takes priority over <see cref="RingColor"/>.</summary>
    public FocusDecoration? Decoration { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Box };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new FocusElement(this);
}

/// <summary>The element backing <see cref="Focus"/>. Registers with the <see cref="FocusManager"/> and draws a focus frame.</summary>
internal sealed class FocusElement : RenderElement
{
    // 登録した FocusNode を Mount 時に捕捉する。build が毎回 `new Focus { Node = new() }` を返しても
    // 登録/解除を同一インスタンスで対にする（Widget.Node の差し替えで解除漏れ＝リークしない）。
    private FocusNode _node = null!;

    public FocusElement(Focus widget)
        : base(widget)
    {
    }

    internal override FocusNode? FocusNodeOrNull => _node;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        _node = ((Focus)Widget).Node;
        if (context.Focusable)
        {
            context.Focus?.Register(_node, () => LayoutNode.Bounds, EnclosingScope(), EnclosingScrollable(), context.Traversable);
            if (((Focus)Widget).Autofocus)
            {
                context.Focus?.AutofocusIfNone(_node);
            }
        }
    }

    public override void Unmount()
    {
        Context.Focus?.Unregister(_node);
        base.Unmount();
    }

    public override void Paint(in PaintContext context)
    {
        base.Paint(context);

        // 全体フォーカスカーソルが有効なときは個別枠を描かない（カーソルが最前面で表現する）。
        if (_node.HasFocus && Context.Owner?.CursorEnabled != true)
        {
            var focus = (Focus)Widget;
            Rect bounds = LayoutNode.Bounds;
            if (focus.Decoration is FocusDecoration d)
            {
                if (d.Background is Color bg)
                {
                    context.FillRoundedRect(bounds, bg, d.Radius);
                }

                context.DrawOutline(bounds, d.Outline, d.Thickness);
            }
            else
            {
                context.DrawOutline(bounds, focus.RingColor, 2f);
            }
        }
    }
}

/// <summary>
/// Groups child focus nodes together (equivalent to Flutter's <c>FocusScope</c>). When <see cref="Trap"/>
/// (default true), this becomes a modal focus trap, confining directional movement and initial focus inside
/// it. Wrap dialogs/bottom sheets with this when presenting them.
/// </summary>
public sealed class FocusScope : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public FocusScopeNode Node { get; init; } = new();

    /// <summary>Keep focus within this scope (default true).</summary>
    public bool Trap { get; init; } = true;

    // 単一子を充填する Stack（Tight 制約下では子が領域いっぱい＝全画面ルート、loose 下では content サイズ）。
    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new FocusScopeElement(this);
}

/// <summary>The element backing <see cref="FocusScope"/>.</summary>
internal sealed class FocusScopeElement : RenderElement
{
    // PushScope した FocusScopeNode を Mount 時に捕捉する。build が毎リビルドで `new FocusScope { Node = new() }`
    // を返しても、Push と Pop を同一インスタンスで対にする（リビルドを跨いでもトラップが解除漏れ＝リークしない）。
    private FocusScopeNode _node = null!;

    public FocusScopeElement(FocusScope widget)
        : base(widget)
    {
    }

    internal override FocusScopeNode? ScopeNodeOrNull => _node;

    public override void Mount(Element? parent, BuildContext context)
    {
        _node = ((FocusScope)Widget).Node;
        _node.Trap = ((FocusScope)Widget).Trap;
        context.Focus?.PushScope(_node); // 子の登録（base.Mount）より前に積む＝トラップ引き込みを有効化
        base.Mount(parent, context);
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        _node.Trap = ((FocusScope)newWidget).Trap; // リビルドで Trap だけ反映（スコープ実体は据え置き）
    }

    public override void Unmount()
    {
        Context.Focus?.PopScope(_node);
        base.Unmount();
    }
}
