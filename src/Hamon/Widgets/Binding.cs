using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A value holder with change notification (equivalent to Flutter's <c>ValueNotifier</c>). Whereas
/// <see cref="State{T}"/> marks the whole tree dirty, this **notifies only its subscribers** (<see cref="Bind{T}"/>),
/// avoiding a full rebuild on frequent updates. If you mutate the contents of a reference type in place, call
/// <see cref="Notify"/> manually to notify listeners.
/// </summary>
public sealed class ValueNotifier<T>
{
    private T _value;
    private Action? _listeners;

    public ValueNotifier(T value) => _value = value;

    public T Value
    {
        get => _value;
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                _value = value;
                _listeners?.Invoke();
            }
        }
    }

    public void AddListener(Action listener) => _listeners += listener;

    public void RemoveListener(Action listener) => _listeners -= listener;

    /// <summary>Force notification of changes (for example, when changing a reference type in-place).</summary>
    public void Notify() => _listeners?.Invoke();
}

/// <summary>
/// Rebuilds only its own subtree in response to a value change (equivalent to Flutter's <c>ValueListenableBuilder</c>).
/// Reflects frequently changing HUD values (HP, timer, etc.) without reconciling the entire tree.
/// Even cheaper: values such as opacity/transform/color on widgets like <see cref="Opacity"/> can be read via a
/// <c>*Getter</c> at draw time, requiring no rebuild at all. This is also the core mechanism behind future external
/// layout bindings (<c>Find().Bind(...)</c>).
/// </summary>
public sealed class Bind<T> : Widget
{
    public ValueNotifier<T> Listenable { get; init; } = null!;

    public Func<T, Widget> Builder { get; init; } = null!;

    public override Element CreateElement() => new BindElement<T>(this);
}

/// <summary>The Element that hosts a <see cref="Bind{T}"/>.</summary>
internal sealed class BindElement<T> : Element
{
    private readonly LayoutNode _node;
    private Element? _child;
    private Element[] _childArray = Array.Empty<Element>();
    private Action? _handler;

    public BindElement(Bind<T> widget)
        : base(widget)
    {
        // 単一子パススルー（親が参照するこの node は再構築でも安定＝部分再構築を安全にする）。
        _node = new LayoutNode(new Style { Kind = LayoutKind.Stack, StackExpandChildren = true });
    }

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _childArray;

    private Bind<T> Widget_ => (Bind<T>)Widget;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        _handler = () => Context.Owner?.MarkElementDirty(this);
        Widget_.Listenable.AddListener(_handler);
        BuildChild();
    }

    public override void Update(Widget newWidget)
    {
        var old = (Bind<T>)Widget;
        base.Update(newWidget);
        var updated = (Bind<T>)newWidget;
        if (!ReferenceEquals(old.Listenable, updated.Listenable) && _handler is not null)
        {
            old.Listenable.RemoveListener(_handler);
            updated.Listenable.AddListener(_handler);
        }

        BuildChild();
    }

    public override void Unmount()
    {
        if (_handler is not null)
        {
            Widget_.Listenable.RemoveListener(_handler);
        }

        _child?.Unmount();
        _child = null;
        _childArray = Array.Empty<Element>();
        _node.Clear();
        base.Unmount();
    }

    public override void Paint(in PaintContext context) => _child?.Paint(context);

    internal override void RebuildInPlace() => BuildChild();

    private void BuildChild()
    {
        Widget built = Widget_.Builder(Widget_.Listenable.Value);
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
}
