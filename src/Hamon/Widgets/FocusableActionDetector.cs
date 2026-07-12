using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A general-purpose primitive that collectively handles focus, hover, press, and gamepad operations (Flutter<c>FocusableActionDetector</c>）。
/// A base for building your own interactive widgets (buttons/slots/toggles, etc.)<see cref="Button"/>is also implemented on top of this.
/// <para>
/// situation(<see cref="WidgetState"/>) is the change in<see cref="OnShowFocusHighlight"/>/<see cref="OnShowHoverHighlight"/>/
/// <see cref="OnFocusChange"/>to notify you with<see cref="Builder"/>(Hamon extension), you can reassemble the children according to the state.
/// </para>
/// <para>
/// <b>Hamon expansion</b>: In addition to Flutter's Shortcuts/Actions, OK/Cancel of the gamepad<see cref="OnActivate"/>/
/// <see cref="OnDismiss"/>(direction movement is<see cref="HamonRoot"/>).
/// </para>
/// </summary>
public sealed class FocusableActionDetector : Widget
{
    /// <summary>A fixed child that does not depend on the state (<see cref="Builder"/>).</summary>
    public Widget? Child { get; init; }

    /// <summary>
    /// A builder that creates children according to the state (Hamon extension = escape hatch added to Flutter's callback method).
    /// When specified, the child is re-created with this builder every time the state changes (you can freely configure the appearance of press/hover/focus/disabled).
    /// </summary>
    public Func<WidgetState, Widget>? Builder { get; init; }

    /// <summary>Is it possible to operate (false = do not receive input, do not register focus =<see cref="WidgetState.Disabled"/>）。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Focus target (kept by caller = does not lose state on rebuild).</summary>
    public FocusNode Node { get; init; } = new();

    /// <summary>If there is no other focus when mounting, I will take it.</summary>
    public bool Autofocus { get; init; }

    /// <summary>of descendants<see cref="FocusNode"/>whether to make it focusable (false disables internal focus).</summary>
    public bool DescendantsAreFocusable { get; init; } = true;

    /// <summary>Whether descendants are subject to directional traversal (false, the interior is not subject to tabbing).</summary>
    public bool DescendantsAreTraversable { get; init; } = true;

    /// <summary>Physical button → Intent name assignment (Flutter<c>shortcuts</c>). <see cref="Actions"/>subtract.</summary>
    public IReadOnlyDictionary<GamepadButton, string>? Shortcuts { get; init; }

    /// <summary>Intent name → Handler (Flutter<c>actions</c>）。<see cref="Shortcuts"/>Resolved to.</summary>
    public IReadOnlyDictionary<string, Action>? Actions { get; init; }

    /// <summary>When focus highlight display status changes (Flutter<c>onShowFocusHighlight</c>）。</summary>
    public Action<bool>? OnShowFocusHighlight { get; init; }

    /// <summary>When the ability to display hover highlights changes (Flutter<c>onShowHoverHighlight</c>）。</summary>
    public Action<bool>? OnShowHoverHighlight { get; init; }

    /// <summary>When focus changes (Flutter<c>onFocusChange</c>）。</summary>
    public Action<bool>? OnFocusChange { get; init; }

    /// <summary>Fire with OK (Gamepad A / Enter / Tap). </summary>
    public Action? OnActivate { get; init; }

    /// <summary>Cancel (gamepad B/Esc) fires. </summary>
    public Action? OnDismiss { get; init; }

    /// <summary>Cursor to present while hovering.</summary>
    public MouseCursor MouseCursor { get; init; } = MouseCursor.Basic;

    /// <summary>Cursor to be presented when disabled (default = cannot be operated).</summary>
    public MouseCursor DisabledCursor { get; init; } = MouseCursor.Forbidden;

    /// <summary>Should semantics include focusability (for future accessibility)?</summary>
    public bool IncludeFocusSemantics { get; init; } = true;

    /// <summary>Accessibility labels (read by screen readers).</summary>
    public string? SemanticLabel { get; init; }

    public override Element CreateElement() => new FocusableActionDetectorElement(this);
}

/// <summary>
/// <see cref="FocusableActionDetector"/>holding entity.
/// Combine gamepad shipping<see cref="WidgetState"/>Consolidate to.
/// （<see cref="FocusableActionDetector.Builder"/>(When specified) Reassembles the children.
/// </summary>
internal sealed class FocusableActionDetectorElement : Element, IHoverTarget
{
    // 子を内包する安定 Box ノード（子の型が変わっても親のレイアウト木への配線を保つ＝局所 rebuild 安全）。
    private readonly LayoutNode _node = new(new Style { Kind = LayoutKind.Box }, null);
    private Element? _child;
    private Element[] _childArray = Array.Empty<Element>();
    private WidgetState _states;
    private bool _pressed;
    private bool _inside;
    private bool _registered;

    public FocusableActionDetectorElement(FocusableActionDetector widget)
        : base(widget)
    {
    }

    private FocusableActionDetector W => (FocusableActionDetector)Widget;

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _childArray;

    public override bool WantsPointer => W.Enabled;

    internal override FocusNode? FocusNodeOrNull => W.Enabled ? W.Node : null;

    bool IHoverTarget.HoverOpaque => true;

    MouseCursor IHoverTarget.HoverCursor => W.Enabled ? W.MouseCursor : W.DisabledCursor;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        ConfigureNode();
        RegisterIfEnabled();
        _states = W.Enabled ? WidgetState.None : WidgetState.Disabled;
        if (W.Node.HasFocus)
        {
            _states |= WidgetState.Focused;
        }

        BuildChild();
    }

    public override void Update(Widget newWidget)
    {
        bool wasRegistered = _registered;
        base.Update(newWidget);
        ConfigureNode();

        // Enabled の変化に応じて登録/解除と Disabled ビットを更新する。
        if (W.Enabled && !wasRegistered)
        {
            RegisterIfEnabled();
        }
        else if (!W.Enabled && wasRegistered)
        {
            UnregisterIfNeeded();
        }

        WidgetState next = W.Enabled ? (_states & ~WidgetState.Disabled) : (WidgetState.Disabled | (_states & WidgetState.Focused));
        ApplyStates(next);
        BuildChild();
    }

    public override void Unmount()
    {
        UnregisterIfNeeded();
        (Context.Owner as HamonRoot)?.NotifyHoverTargetUnmounted(this);
        _child?.Unmount();
        _child = null;
        _childArray = Array.Empty<Element>();
        _node.Clear();
        base.Unmount();
    }

    public override void Paint(in PaintContext context) => _child?.Paint(context);

    internal override void RebuildInPlace() => BuildChild();

    public override void HandlePointer(in PointerEvent pointer)
    {
        if (!W.Enabled)
        {
            return;
        }

        switch (pointer.Phase)
        {
            case PointerPhase.Down:
                _pressed = true;
                _inside = true;
                ApplyStates(_states | WidgetState.Pressed);
                break;

            case PointerPhase.Move when _pressed:
                bool inside = LayoutNode.Bounds.Contains(pointer.Position.X, pointer.Position.Y);
                if (inside != _inside)
                {
                    _inside = inside;
                    ApplyStates(inside ? _states | WidgetState.Pressed : _states & ~WidgetState.Pressed);
                }

                break;

            case PointerPhase.Up:
                if (_pressed && LayoutNode.Bounds.Contains(pointer.Position.X, pointer.Position.Y))
                {
                    W.OnActivate?.Invoke();
                }

                _pressed = false;
                _inside = false;
                ApplyStates(_states & ~WidgetState.Pressed);
                break;

            case PointerPhase.Cancel: // 調停でスクロール等に取られた／pointer cancel＝押下状態を解除
                _pressed = false;
                _inside = false;
                ApplyStates(_states & ~WidgetState.Pressed);
                break;
        }
    }

    void IHoverTarget.HoverEnter(Vec2 position)
    {
        if (W.Enabled)
        {
            ApplyStates(_states | WidgetState.Hovered);
        }
    }

    void IHoverTarget.HoverMove(Vec2 position)
    {
    }

    void IHoverTarget.HoverExit(Vec2 position) => ApplyStates(_states & ~WidgetState.Hovered);

    private void ConfigureNode()
    {
        FocusNode node = W.Node;
        node.CanRequestFocus = W.Enabled;
        node.SemanticLabel = W.IncludeFocusSemantics ? W.SemanticLabel : null;
        node.OnActivate = () =>
        {
            if (W.Enabled)
            {
                W.OnActivate?.Invoke();
            }
        };
        node.OnDismiss = () =>
        {
            if (W.Enabled)
            {
                W.OnDismiss?.Invoke();
            }
        };
        node.OnButtonDown = HandleButton;
        node.OnFocusChange = OnNodeFocusChange;
    }

    private void RegisterIfEnabled()
    {
        if (_registered || !W.Enabled || !Context.Focusable)
        {
            return;
        }

        Context.Focus?.Register(W.Node, () => LayoutNode.Bounds, EnclosingScope(), EnclosingScrollable(), Context.Traversable);
        _registered = true;
        if (W.Autofocus)
        {
            Context.Focus?.AutofocusIfNone(W.Node);
        }
    }

    private void UnregisterIfNeeded()
    {
        if (_registered)
        {
            Context.Focus?.Unregister(W.Node);
            _registered = false;
        }
    }

    private void HandleButton(GamepadButton button)
    {
        if (!W.Enabled || W.Shortcuts is null || W.Actions is null)
        {
            return;
        }

        if (W.Shortcuts.TryGetValue(button, out string? intent) && W.Actions.TryGetValue(intent, out Action? action))
        {
            action();
        }
    }

    private void OnNodeFocusChange(bool focused) =>
        ApplyStates(focused ? _states | WidgetState.Focused : _states & ~WidgetState.Focused);

    /// <summary>Update state, fire highlight/focus callbacks depending on changed bits + reassemble children if necessary.</summary>
    private void ApplyStates(WidgetState next)
    {
        WidgetState changed = _states ^ next;
        if (changed == WidgetState.None)
        {
            return;
        }

        _states = next;

        if (changed.Has(WidgetState.Focused))
        {
            bool focused = next.Has(WidgetState.Focused);
            W.OnFocusChange?.Invoke(focused);
            W.OnShowFocusHighlight?.Invoke(focused && W.Enabled);
        }

        if (changed.Has(WidgetState.Hovered))
        {
            W.OnShowHoverHighlight?.Invoke(next.Has(WidgetState.Hovered) && W.Enabled);
        }

        // Builder 利用時は状態に応じて子を組み直す（入力時のみ＝定常フレームでは走らない）。
        if (W.Builder is not null)
        {
            Context.Owner?.MarkElementDirty(this);
        }
    }

    private void BuildChild()
    {
        Widget built = W.Builder is not null ? W.Builder(_states) : (W.Child ?? new SizedBox());
        BuildContext childContext = Context.WithFocusGating(W.Enabled && W.DescendantsAreFocusable, W.DescendantsAreTraversable);

        if (_child is not null && Widget.CanUpdate(_child.Widget, built))
        {
            _child.Update(built);
        }
        else
        {
            _child?.Unmount();
            _child = built.CreateElement();
            _child.Mount(this, childContext);
            _childArray = new[] { _child };
        }

        // 子ノードを安定 Box へ配線し直す（型が変わっても親側は _node を見続ける）。
        _node.Clear();
        _node.Add(_child.LayoutNode);
    }
}
