using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A unit of reactive state (equivalent to an atom in jotai) — either <b>primitive</b> (has an initial value) or
/// <b>derived</b> (computed from other atoms). Its value is held in <see cref="HamonRoot"/>'s global store, or in
/// a separate store provided by <see cref="StoreProvider"/>. Subscribe to and update it via <c>Hooks.Watch/UseAtom</c>.
/// </summary>
public sealed class Atom<T>
{
    /// <summary>Primitive atom (readable and writable, with initial value).</summary>
    public Atom(T initial) => Initial = initial;

    /// <summary>Derived atom (calculated/read only from other atoms. Automatically recalculated on dependent changes).</summary>
    public Atom(Func<AtomGetter, T> compute) => Compute = compute;

    /// <summary>A bidirectional derived atom (similar to jotai): computed via <paramref name="read"/>, and updates other atoms via <paramref name="write"/>.</summary>
    public Atom(Func<AtomGetter, T> read, Action<AtomWriter, T> write)
    {
        Compute = read;
        Write = write;
    }

    /// <summary>A write-only (action) atom (equivalent to jotai's <c>atom(null, (get,set,arg)=>…)</c>).</summary>
    public Atom(Action<AtomWriter, T> write) => Write = write;

    internal T Initial { get; } = default!;

    internal Func<AtomGetter, T>? Compute { get; }

    /// <summary>The write function for a bidirectional/write-only atom. Updates other atoms via <see cref="AtomWriter"/>.</summary>
    internal Action<AtomWriter, T>? Write { get; }

    /// <summary>
    /// Subscription lifecycle hook (equivalent to jotai's <c>atom.onMount</c>).
    /// The return value is a cleanup callback, invoked when the last subscriber is removed.
    /// </summary>
    public Func<Action<T>, Action?>? OnMount { get; init; }

    internal bool IsDerived => Compute is not null;
}

/// <summary>Dependency tracker passed to a derived atom's compute function. Atoms read via <see cref="Get{U}"/> are registered as dependencies.</summary>
public sealed class AtomGetter
{
    private readonly AtomStore _store;
    private readonly List<AtomCell> _deps;

    internal AtomGetter(AtomStore store, List<AtomCell> deps)
    {
        _store = store;
        _deps = deps;
    }

    public U Get<U>(Atom<U> atom)
    {
        _deps.Add(_store.Cell(atom)); // 型付き Cell＝依存側の onMount も配線
        return (U)_store.Read(atom)!;
    }
}

/// <summary>Context passed to a bidirectional/write-only atom write expression. </summary>
public sealed class AtomWriter
{
    private readonly AtomStore _store;

    internal AtomWriter(AtomStore store) => _store = store;

    public U Get<U>(Atom<U> atom) => (U)_store.Read(atom)!;

    public void Set<U>(Atom<U> atom, U value) => _store.Set(atom, value);

    public void Reset<U>(Atom<U> atom) => _store.Reset(atom);
}

/// <summary>An atom's value and its subscribers (for change notifications). Shared per scope via <see cref="Provider{T}"/>.</summary>
public sealed class AtomCell
{
    private Action? _listeners;
    private int _count;

    internal object? Value { get; set; }

    internal bool HasValue { get; set; }

    /// <summary>Whether onMount has already been wired (prevents wiring it more than once).</summary>
    internal bool OnMountWired { get; set; }

    /// <summary>Called (atom onMount) when the first subscriber is added (0 → 1).</summary>
    internal Action? OnFirstListener { get; set; }

    /// <summary>Called when the last subscriber is removed (1→0) (onMount cleanup).</summary>
    internal Action? OnLastListener { get; set; }

    internal void Subscribe(Action listener)
    {
        _listeners += listener;
        if (++_count == 1)
        {
            OnFirstListener?.Invoke();
        }
    }

    internal void Unsubscribe(Action listener)
    {
        _listeners -= listener;
        if (--_count == 0)
        {
            OnLastListener?.Invoke();
        }
    }

    internal void Notify() => _listeners?.Invoke();

    /// <summary>Number of subscribers (useful for detecting missing unsubscriptions — leak detection, inspection, and testing).</summary>
    internal int ListenerCount => _count;
}

