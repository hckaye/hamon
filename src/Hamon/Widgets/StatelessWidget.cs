namespace Hamon.Widgets;

/// <summary>
/// A composition widget that assembles and returns other widgets (equivalent to Flutter's <c>StatelessWidget</c>).
/// <see cref="Build"/> is called when state changes, and the result is reconciled as this widget's child.
/// </summary>
public abstract class StatelessWidget : Widget
{
    public abstract Widget Build(BuildContext context);

    public override Element CreateElement() => new StatelessElement(this);
}

/// <summary>The element that backs <see cref="StatelessWidget"/>.</summary>
public sealed class StatelessElement : Element
{
    private Element? _child;
    private Element[] _childArray = Array.Empty<Element>();

    public StatelessElement(StatelessWidget widget)
        : base(widget)
    {
    }

    public override Layout.LayoutNode LayoutNode => _child!.LayoutNode;

    public override IReadOnlyList<Element> Children => _childArray;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        Rebuild();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        Rebuild();
    }

    public override void Unmount()
    {
        _child?.Unmount();
        _child = null;
        _childArray = Array.Empty<Element>();
        base.Unmount();
    }

    public override void Paint(in PaintContext context) => _child?.Paint(context);

    private void Rebuild()
    {
        Widget built = ((StatelessWidget)Widget).Build(Context);
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
    }
}
