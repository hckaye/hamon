using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A general-purpose primitive that handles focus, hover, press, and gamepad operations together (equivalent
/// to Flutter's <c>FocusableActionDetector</c>). A base for building custom interactive widgets
/// (buttons/slots/toggles, etc.); <see cref="Button"/> is also implemented on top of this.
/// <para>
/// State changes (<see cref="WidgetState"/>) are reported via <see cref="OnShowFocusHighlight"/>/
/// <see cref="OnShowHoverHighlight"/>/<see cref="OnFocusChange"/>. With <see cref="Builder"/> (a Hamon
/// extension), you can reassemble the children according to state.
/// </para>
/// <para>
/// <b>Hamon extension</b>: in addition to Flutter's Shortcuts/Actions, gamepad OK/Cancel is exposed via
/// <see cref="OnActivate"/>/<see cref="OnDismiss"/> (directional movement is handled by <see cref="HamonRoot"/>).
/// </para>
/// </summary>
public sealed class FocusableActionDetector : Widget
{
    /// <summary>A fixed child that does not depend on state (ignored if <see cref="Builder"/> is set).</summary>
    public Widget? Child { get; init; }

    /// <summary>
    /// A builder that creates children according to state (a Hamon extension — an escape hatch added on top of
    /// Flutter's callback-based approach). When specified, the child is re-created with this builder every time
    /// the state changes, so you can freely configure the appearance for press/hover/focus/disabled states.
    /// </summary>
    public Func<WidgetState, Widget>? Builder { get; init; }

    /// <summary>Whether the widget can be operated (false = does not receive input and does not register focus = <see cref="WidgetState.Disabled"/>).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Focus target (kept by caller = does not lose state on rebuild).</summary>
    public FocusNode Node { get; init; } = new();

    /// <summary>If nothing else has focus when this widget is mounted, it takes focus.</summary>
    public bool Autofocus { get; init; }

    /// <summary>Whether descendant <see cref="FocusNode"/> instances can be focusable (false disables internal focus).</summary>
    public bool DescendantsAreFocusable { get; init; } = true;

    /// <summary>Whether descendants are subject to directional traversal (false, the interior is not subject to tabbing).</summary>
    public bool DescendantsAreTraversable { get; init; } = true;

    /// <summary>Physical button to intent-name mapping (equivalent to Flutter's <c>shortcuts</c>), resolved via <see cref="Actions"/>.</summary>
    public IReadOnlyDictionary<GamepadButton, string>? Shortcuts { get; init; }

    /// <summary>Intent name to handler mapping (equivalent to Flutter's <c>actions</c>), resolved from <see cref="Shortcuts"/>.</summary>
    public IReadOnlyDictionary<string, Action>? Actions { get; init; }

    /// <summary>Called when the focus highlight visibility changes (equivalent to Flutter's <c>onShowFocusHighlight</c>).</summary>
    public Action<bool>? OnShowFocusHighlight { get; init; }

    /// <summary>Called when the hover highlight visibility changes (equivalent to Flutter's <c>onShowHoverHighlight</c>).</summary>
    public Action<bool>? OnShowHoverHighlight { get; init; }

    /// <summary>Called when focus changes (equivalent to Flutter's <c>onFocusChange</c>).</summary>
    public Action<bool>? OnFocusChange { get; init; }

    /// <summary>Fires on OK (gamepad A / Enter / tap).</summary>
    public Action? OnActivate { get; init; }

    /// <summary>Fires on Cancel (gamepad B / Esc).</summary>
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
/// The element backing <see cref="FocusableActionDetector"/>. Combines gamepad delivery and consolidates
/// state into <see cref="WidgetState"/>, reassembling children when
/// <see cref="FocusableActionDetector.Builder"/> is specified.
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
