using Hamon.Layout;
using System.Threading.Tasks;

namespace Hamon.Widgets;

/// <summary>The three states of an asynchronous value (jotai's async atom, without Suspense).</summary>
public enum AsyncState : byte
{
    Loading,
    Data,
    Error,
}

/// <summary>The result of an asynchronous load (loading/data/error), returned by <c>Hooks.UseAsync</c>.</summary>
public readonly struct AsyncValue<T>
{
    private AsyncValue(AsyncState state, T? data, Exception? error)
    {
        State = state;
        Data = data;
        Error = error;
    }

    public AsyncState State { get; }

    public T? Data { get; }

    public Exception? Error { get; }

    public bool IsLoading => State == AsyncState.Loading;

    public bool HasData => State == AsyncState.Data;

    public bool HasError => State == AsyncState.Error;

    public static AsyncValue<T> Loading => new(AsyncState.Loading, default, null);

    public static AsyncValue<T> Ok(T data) => new(AsyncState.Data, data, null);

    public static AsyncValue<T> Fail(Exception error) => new(AsyncState.Error, default, error);
}

/// <summary>
/// A hook-enabled composition widget (like React or flutter_hooks). Inside <see cref="Build(BuildContext, Hooks)"/>,
/// call <c>hooks.UseState</c>/<c>UseFocusNode</c>/<c>UseAnimation</c>/<c>Watch</c>, etc.
/// When the state changes, <b>only this subtree is automatically rebuilt</b> — no manual Invalidate is needed, and a
/// full tree reconcile is avoided.
/// Call hooks in the same order every time (do not add or remove hook calls via conditional branches — the same rule
/// as React).
/// </summary>
public abstract class HookWidget : Widget
{
    public abstract Widget Build(BuildContext context, Hooks hooks);

    public override Element CreateElement() => new HookElement(this);
}

/// <summary>A state cell returned by <c>UseState</c>. Changing <see cref="Value"/> rebuilds only the subtree of the owning element.</summary>
public sealed class HookState<T>
{
    private readonly HookElement _element;
    private T _value;

    internal HookState(HookElement element, T value)
    {
        _element = element;
        _value = value;
    }

    public T Value
    {
        get => _value;
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                _value = value;
                _element.MarkSelfDirty();
            }
        }
    }
}

/// <summary>Supplies hooks during Build. <see cref="HookElement"/> keeps a single instance and reuses it.</summary>
public sealed class Hooks
{
    private readonly HookElement _element;

    internal Hooks(HookElement element) => _element = element;

    /// <summary>A state value that persists on the element.</summary>
    public HookState<T> UseState<T>(T initial) => _element.Cell(() => new HookState<T>(_element, initial));

    /// <summary>Creates a value once and persists it on the element (equivalent to React's useMemo with no dependencies, or useRef).</summary>
    public T UseMemo<T>(Func<T> create) => _element.Cell(create);

    /// <summary>Creates and persists a <see cref="FocusNode"/> on the element (no need to hold it as an external member).</summary>
    public FocusNode UseFocusNode() => _element.Cell(() => new FocusNode());

    /// <summary>Creates and persists an <see cref="AnimationController"/> on the element (automatically stopped on Unmount).</summary>
    public AnimationController UseAnimation(float durationSeconds, Curve? curve = null) =>
        _element.Cell(() => _element.Owner.CreateAnimation(durationSeconds, curve));

    /// <summary>Subscribes to an atom and returns its current value (only this subtree is rebuilt when it changes).</summary>
    public T Watch<T>(Atom<T> atom) => _element.Watch(atom);

    /// <summary>Returns the atom's current value together with a setter function.</summary>
    public (T Value, Action<T> Set) UseAtom<T>(Atom<T> atom) =>
        (_element.Watch(atom), value => _element.SetAtom(atom, value));

    /// <summary>Returns a resetter that restores the atom to its initial value (equivalent to jotai's <c>useResetAtom</c>).</summary>
    public Action UseReset<T>(Atom<T> atom) => () => _element.ResetAtom(atom);

    /// <summary>
    /// Asynchronous loading (equivalent to jotai's async atom). Runs <paramref name="loader"/> whenever
    /// <paramref name="key"/> changes (including the first time), and returns the outcome as an
    /// <see cref="AsyncValue{T}"/> (Loading → Data/Error). Thread-safe, since the result is delivered back to the UI
    /// thread via <see cref="HamonRoot.Post"/>.
    /// </summary>
    public AsyncValue<T> UseAsync<T>(Func<Task<T>> loader, object? key = null)
    {
        HookState<AsyncValue<T>> state = _element.Cell(() => new HookState<AsyncValue<T>>(_element, AsyncValue<T>.Loading));
        UseEffect(
            () =>
            {
                bool cancelled = false;
                state.Value = AsyncValue<T>.Loading;
                IHamonHost owner = _element.Owner;
                _ = loader().ContinueWith(
                    task => owner.Post(() =>
                    {
                        if (cancelled)
                        {
                            return;
                        }

                        state.Value = task.IsFaulted
                            ? AsyncValue<T>.Fail((Exception?)task.Exception?.InnerException ?? task.Exception!)
                            : AsyncValue<T>.Ok(task.Result);
                    }),
                    TaskContinuationOptions.ExecuteSynchronously);
                return () => cancelled = true;
            },
            key);

        return state.Value;
    }