/// <summary>
/// An atom→cell store. The default is <see cref="HamonRoot"/>'s global store, or a separate store from
/// <see cref="StoreProvider"/>. Derived atoms are computed lazily and cached, and automatically recomputed when a
/// dependency changes (downstream listeners are notified only when the value actually changes).
/// </summary>
public sealed class AtomStore
{
    private readonly Dictionary<object, AtomCell> _cells = new();
    private readonly Dictionary<object, List<AtomCell>> _derivedDeps = new();
    private readonly Dictionary<object, Action> _recompute = new();

    public AtomCell Cell(object atom)
    {
        if (!_cells.TryGetValue(atom, out AtomCell? cell))
        {
            cell = new AtomCell();
            _cells[atom] = cell;
        }

        return cell;
    }

    /// <summary>Get typed cell. </summary>
    public AtomCell Cell<T>(Atom<T> atom)
    {
        AtomCell cell = Cell((object)atom);
        if (atom.OnMount is not null && !cell.OnMountWired)
        {
            cell.OnMountWired = true;
            Action? cleanup = null;
            cell.OnFirstListener = () => cleanup = atom.OnMount(value => Set(atom, value));
            cell.OnLastListener = () =>
            {
                cleanup?.Invoke();
                cleanup = null;
            };
        }

        return cell;
    }

    /// <summary>Read the value (calculate the derivative if not yet calculated).</summary>
    public object? Read<T>(Atom<T> atom)
    {
        AtomCell cell = Cell(atom);
        if (atom.IsDerived && !cell.HasValue)
        {
            Recompute(atom, cell);
        }

        return cell.HasValue ? cell.Value : atom.Initial;
    }

    /// <summary>Updates the atom's value.</summary>
    public void Set<T>(Atom<T> atom, T value)
    {
        if (atom.Write is not null)
        {
            atom.Write(new AtomWriter(this), value); // 双方向/アクション：他 atom を更新（再計算が伝播）
            return;
        }

        AtomCell cell = Cell(atom);
        cell.Value = value;
        cell.HasValue = true;
        cell.Notify();
    }

    /// <summary>Resets the atom to its initial value (equivalent to jotai's RESET).</summary>
    public void Reset<T>(Atom<T> atom)
    {
        AtomCell cell = Cell(atom);
        cell.Value = null;
        cell.HasValue = false; // 派生は次回 Read で再計算、プリミティブは Initial に戻る
        cell.Notify();
    }

    /// <summary>Seeds an initial value (used by <see cref="StoreProvider"/>'s initialValues).</summary>
    public void Seed<T>(Atom<T> atom, T value)
    {
        AtomCell cell = Cell(atom);
        cell.Value = value;
        cell.HasValue = true;
    }

    private void Recompute<T>(Atom<T> atom, AtomCell cell)
    {
        Action recompute = _recompute.TryGetValue(atom, out Action? existing)
            ? existing
            : _recompute[atom] = () => Recompute(atom, cell);

        var deps = new List<AtomCell>();
        T value = atom.Compute!(new AtomGetter(this, deps));

        // 新依存を先に購読してから旧依存を解除（持続する依存で購読者数が一時的に0にならず onMount が誤発火しない）。
        foreach (AtomCell c in deps)
        {
            c.Subscribe(recompute);
        }

        if (_derivedDeps.TryGetValue(atom, out List<AtomCell>? old))
        {
            foreach (AtomCell c in old)
            {
                c.Unsubscribe(recompute);
            }
        }

        _derivedDeps[atom] = deps;

        bool changed = !cell.HasValue || !Equals(cell.Value, value);
        cell.Value = value;
        cell.HasValue = true;
        if (changed)
        {
            cell.Notify(); // 値が変わったときだけ下流（要素/別派生）へ伝播
        }
    }
}

/// <summary>The scope, established by <see cref="Provider{T}"/>, that overrides an atom's value for a subtree (one node = one atom).</summary>
internal sealed class AtomScope
{
    public AtomScope(AtomScope? parent, object atom, AtomCell cell)
    {
        Parent = parent;
        Atom = atom;
        Cell = cell;
    }

    public AtomScope? Parent { get; }

    public object Atom { get; }

    public AtomCell Cell { get; }
}

/// <summary>
/// **Overrides** the value of an <see cref="Atom{T}"/> for a subtree (similar to Flutter's <c>InheritedWidget</c> or
/// a Provider-style value override). Descendants using <c>Watch/UseAtom</c> see the cell overridden by this scope
/// (nested providers override further). Note: jotai's "store boundary" — a fully independent separation of all
/// atoms — is instead provided by <see cref="StoreProvider"/>.
/// </summary>
public sealed class Provider<T> : Widget
{
    public Atom<T> Atom { get; init; } = null!;

