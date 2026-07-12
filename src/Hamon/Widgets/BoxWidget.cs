using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Common settings for widgets that generate one layout node.<see cref="RenderElement"/>reads this.
/// Each Flutter-style widget (<see cref="Row"/>/<see cref="Column"/>/<see cref="Container"/>etc.) will be implemented.
/// </summary>
internal interface IRenderConfig
{
    Style Style { get; }

    IReadOnlyList<Widget>? Children { get; }

    Color? Background { get; }

    Func<BoxConstraints, Size>? Measure { get; }

    /// <summary>Background corner radius (px, default 0 = rectangle). </summary>
    float Radius => 0f;
}

/// <summary>
/// Low-level layout box (<see cref="Style"/>directly).
/// </summary>
public sealed class BoxWidget : Widget, IRenderConfig
{
    public Style Style { get; init; }

    public IReadOnlyList<Widget>? Children { get; init; }

    public Func<BoxConstraints, Size>? Measure { get; init; }

    public Color? Background { get; init; }

    /// <summary>Background corner radius (px, default 0 = rectangle).</summary>
    public float Radius { get; init; }

    float IRenderConfig.Radius => Radius;

    public override Element CreateElement() => new RenderElement(this);
}

/// <summary>
/// <see cref="IRenderConfig"/>The holding entity of the Widget.<see cref="LayoutNode"/>own,
/// Update child with keyed reconcile.
/// </summary>
public class RenderElement : Element
{
    private readonly LayoutNode _node;
    private readonly List<Element> _children = new();

    public RenderElement(Widget widget)
        : base(widget)
    {
        var config = (IRenderConfig)widget;
        _node = new LayoutNode(config.Style, config.Measure);
    }

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _children;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        Apply((IRenderConfig)Widget);
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        Apply((IRenderConfig)newWidget);
    }

    public override void Unmount()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Unmount();
        }

        _children.Clear();
        _node.Clear();
        base.Unmount();
    }

    public override void Paint(in PaintContext context)
    {
        var config = (IRenderConfig)Widget;
        if (config.Background is Color background)
        {
            context.FillRoundedRect(_node.Bounds, background, config.Radius);
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Paint(context);
        }
    }

    private void Apply(IRenderConfig config)
    {
        _node.Style = config.Style;
        _node.Measure = config.Measure;
        UpdateChildren(config.Children ?? Array.Empty<Widget>());
    }

    /// <summary>
    /// Update child with keyed reconcile.
    /// Unmount the remaining old entity.
    /// Derivative (<see cref="ButtonElement"/>protected so that state builders, etc.) can be called when the state changes.
    /// </summary>
    protected void UpdateChildren(IReadOnlyList<Widget> newWidgets)
    {
        var oldChildren = _children;
        var newChildren = new List<Element>(newWidgets.Count);
        var matched = new HashSet<Element>();

        Dictionary<object, Element>? keyed = null;
        for (int i = 0; i < oldChildren.Count; i++)
        {
            object? key = oldChildren[i].Widget.Key;
            if (key is not null)
            {
                keyed ??= new Dictionary<object, Element>();
                keyed[key] = oldChildren[i];
            }
        }

        int positional = 0;
        for (int i = 0; i < newWidgets.Count; i++)
        {
            Widget nw = newWidgets[i];
            Element? reuse = null;

            if (nw.Key is not null && keyed is not null
                && keyed.TryGetValue(nw.Key, out Element? keyedMatch)
                && Widget.CanUpdate(keyedMatch.Widget, nw)
                && matched.Add(keyedMatch))
            {
                reuse = keyedMatch;
            }
            else
            {
                while (positional < oldChildren.Count)
                {
                    Element candidate = oldChildren[positional++];
                    if (candidate.Widget.Key is null && Widget.CanUpdate(candidate.Widget, nw) && matched.Add(candidate))
                    {
                        reuse = candidate;
                        break;
                    }
                }
            }

            if (reuse is not null)
            {
                reuse.Update(nw);
                newChildren.Add(reuse);
            }
            else
            {
                Element created = nw.CreateElement();
                created.Mount(this, Context);
                newChildren.Add(created);
            }
        }

        for (int i = 0; i < oldChildren.Count; i++)
        {
            if (!matched.Contains(oldChildren[i]))
            {
                oldChildren[i].Unmount();
            }
        }

        _children.Clear();
        _children.AddRange(newChildren);

        _node.Clear();
        for (int i = 0; i < _children.Count; i++)
        {
            _node.Add(_children[i].LayoutNode);
        }
    }
}
