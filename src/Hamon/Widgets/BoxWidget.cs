using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Common configuration for widgets that produce a single layout node; <see cref="RenderElement"/> reads this.
/// Implemented by each Flutter-style widget (<see cref="Row"/>/<see cref="Column"/>/<see cref="Container"/>, etc.).
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

/// <summary>A low-level layout box that directly exposes <see cref="Style"/>.</summary>
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
/// The Element that hosts a Widget implementing <see cref="IRenderConfig"/>. Owns its own <see cref="LayoutNode"/>
/// and updates children via keyed reconciliation.
/// </summary>
public class RenderElement : Element
{
    private readonly LayoutNode _node;
    private readonly List<Element> _children = new();

    // Reusable scratch buffers for UpdateChildren's keyed reconciliation. These are cleared (not
    // reallocated) on each call so a steady-state rebuild allocates no new collections; they hold no
    // state between calls. UpdateChildren is never called reentrantly on the same instance, so sharing
    // these per-instance buffers is safe.
    private readonly List<Element> _newChildrenScratch = new();
    private readonly HashSet<Element> _matchedScratch = new();
    private readonly Dictionary<object, Element> _keyedScratch = new();

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
    /// Updates children via keyed reconciliation, unmounting any old elements that were not matched.
    /// Protected so that derived classes (such as <see cref="ButtonElement"/>, for state-driven builders) can call
    /// it when their state changes.
    /// </summary>
    protected void UpdateChildren(IReadOnlyList<Widget> newWidgets)
    {
        var oldChildren = _children;
        var newChildren = _newChildrenScratch;
        var matched = _matchedScratch;
        var keyed = _keyedScratch;
        newChildren.Clear();
        matched.Clear();
        keyed.Clear();

        for (int i = 0; i < oldChildren.Count; i++)
        {
            object? key = oldChildren[i].Widget.Key;
            if (key is not null)
            {
                keyed[key] = oldChildren[i];
            }
        }

        int positional = 0;
        for (int i = 0; i < newWidgets.Count; i++)
        {
            Widget nw = newWidgets[i];
            Element? reuse = null;

            if (nw.Key is not null
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
