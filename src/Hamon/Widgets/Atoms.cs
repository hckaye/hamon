using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Unit of reactive state (equivalent to atom in jotai).<b>primitive</b>(initial value) or<b>derivative</b>(calculated from other atoms).
/// The value is<see cref="HamonRoot"/>global store, or<see cref="StoreProvider"/>A separate store holds the
/// <c>Hooks.Watch/UseAtom</c>Subscribe and update at.
/// </summary>
public sealed class Atom<T>
{
    /// <summary>Primitive atom (readable and writable, with initial value).</summary>
    public Atom(T initial) => Initial = initial;

    /// <summary>Derived atom (calculated/read only from other atoms. Automatically recalculated on dependent changes).</summary>
    public Atom(Func<AtomGetter, T> compute) => Compute = compute;

    /// <summary>Bidirectional derived atom (similar to jotai:<paramref name="read"/>Calculate with,<paramref name="write"/>(update other atoms).</summary>
    public Atom(Func<AtomGetter, T> read, Action<AtomWriter, T> write)
    {
        Compute = read;
        Write = write;
    }

    /// <summary>write-only (action) atom (jotai's<c>atom(null, (get,set,arg)=>…)</c>equivalent). </summary>
    public Atom(Action<AtomWriter, T> write) => Write = write;

    internal T Initial { get; } = default!;

    internal Func<AtomGetter, T>? Compute { get; }

    /// <summary>Write type (bidirectional/write only atom). <see cref="AtomWriter"/>Update other atoms via.</summary>
    internal Action<AtomWriter, T>? Write { get; }

    /// <summary>
    /// Subscription lifecycle (jotai<c>atom.onMount</c>).
    /// The return value is cleanup (called when the last subscriber is removed).
    /// </summary>
    public Func<Action<T>, Action?>? OnMount { get; init; }

    internal bool IsDerived => Compute is not null;
}

/// <summary>Dependent leader passed to the derived atom calculation formula.<see cref="Get{U}"/>The atom read in is registered as a dependency.</summary>
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

/// <summary>1 atom value and subscribers (change notifications). <see cref="Provider{T}"/>Shared by scope.</summary>
public sealed class AtomCell
{
    private Action? _listeners;
    private int _count;

    internal object? Value { get; set; }

    internal bool HasValue { get; set; }

    /// <summary>onMount Has it been wired (to prevent duplicate wiring)?</summary>
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

    /// <summary>Number of subscribers (missing unsubscriptions = for leak detection/inspection/testing).</summary>
    internal int ListenerCount => _count;
}

/// <summary>
/// atom→cell store.<see cref="HamonRoot"/>global default, or<see cref="StoreProvider"/>Separate store.
/// Derived atoms are lazily calculated and cached, and automatically recalculated when dependent changes (notification only when changes = propagation downstream).
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

    /// <summary>Updated atom. </summary>
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

    /// <summary>Return atom to its initial value (equivalent to jotai's RESET). </summary>
    public void Reset<T>(Atom<T> atom)
    {
        AtomCell cell = Cell(atom);
        cell.Value = null;
        cell.HasValue = false; // 派生は次回 Read で再計算、プリミティブは Initial に戻る
        cell.Notify();
    }

    /// <summary>Initial seed (<see cref="StoreProvider"/>initialValues ​​). </summary>
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

/// <summary><see cref="Provider{T}"/>The scope of overwriting the atom value that extends to the subtree (1 node = 1 atom). </summary>
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
/// for subtrees<see cref="Atom"/>**Override** the value of (Flutter<c>InheritedWidget</c>/Provider-like "value plug-in").
/// of descendants<c>Watch/UseAtom</c>sees cells in this scope (overridden by nesting).
/// *jotai's "store boundary (independent separation of all atoms)" is<see cref="StoreProvider"/>For those of you.
/// </summary>
public sealed class Provider<T> : Widget
{
    public Atom<T> Atom { get; init; } = null!;

    public T Value { get; init; } = default!;

    public Widget? Child { get; init; }

    public override Element CreateElement() => new ProviderElement<T>(this);
}

/// <summary>
/// into a subtree<b>independent atom store</b>to stretch (jotai)<c>Provider</c>equivalent store boundaries). <c>Watch/UseAtom</c>teeth
/// Using this separate store, all atoms have values ​​that are independent of the global and external values ​​(for state isolation and testing on a screen/modal basis).
/// </summary>
public sealed class StoreProvider : Widget
{
    public Widget? Child { get; init; }

    /// <summary>Isolated store initial seed (jotai's<c>initialValues</c>equivalent). <c>s =&gt; s.Seed(atom, 5)</c>. </summary>
    public Action<AtomStore>? InitialValues { get; init; }

    public override Element CreateElement() => new StoreProviderElement(this);
}

/// <summary>
/// Generating and caching atoms with parameters (jotai<c>atomFamily</c>equivalent). <typeparamref name="TKey"/>for
/// same<see cref="Atom{T}"/>(e.g. state by entity id).
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

/// <summary><see cref="StoreProvider"/>holding entity. </summary>
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

/// <summary><see cref="Provider{T}"/>holding entity. </summary>
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
