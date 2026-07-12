using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Tooltip with explanation on hover (Flutter<c>Tooltip</c>). <see cref="WaitDuration"/>) When you hover your mouse over
/// Display a message as a small card under the child and disappear when you leave it (put it on the overlay layer).
/// </summary>
public sealed class Tooltip : Widget
{
    public Widget? Child { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>Hover time to display (in seconds).</summary>
    public float WaitDuration { get; init; } = 0.5f;

    public Color? Background { get; init; }

    public Color? TextColor { get; init; }

    public float FontSize { get; init; } = 13f;

    /// <summary>Vertical offset from child (px).</summary>
    public float VerticalOffset { get; init; } = 6f;

    public override Element CreateElement() => new TooltipElement(this);
}

/// <summary><see cref="Tooltip"/>holding entity. </summary>
internal sealed class TooltipElement : Element, IHoverTarget, ITicker
{
    private readonly LayoutNode _node = new(new Style { Kind = LayoutKind.Box }, null);
    private Element? _child;
    private Element[] _childArray = Array.Empty<Element>();
    private bool _hovered;
    private bool _ticking;
    private float _held;
    private OverlayEntry? _entry;

    public TooltipElement(Tooltip widget)
        : base(widget)
    {
    }

    private Tooltip W => (Tooltip)Widget;

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _childArray;

    bool IHoverTarget.HoverOpaque => false; // ツールチップは hover を背後へ通す（純粋ラッパ）

    MouseCursor IHoverTarget.HoverCursor => MouseCursor.Basic;

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
        Hide();
        StopTimer();
        (Context.Owner as HamonRoot)?.NotifyHoverTargetUnmounted(this);
        _child?.Unmount();
        _child = null;
        _childArray = Array.Empty<Element>();
        _node.Clear();
        base.Unmount();
    }

    public override void Paint(in PaintContext context) => _child?.Paint(context);

    void IHoverTarget.HoverEnter(Vec2 position)
    {
        _hovered = true;
        _held = 0f;
        StartTimer();
    }

    void IHoverTarget.HoverMove(Vec2 position)
    {
    }

    void IHoverTarget.HoverExit(Vec2 position)
    {
        _hovered = false;
        StopTimer();
        Hide();
    }

    bool ITicker.Tick(float dt)
    {
        if (!_hovered)
        {
            _ticking = false;
            return false;
        }

        _held += dt;
        if (_held >= W.WaitDuration)
        {
            Show();
            _ticking = false;
            return false; // 表示後は計時不要（離脱で消す）
        }

        return true;
    }

    private void Show()
    {
        if (_entry is not null || Context.Owner is not HamonRoot root)
        {
            return;
        }

        Rect b = _node.Bounds;
        float left = b.X;
        float top = b.Bottom + W.VerticalOffset;
        Tooltip w = W;
        _entry = root.PushOverlay(() => new Positioned
        {
            Left = Dimension.Px(left),
            Top = Dimension.Px(top),
            Child = new Align
            {
                Alignment = Alignment.TopLeft,
                Child = new Container
                {
                    Color = w.Background ?? new Color(20, 22, 28, 235),
                    Radius = 6f,
                    Padding = EdgeInsets.Symmetric(8f, 5f),
                    Child = new Text(w.Message) { FontSize = w.FontSize, Color = w.TextColor ?? new Color(231, 235, 243) },
                },
            },
        });
    }

    private void Hide()
    {
        if (_entry is OverlayEntry entry && Context.Owner is HamonRoot root)
        {
            root.RemoveOverlay(entry);
        }

        _entry = null;
    }

    private void StartTimer()
    {
        if (!_ticking)
        {
            _ticking = true;
            Context.Owner?.RegisterTicker(this);
        }
    }

    private void StopTimer()
    {
        if (_ticking)
        {
            _ticking = false;
            Context.Owner?.UnregisterTicker(this);
        }
    }

    private void BuildChild()
    {
        Widget built = W.Child ?? new SizedBox();
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
