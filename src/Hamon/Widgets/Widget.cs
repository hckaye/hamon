using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// An immutable UI blueprint. Diffed and applied to an <see cref="Element"/> (not on every frame).
/// <see cref="Key"/> is used to determine identity (for reuse, reordering, removal). Widgets hold no runtime
/// resources (such as a text renderer); those are supplied via <see cref="BuildContext"/>.
/// </summary>
public abstract class Widget
{
    /// <summary>An optional, stable identifier used to preserve the corresponding entity across reordering and insertion.</summary>
    public object? Key { get; init; }

    /// <summary>Creates the <see cref="Element"/> that backs this widget.</summary>
    public abstract Element CreateElement();

    /// <summary>Returns whether an existing <see cref="Element"/> can be updated in place rather than recreated - true when both widgets are the same type and their keys match.</summary>
    public static bool CanUpdate(Widget oldWidget, Widget newWidget) =>
        oldWidget.GetType() == newWidget.GetType() && Equals(oldWidget.Key, newWidget.Key);
}

/// <summary>
/// Runtime context (such as the text renderer) that flows down the widget tree,
/// propagating from parent to child.
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

    /// <summary>The current atom scope (the override chain established by <see cref="Provider{T}"/>).</summary>
    internal AtomScope? AtomScope { get; }

    /// <summary>The current atom store (a separate store established by <see cref="StoreProvider"/>).</summary>
    internal AtomStore? Store { get; }

    /// <summary>Whether a <see cref="FocusNode"/> can be registered within this subtree (controlled by <see cref="FocusableActionDetector"/>).</summary>
    internal bool Focusable => !_focusBlocked;

    /// <summary>Whether this subtree participates in directional traversal (explicit focus is still possible even when false).</summary>
    internal bool Traversable => !_traversalBlocked;

    /// <summary>Returns a context with focusability/traversability narrowed for the child (combined with ancestor restrictions via logical AND).</summary>
    internal BuildContext WithFocusGating(bool focusable, bool traversable) =>
        new(Text, Focus, Owner, _theme, AtomScope, Store, _focusBlocked || !focusable, _traversalBlocked || !traversable);

    /// <summary>Returns the context with the atom scope set (established by <see cref="Provider{T}"/>).</summary>
    internal BuildContext WithAtomScope(AtomScope? scope) => new(Text, Focus, Owner, _theme, scope, Store, _focusBlocked, _traversalBlocked);

    /// <summary>Returns the context with the atom store replaced (established by <see cref="StoreProvider"/>).</summary>
    internal BuildContext WithStore(AtomStore store) => new(Text, Focus, Owner, _theme, AtomScope, store, _focusBlocked, _traversalBlocked);

    public ITextRenderer? Text { get; }

    /// <summary>Focus management (gamepad directional movement, OK/Cancel).</summary>
    public FocusManager? Focus { get; }

    /// <summary>The host.</summary>
    public IHamonHost? Owner { get; }

    /// <summary>
    /// The active UI style. When <see cref="Owner"/> is the visual host, this refers <b>live</b> to
    /// <see cref="HamonRoot.EffectiveTheme"/> (resolved from the theme mode plus <see cref="HamonRoot.DarkTheme"/>),
    /// so replacing the theme or mode at runtime is reflected on redraw even without a rebuild - it is not bound
    /// to the context snapshot captured when the element was created. Otherwise, this is a build-time snapshot,
    /// falling back to <see cref="HamonTheme.Default"/> (Ripple Light) if none was supplied.
    /// </summary>
    public HamonTheme Theme => (Owner as HamonRoot)?.EffectiveTheme ?? _theme ?? HamonTheme.Default;

    public static BuildContext Empty => default;
}

/// <summary>
/// A persistent UI entity. Children are updated via keyed reconciliation. Runtime resources are obtained
/// from <see cref="Context"/>.
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

    /// <summary>Paints using the confirmed <see cref="LayoutNode.Bounds"/> (does nothing by default).</summary>
    public virtual void Paint(in PaintContext context)
    {
    }

    /// <summary>
    /// Rebuilds only this entity, avoiding a whole-tree reconcile (used by <see cref="Bind{T}"/> and similar
    /// constructs for frequent updates). Called by <see cref="HamonRoot"/> for entities registered as dirty.
    /// </summary>
    internal virtual void RebuildInPlace()
    {
    }

    /// <summary>Child elements (used for hit testing and paint traversal).</summary>
    public virtual IReadOnlyList<Element> Children => Array.Empty<Element>();

    /// <summary>Whether this entity receives pointer (touch/mouse) events; true for <see cref="GestureDetector"/> and similar.</summary>
    public virtual bool WantsPointer => false;

    /// <summary>Handles a pointer event that hit this entity.</summary>
    public virtual void HandlePointer(in PointerEvent pointer)
    {
    }

    /// <summary>
    /// The transform applied when performing hit testing and delivering pointer events to children, mapping
    /// from this element's coordinate space into the child's coordinate space.
    /// Elements that accumulate a transform when painting children via <see cref="PaintContext.WithTransform"/>
    /// (such as <see cref="Transform"/> / <see cref="InteractiveViewer"/>) should return the <b>inverse</b> of
    /// that transform here, so that hit testing and pointer position (after capture) match what is displayed.
    /// Null for elements that only paint and do not transform their children.
    /// </summary>
    internal virtual Transform2D? ChildHitTestTransform => null;

    /// <summary>The <see cref="FocusNode"/> held by this entity, if any.</summary>
    internal virtual FocusNode? FocusNodeOrNull => null;

    /// <summary>The <see cref="FocusScopeNode"/> held by this entity, if any.</summary>
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

    /// <summary>The nearest <see cref="FocusScopeNode"/>, including this entity itself (the scope this entity belonged to at focus-registration time).</summary>
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

/// <summary>Entry point that applies the root widget to the element tree.</summary>
public static class Reconciler
{
    /// <summary>
    /// Updates the existing <paramref name="current"/> element with <paramref name="widget"/>, or recreates
    /// it if identity cannot be preserved.
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