    public T Value { get; init; } = default!;

    public Widget? Child { get; init; }

    public override Element CreateElement() => new ProviderElement<T>(this);
}

/// <summary>
/// Establishes an <b>independent atom store</b> for a subtree (equivalent to jotai's <c>Provider</c> store
/// boundary). Within it, <c>Watch/UseAtom</c> uses this separate store, so all atoms have values independent
/// from the global store and any outer scope (useful for state isolation and testing on a per-screen/per-modal basis).
/// </summary>
public sealed class StoreProvider : Widget
{
    public Widget? Child { get; init; }

    /// <summary>Initial seed data for the isolated store (equivalent to jotai's <c>initialValues</c>). For example: <c>s =&gt; s.Seed(atom, 5)</c>.</summary>
    public Action<AtomStore>? InitialValues { get; init; }

    public override Element CreateElement() => new StoreProviderElement(this);
}

/// <summary>
/// Creates and caches parameterized atoms (equivalent to jotai's <c>atomFamily</c>). Returns the same
/// <see cref="Atom{T}"/> for the same <typeparamref name="TKey"/> (e.g. per-entity state keyed by id).
/// </summary>
public sealed class AtomFamily<TKey, T>
    where TKey : notnull
{
    private readonly Func<TKey, Atom<T>> _create;
    private readonly Dictionary<TKey, Atom<T>> _cache = new();

    public AtomFamily(Func<TKey, Atom<T>> create) => _create = create;

    public Atom<T> this[TKey key]
    {
        get
        {
            if (!_cache.TryGetValue(key, out Atom<T>? atom))
            {
                atom = _create(key);
                _cache[key] = atom;
            }

            return atom;
        }
    }

    /// <summary>Discard the key from the cache (recreate it on next access).</summary>
    public void Remove(TKey key) => _cache.Remove(key);
}

/// <summary>The Element that hosts a <see cref="StoreProvider"/>.</summary>
internal sealed class StoreProviderElement : Element
{
    private readonly LayoutNode _node;
    private readonly AtomStore _store = new();
    private Element? _child;
    private Element[] _childArray = Array.Empty<Element>();

    public StoreProviderElement(StoreProvider widget)
        : base(widget)
    {
        _node = new LayoutNode(new Style { Kind = LayoutKind.Stack, StackExpandChildren = true });
    }

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _childArray;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        ((StoreProvider)Widget).InitialValues?.Invoke(_store); // initialValues シード
        BuildChild();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        BuildChild();
    }

    public override void Unmount()
    {
        _child?.Unmount();
        _child = null;
        _childArray = Array.Empty<Element>();
        _node.Clear();
        base.Unmount();
    }

    public override void Paint(in PaintContext context) => _child?.Paint(context);

    private void BuildChild()
    {
        Widget built = ((StoreProvider)Widget).Child ?? new SizedBox();
        BuildContext childContext = Context.WithStore(_store);
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

        _node.Clear();
        _node.Add(_child.LayoutNode);
    }
}

/// <summary>The Element that hosts a <see cref="Provider{T}"/>.</summary>
internal sealed class ProviderElement<T> : Element
{
    private readonly LayoutNode _node;
    private Element? _child;
    private Element[] _childArray = Array.Empty<Element>();
    private AtomScope? _scope;

    public ProviderElement(Provider<T> widget)
        : base(widget)
    {
        _node = new LayoutNode(new Style { Kind = LayoutKind.Stack, StackExpandChildren = true });
    }

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _childArray;

    private Provider<T> W => (Provider<T>)Widget;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        var cell = new AtomCell { Value = W.Value, HasValue = true };
        _scope = new AtomScope(context.AtomScope, W.Atom, cell);
        BuildChild();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        if (_scope is not null)
        {
            _scope.Cell.Value = W.Value;
            _scope.Cell.HasValue = true;
            _scope.Cell.Notify();
        }

        BuildChild();
    }

    public override void Unmount()
    {
        _child?.Unmount();
        _child = null;
        _childArray = Array.Empty<Element>();
        _node.Clear();
        base.Unmount();
    }

    public override void Paint(in PaintContext context) => _child?.Paint(context);

    private void BuildChild()
    {
        Widget built = W.Child ?? new SizedBox();
        BuildContext childContext = Context.WithAtomScope(_scope);
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

        _node.Clear();
        _node.Add(_child.LayoutNode);
    }
}
