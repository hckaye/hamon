using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Dropdown items (values ​​and labels).</summary>
public sealed class DropdownItem<T>
{
    public DropdownItem(T value, Widget label)
    {
        Value = value;
        Label = label;
    }

    public T Value { get; }

    public Widget Label { get; }
}

/// <summary>
/// Single selection dropdown (Flutter<c>DropdownButton&lt;T&gt;</c>).
/// Open the menu (overlay) below. <see cref="OnChanged"/>+Close.
/// </summary>
public sealed class Dropdown<T> : Widget
{
    public T Value { get; init; } = default!;

    public IReadOnlyList<DropdownItem<T>> Items { get; init; } = Array.Empty<DropdownItem<T>>();

    public Action<T>? OnChanged { get; init; }

    /// <summary>Display when no selection is made (if there is no value in the item).</summary>
    public Widget? Placeholder { get; init; }

    public Color? Background { get; init; }

    public Color? MenuBackground { get; init; }

    public float Radius { get; init; } = 8f;

    public override Element CreateElement() => new DropdownElement<T>(this);
}

/// <summary><see cref="Dropdown{T}"/>holding entity. </summary>
internal sealed class DropdownElement<T> : Element
{
    private readonly LayoutNode _node = new(new Style { Kind = LayoutKind.Box }, null);
    private Element? _child;
    private Element[] _childArray = Array.Empty<Element>();
    private OverlayEntry? _entry;

    public DropdownElement(Dropdown<T> widget)
        : base(widget)
    {
    }

    private Dropdown<T> W => (Dropdown<T>)Widget;

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _childArray;

    /// <summary>Is the menu open (for inspection/testing)?</summary>
    internal bool IsOpen => _entry is not null;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        BuildChild();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        BuildChild();
    }

    public override void Unmount()
    {
        Close();
        _child?.Unmount();
        _child = null;
        _childArray = Array.Empty<Element>();
        _node.Clear();
        base.Unmount();
    }

    public override void Paint(in PaintContext context) => _child?.Paint(context);

    private Widget BuildTrigger()
    {
        HamonTheme theme = Context.Theme;
        Widget label = SelectedLabel() ?? W.Placeholder ?? new Text(string.Empty);
        return new Button
        {
            Background = W.Background ?? theme.SurfaceVariant,
            Radius = W.Radius,
            Padding = EdgeInsets.Symmetric(12f, 8f),
            OnPressed = Open,
            Child = new Row
            {
                MainAxisSize = MainAxisSize.Min,
                Spacing = 8f,
                Children = new Widget[] { label, new Text("▾") { FontSize = 10f, Color = theme.OnSurfaceVariant } },
            },
        };
    }

    private Widget? SelectedLabel()
    {
        for (int i = 0; i < W.Items.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(W.Items[i].Value, W.Value))
            {
                return W.Items[i].Label;
            }
        }

        return null;
    }

    private void Open()
    {
        if (_entry is not null || Context.Owner is not HamonRoot root)
        {
            return;
        }

        Rect b = _node.Bounds;
        OverlayEntry entry = null!;
        entry = root.PushOverlay(() => BuildMenu(b, entry));
        _entry = entry;
    }

    private void Close()
    {
        if (_entry is OverlayEntry entry && Context.Owner is HamonRoot root)
        {
            root.RemoveOverlay(entry);
        }

        _entry = null;
    }

    private Widget BuildMenu(Rect anchor, OverlayEntry entry)
    {
        HamonTheme theme = Context.Theme;
        var items = new Widget[W.Items.Count];
        for (int i = 0; i < W.Items.Count; i++)
        {
            DropdownItem<T> item = W.Items[i];
            bool selected = EqualityComparer<T>.Default.Equals(item.Value, W.Value);
            items[i] = new Button
            {
                Background = selected ? theme.SurfaceVariant : new Color(0, 0, 0, 0),
                Radius = 4f,
                Padding = EdgeInsets.Symmetric(12f, 8f),
                OnPressed = () =>
                {
                    W.OnChanged?.Invoke(item.Value);
                    if (Context.Owner is HamonRoot r)
                    {
                        r.RemoveOverlay(entry);
                    }

                    _entry = null;
                },
                Child = item.Label,
            };
        }

        return new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                // scrim：外側タップで閉じる。
                new GestureDetector
                {
                    OnTap = () =>
                    {
                        if (Context.Owner is HamonRoot r)
                        {
                            r.RemoveOverlay(entry);
                        }

                        _entry = null;
                    },
                    Child = new SizedBox { Width = Dimension.Percent(100f), Height = Dimension.Percent(100f) },
                },
                new Positioned
                {
                    Left = Dimension.Px(anchor.X),
                    Top = Dimension.Px(anchor.Bottom + 2f),
                    Width = Dimension.Px(anchor.Width),
                    Child = new Align
                    {
                        Alignment = Alignment.TopLeft,
                        Child = new Container
                        {
                            Color = W.MenuBackground ?? theme.Surface,
                            Radius = W.Radius,
                            Padding = EdgeInsets.All(4f),
                            Child = new Column { CrossAxisAlignment = CrossAxisAlignment.Stretch, Spacing = 2f, Children = items },
                        },
                    },
                },
            },
        };
    }

    private void BuildChild()
    {
        Widget built = BuildTrigger();
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