    /// <summary>
    /// Runs <paramref name="effect"/> whenever <paramref name="key"/> changes (including the first time). The
    /// returned <see cref="Action"/> is a cleanup callback (invoked before the next run and on Unmount, e.g. to
    /// unsubscribe or dispose a timer).
    /// </summary>
    public void UseEffect(Func<Action?> effect, object? key = null)
    {
        var cell = _element.Cell(() => new EffectCell());
        if (!cell.Ran || !Equals(cell.Key, key))
        {
            cell.Cleanup?.Invoke(); // 再実行前に前回のクリーンアップ
            cell.Ran = true;
            cell.Key = key;
            cell.Cleanup = effect();
        }
    }

    /// <summary>Cleanup-free version.</summary>
    public void UseEffect(Action effect, object? key = null) => UseEffect(
        () =>
        {
            effect();
            return null;
        },
        key);

    internal sealed class EffectCell
    {
        public bool Ran { get; set; }

        public object? Key { get; set; }

        public Action? Cleanup { get; set; }
    }
}

/// <summary>
/// The holding entity for <see cref="HookWidget"/>.
/// On a hook state/atom change, <see cref="RebuildInPlace"/> rebuilds only this subtree.
/// </summary>
internal sealed class HookElement : Element
{
    private readonly LayoutNode _node;
    private readonly List<object> _cells = new();
    private readonly HashSet<AtomCell> _subscribed = new(); // build 中の購読を atom 単位で dedupe
    private readonly Hooks _hooks;
    private Element? _child;
    private Element[] _childArray = Array.Empty<Element>();
    private Action? _onChanged;
    private int _cursor;

    public HookElement(HookWidget widget)
        : base(widget)
    {
        _node = new LayoutNode(new Style { Kind = LayoutKind.Stack, StackExpandChildren = true });
        _hooks = new Hooks(this);
    }

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _childArray;

    internal IHamonHost Owner => Context.Owner
        ?? throw new InvalidOperationException("HookWidget requires a HamonRoot (used to manage hook state/animation/atoms).");

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        _onChanged = MarkSelfDirty;
        Rebuild();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        Rebuild();
    }

    public override void Unmount()
    {
        UnsubscribeAll();
        for (int i = 0; i < _cells.Count; i++)
        {
            switch (_cells[i])
            {
                case AnimationController anim:
                    anim.Stop();
                    break;
                case Hooks.EffectCell effect:
                    effect.Cleanup?.Invoke(); // Unmount 時にエフェクトのクリーンアップ
                    break;
            }
        }

        _child?.Unmount();
        _child = null;
        _childArray = Array.Empty<Element>();
        _node.Clear();
        base.Unmount();
    }

    public override void Paint(in PaintContext context) => _child?.Paint(context);

    internal override void RebuildInPlace() => Rebuild();

    internal void MarkSelfDirty() => Context.Owner?.MarkElementDirty(this);

    /// <summary>A position-dependent cell: created and stored the first time, then reused in the same call order on every rebuild.</summary>
    internal T Cell<T>(Func<T> create)
    {
        if (_cursor == _cells.Count)
        {
            _cells.Add(create()!);
        }

        var cell = (T)_cells[_cursor];
        _cursor++;
        return cell;
    }

    internal T Watch<T>(Atom<T> atom)
    {
        AtomCell? overrideCell = HamonRoot.FindOverride(Context.AtomScope, atom);
        if (overrideCell is not null)
        {
            Subscribe(overrideCell);
            return overrideCell.HasValue ? (T)overrideCell.Value! : atom.Initial;
        }

        AtomStore store = Context.Store ?? Owner.GlobalStore;
        Subscribe(store.Cell(atom)); // 値セルを購読（派生なら計算後に通知が来る）
        return (T)store.Read(atom)!;
    }

    internal void SetAtom<T>(Atom<T> atom, T value)
    {
        AtomCell? overrideCell = HamonRoot.FindOverride(Context.AtomScope, atom);
        if (overrideCell is not null)
        {
            overrideCell.Value = value;
            overrideCell.HasValue = true;
            overrideCell.Notify();
            return;
        }

        (Context.Store ?? Owner.GlobalStore).Set(atom, value);
    }

    internal void ResetAtom<T>(Atom<T> atom)
    {
        AtomCell? overrideCell = HamonRoot.FindOverride(Context.AtomScope, atom);
        if (overrideCell is not null)
        {
            overrideCell.Value = null;
            overrideCell.HasValue = false;
            overrideCell.Notify();
            return;
        }

        (Context.Store ?? Owner.GlobalStore).Reset(atom);
    }

    private void Subscribe(AtomCell cell)
    {
        if (_subscribed.Add(cell)) // 同一 build で同じ atom を複数回 Watch しても購読は1回
        {
            cell.Subscribe(_onChanged!);
        }
    }

    private void Rebuild()
    {
        _cursor = 0;
        UnsubscribeAll(); // 今回の Build で Watch された atom だけ購読し直す
        Widget built = ((HookWidget)Widget).Build(Context, _hooks);

        if (_child is not null && Widget.CanUpdate(_child.Widget, built))
        {
            _child.Update(built);
        }
        else
        {
            _child?.Unmount();
            _child = built.CreateElement();
            _child.Mount(this, Context);
            _childArray = new[] { _child };
        }

        _node.Clear();
        _node.Add(_child.LayoutNode);
    }

    private void UnsubscribeAll()
    {
        if (_onChanged is null)
        {
            return;
        }

        foreach (AtomCell cell in _subscribed)
        {
            cell.Unsubscribe(_onChanged);
        }

        _subscribed.Clear();
    }
}
