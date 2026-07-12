using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Immutable UI blueprint. <see cref="Element"/>differentially applied to
/// (Not every frame).<see cref="Key"/>is used for identity determination (reuse/sort/delete).
/// Does not have runtime resources (text renderer, etc.)<see cref="BuildContext"/>Supply via.
/// </summary>
public abstract class Widget
{
    /// <summary>A stable identifier (optional) to preserve the entity during sorting and insertion.</summary>
    public object? Key { get; init; }

    /// <summary>Generate a holding entity corresponding to this widget.</summary>
    public abstract Element CreateElement();

    /// <summary>If they are of the same type and the keys match, you can update the existing Element without recreating it.</summary>
    public static bool CanUpdate(Widget oldWidget, Widget newWidget) =>
        oldWidget.GetType() == newWidget.GetType() && Equals(oldWidget.Key, newWidget.Key);
}

/// <summary>
/// Runtime context (text renderer, etc.) that flows to the tree.
/// Propagates from parent to child.
/// </summary>
public readonly struct BuildContext
{
    private readonly HamonTheme? _theme;

    // gating はブロックフラグ（false=許可）で持つ＝`default(BuildContext)` がフルにフォーカス可能になる。
    private readonly bool _focusBlocked;
    private readonly bool _traversalBlocked;

    public BuildContext(ITextRenderer? text, FocusManager? focus = null, HamonRoot? owner = null, HamonTheme? theme = null)
    {
        Text = text;
        Focus = focus;
        Owner = owner;
        _theme = theme;
        AtomScope = null;
        Store = null;
        _focusBlocked = false;
        _traversalBlocked = false;
    }

    private BuildContext(ITextRenderer? text, FocusManager? focus, IHamonHost? owner, HamonTheme? theme, AtomScope? atomScope, AtomStore? store, bool focusBlocked, bool traversalBlocked)
    {
        Text = text;
        Focus = focus;
        Owner = owner;
        _theme = theme;
        AtomScope = atomScope;
        Store = store;
        _focusBlocked = focusBlocked;
        _traversalBlocked = traversalBlocked;
    }

    /// <summary>The current atom scope (<see cref="Provider{T}"/>overwrite chain. </summary>
    internal AtomScope? AtomScope { get; }

    /// <summary>Current atom store (<see cref="StoreProvider"/>Separate store. </summary>
    internal AtomStore? Store { get; }

    /// <summary>This partial tree<see cref="FocusNode"/>Can focus be registered? (<see cref="FocusableActionDetector"/>).</summary>
    internal bool Focusable => !_focusBlocked;

    /// <summary>Whether this subtree is subject to direction traversal (explicit focus is possible even if false).</summary>
    internal bool Traversable => !_traversalBlocked;

    /// <summary>Returns the context that narrows down focusability/traversability to be passed to the child (ANDed with ancestor restrictions).</summary>
    internal BuildContext WithFocusGating(bool focusable, bool traversable) =>
        new(Text, Focus, Owner, _theme, AtomScope, Store, _focusBlocked || !focusable, _traversalBlocked || !traversable);

    /// <summary>Returns the context plus the atom scope (<see cref="Provider{T}"/>).</summary>
    internal BuildContext WithAtomScope(AtomScope? scope) => new(Text, Focus, Owner, _theme, scope, Store, _focusBlocked, _traversalBlocked);

    /// <summary>Returns the context with the atom store replaced (<see cref="StoreProvider"/>).</summary>
    internal BuildContext WithStore(AtomStore store) => new(Text, Focus, Owner, _theme, AtomScope, store, _focusBlocked, _traversalBlocked);

    public ITextRenderer? Text { get; }

    /// <summary>Focus management (gamepad direction movement/OK/Cancel).</summary>
    public FocusManager? Focus { get; }

    /// <summary>host. </summary>
    public IHamonHost? Owner { get; }

    /// <summary>
    /// Default UI style.<see cref="HamonRoot.EffectiveTheme"/>(after solving mode+DarkTheme)<b>live</b>For reference,
    /// If you replace the theme or mode at runtime, it will be reflected even if you redraw without rebuilding (it is not bound by the context snapshot of the reused element).
    /// Build-time snapshot if Owner is not the visual host, otherwise<see cref="HamonTheme.Default"/>(Ripple Light).
    /// </summary>
    public HamonTheme Theme => (Owner as HamonRoot)?.EffectiveTheme ?? _theme ?? HamonTheme.Default;

    public static BuildContext Empty => default;
}

/// <summary>
/// UI entity to be preserved.
/// child is updated with keyed reconcile.<see cref="Context"/>Get runtime resources from.
/// </summary>
public abstract class Element
{
    protected Element(Widget widget) => Widget = widget;

    public Widget Widget { get; protected set; }

    public Element? Parent { get; private set; }

    public BuildContext Context { get; private set; }

    public bool IsMounted { get; private set; }

    /// <summary>The layout node that this entity contributes to.</summary>
    public abstract LayoutNode LayoutNode { get; }

    public virtual void Mount(Element? parent, BuildContext context)
    {
        Parent = parent;
        Context = context;
        IsMounted = true;
    }

    public virtual void Update(Widget newWidget) => Widget = newWidget;

    public virtual void Unmount() => IsMounted = false;

    /// <summary>confirmed<see cref="LayoutNode.Bounds"/>(default is no drawing).</summary>
    public virtual void Paint(in PaintContext context)
    {
    }

    /// <summary>
    /// Rebuild only this entity (avoid whole tree reconcile =<see cref="Bind{T}"/>etc.) for frequent updates).
    /// <see cref="HamonRoot"/>is called for dirty registered entities.
    /// </summary>
    internal virtual void RebuildInPlace()
    {
    }

    /// <summary>Fruiting body (for hit testing/traversing drawings). </summary>
    public virtual IReadOnlyList<Element> Children => Array.Empty<Element>();

    /// <summary>Does it receive pointer (touch/mouse)?<see cref="GestureDetector"/>etc. are true.</summary>
    public virtual bool WantsPointer => false;

    /// <summary>Handle hit pointer events.</summary>
    public virtual void HandlePointer(in PointerEvent pointer)
    {
    }

    /// <summary>
    /// The transformation by which the child's hit test/pointer delivery is multiplied (from this element's coordinate space to the child's coordinate space).
    /// Draw to child<see cref="PaintContext.WithTransform"/>The element that accumulates (<see cref="InteractiveViewer"/>etc.) is that<b>Inverse transformation</b>When you return
    /// Hit test and pointer position (after capture) matches display (<see cref="Transform"/>is null because it is only drawn).
    /// </summary>
    internal virtual Transform2D? ChildHitTestTransform => null;

    /// <summary>This entity holds<see cref="FocusNode"/>(if any). </summary>
    internal virtual FocusNode? FocusNodeOrNull => null;

    /// <summary>This entity holds<see cref="FocusScopeNode"/>(if any). </summary>
    internal virtual FocusScopeNode? ScopeNodeOrNull => null;

    /// <summary>Nearest ancestor scroll element (for scroll-to-focus; null if not present).</summary>
    internal IScrollable? EnclosingScrollable()
    {
        for (Element? current = Parent; current is not null; current = current.Parent)
        {
            if (current is IScrollable scrollable)
            {
                return scrollable;
            }
        }

        return null;
    }

    /// <summary>closest ancestor including yourself<see cref="FocusScopeNode"/>(belonging scope at the time of focus registration).</summary>
    internal FocusScopeNode? EnclosingScope()
    {
        for (Element? current = this; current is not null; current = current.Parent)
        {
            if (current.ScopeNodeOrNull is FocusScopeNode scope)
            {
                return scope;
            }
        }

        return null;
    }
}

/// <summary>Entry that reflects the root widget to the tree.</summary>
public static class Reconciler
{
    /// <summary>
    /// Existing route<paramref name="current"/>of<paramref name="widget"/>Update with.
    /// If there is no identity, recreate it.
    /// </summary>
    public static Element Reconcile(Element? current, Widget widget, BuildContext context = default)
    {
        if (current is not null && Widget.CanUpdate(current.Widget, widget))
        {
            current.Update(widget);
            return current;
        }

        current?.Unmount();
        Element element = widget.CreateElement();
        element.Mount(null, context);
        return element;
    }
}
