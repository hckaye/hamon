using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Virtualized grid (equivalent to Flutter's <c>GridView.builder</c> plus
/// <c>SliverGridDelegateWithFixedCrossAxisCount</c>). Divides the cross axis evenly into
/// <see cref="CrossAxisCount"/> columns and materializes only <b>visible rows</b> via <see cref="Builder"/>
/// (lightweight even with huge item counts = row-by-row virtualization). Specify <see cref="MainAxisExtent"/>
/// directly, or if unspecified, derive the main-axis cell length from the cross-axis cell length and
/// <see cref="ChildAspectRatio"/> (= cross/main). Use <see cref="OnEndReached"/> for infinite scroll.
/// Frequently used for inventory/slot grids, etc.
/// </summary>
public sealed class GridView : Widget
{
    public int ItemCount { get; init; }

    /// <summary>Maps an index to a cell widget.</summary>
    public Func<int, Widget> Builder { get; init; } = static _ => new SizedBox();

    /// <summary>Number of columns in the cross axis (≥1).</summary>
    public int CrossAxisCount { get; init; } = 1;

    /// <summary>Scroll direction (default <see cref="Axis.Vertical"/>).</summary>
    public Axis Axis { get; init; } = Axis.Vertical;

    /// <summary>Main-axis cell length (px); takes priority over <see cref="ChildAspectRatio"/>.</summary>
    public float? MainAxisExtent { get; init; }

    /// <summary>Cell aspect ratio (cross-axis length / main-axis length); used to derive the main-axis length when <see cref="MainAxisExtent"/> is not specified (default 1 = square).</summary>
    public float ChildAspectRatio { get; init; } = 1f;

    /// <summary>Spacing between columns (cross axis) in px.</summary>
    public float CrossAxisSpacing { get; init; }

    /// <summary>Spacing between rows (main axis) in px.</summary>
    public float MainAxisSpacing { get; init; }

    public ScrollController? Controller { get; init; }

    /// <summary>Overscroll at the edge (rubber band + return). </summary>
    public bool Bounce { get; init; } = true;

    /// <summary>
    /// Whether to enable manual scrolling (drag/wheel). Set to false to <b>disable user-interaction
    /// scrolling</b> (programmatic control via <see cref="Controller"/>, scroll-to-focus, etc. is <b>still
    /// possible</b>). Roughly equivalent to Flutter's <c>NeverScrollableScrollPhysics</c>.
    /// </summary>
    public bool ManualScroll { get; init; } = true;

    /// <summary>Scroll movement constants (sensitivity/following/rubber band/inertia); falls back to <see cref="HamonTheme.ScrollPhysics"/> when not set.</summary>
    public ScrollPhysics? Physics { get; init; }

    public Dimension Width { get; init; }

    public Dimension Height { get; init; }

    /// <summary>Fires only once when visibility reaches near the end (infinite scroll = increase itemCount).</summary>
    public Action? OnEndReached { get; init; }

    public override Element CreateElement() => new GridViewElement(this);
}

/// <summary>
/// The element backing <see cref="GridView"/>. Maintains the scroll offset, updates it via drag or the
/// controller, and clips drawing to a rectangle.
/// </summary>
internal sealed class GridViewElement : Element, IVirtualLayout, IScrollable
{
    private readonly LayoutNode _node;
    private readonly Dictionary<int, Element> _active = new();
    private readonly List<Element> _visible = new();
    private readonly DragScroller _drag;

    private int _lastEndCount = -1;

    public GridViewElement(GridView widget)
        : base(widget)
    {
        _node = new LayoutNode { Virtual = this };
        _drag = new DragScroller(this);
    }

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _visible;

    public float ScrollOffset { get; private set; }

    Axis IScrollable.ScrollAxis => ((GridView)Widget).Axis;

    ScrollPhysics IScrollable.Physics => ((GridView)Widget).Physics ?? Context.Theme.ScrollPhysics;

    /// <summary>Currently materialized index set (for inspection/testing).</summary>
    internal IReadOnlyCollection<int> ActiveIndices => _active.Keys;

    /// <summary>The entity of the specified index (or null if it does not exist. For inspection/testing).</summary>
    internal Element? ActiveElement(int index) => _active.TryGetValue(index, out Element? e) ? e : null;

    void IScrollable.SetScroll(float offset) => SetScroll(offset);

    void IScrollable.RevealRect(Rect target) => ScrollReveal.Reveal(this, _node.Bounds, target);

    void IScrollable.ScrollByAnimated(float offsetDelta) => _drag.ScrollBy(offsetDelta);

    void IScrollable.AnimateScrollTo(float offset) => _drag.GlideTo(offset);

    float IScrollable.MaxScroll
    {
        get
        {
            var widget = (GridView)Widget;
            int crossCount = CrossCount(widget);
            int rowCount = widget.ItemCount == 0 ? 0 : (widget.ItemCount + crossCount - 1) / crossCount;
            float vpCross = Vertical ? _node.Size.Width : _node.Size.Height;
            float crossExtent = CellCrossExtent(widget, crossCount, vpCross);
            float mainExtent = CellMainExtent(widget, crossExtent);
            float content = ContentExtent(rowCount, mainExtent, widget.MainAxisSpacing);
            float vpMain = Vertical ? _node.Size.Height : _node.Size.Width;
            return Math.Max(0f, content - vpMain);
        }
    }

    bool IScrollable.BounceEnabled => ((GridView)Widget).Bounce;

    bool IScrollable.ManualScrollEnabled => ((GridView)Widget).ManualScroll;

    public override bool WantsPointer => true;

    private bool Vertical => ((GridView)Widget).Axis == Axis.Vertical;

    private static int CrossCount(GridView widget) => Math.Max(1, widget.CrossAxisCount);

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        _drag.Owner = Context.Owner;
        Attach((GridView)Widget);
    }

    public override void Update(Widget newWidget)
    {
        var old = (GridView)Widget;
        base.Update(newWidget);
        var updated = (GridView)newWidget;
        if (!ReferenceEquals(old.Controller, updated.Controller))
        {
            if (old.Controller is not null && ReferenceEquals(old.Controller.Target, this))
            {
                old.Controller.Target = null;
            }

            Attach(updated);
        }
        // 次のレイアウトで Builder/ItemCount を反映して可視セルを作り直す（Virtual.Measure で実施）。
    }

    public override void Unmount()
    {
        var widget = (GridView)Widget;
        if (widget.Controller is not null && ReferenceEquals(widget.Controller.Target, this))
        {
            widget.Controller.Target = null;
        }

        foreach (Element e in _active.Values)
        {
            e.Unmount();
        }

        _active.Clear();
        _visible.Clear();
        base.Unmount();
    }

    public override void HandlePointer(in PointerEvent pointer)
    {
        if (!((GridView)Widget).ManualScroll)
        {
            return; // 手動スクロール無効＝ドラッグを無視（プログラム制御は別経路で可能）
        }

        _drag.HandlePointer(pointer, Vertical ? pointer.Position.Y : pointer.Position.X);
    }

    public override void Paint(in PaintContext context)
    {
        object? previous = context.PushClip(_node.Bounds);
        float over = _drag.Overscroll;
        PaintContext inner = over == 0f
            ? context
            : context.WithTransform(Transform2D.Translation(Vertical ? new Vec2(0f, -over) : new Vec2(-over, 0f)));
        for (int i = 0; i < _visible.Count; i++)
        {
            _visible[i].Paint(inner);
        }

        context.PopClip(previous);
    }

    /// <summary>Virtualization measurement: Build and measure only cells in visible rows, set children and offset, and return viewport size.</summary>
    public Size Measure(LayoutNode node, BoxConstraints constraints)
    {
        var widget = (GridView)Widget;
        bool vertical = Vertical;
        int crossCount = CrossCount(widget);

        float? fixedMain = vertical ? widget.Height.Resolve(constraints.MaxHeight) : widget.Width.Resolve(constraints.MaxWidth);
        float? fixedCross = vertical ? widget.Width.Resolve(constraints.MaxWidth) : widget.Height.Resolve(constraints.MaxHeight);

        float maxMain = vertical ? constraints.MaxHeight : constraints.MaxWidth;
        float maxCross = vertical ? constraints.MaxWidth : constraints.MaxHeight;

        float vpMain = fixedMain ?? (float.IsFinite(maxMain) ? maxMain : 0f);
        float vpCross = fixedCross ?? (float.IsFinite(maxCross) ? maxCross : 0f);
        Size viewport = constraints.Constrain(vertical ? new Size(vpCross, vpMain) : new Size(vpMain, vpCross));
        vpMain = vertical ? viewport.Height : viewport.Width;
        vpCross = vertical ? viewport.Width : viewport.Height;

        // セル寸法（交差軸は列数で等分、主軸は MainAxisExtent か aspect から）。
        float crossExtent = CellCrossExtent(widget, crossCount, vpCross);
        float mainExtent = CellMainExtent(widget, crossExtent);
        float mainStride = mainExtent + widget.MainAxisSpacing;
        float crossStride = crossExtent + widget.CrossAxisSpacing;

        int count = widget.ItemCount;
        int rowCount = count == 0 ? 0 : (count + crossCount - 1) / crossCount;
        float content = ContentExtent(rowCount, mainExtent, widget.MainAxisSpacing);
        ScrollOffset = Math.Clamp(ScrollOffset, 0f, Math.Max(0f, content - vpMain));
        float scroll = ScrollOffset;

        FindVisibleRows(rowCount, mainStride, scroll, vpMain, out int firstRow, out int lastRow);
        int first = firstRow * crossCount;
        int last = count == 0 ? -1 : Math.Min(count - 1, ((lastRow + 1) * crossCount) - 1);

        RemoveOutOfRange(first, last);

        _visible.Clear();
        _node.Clear();

        BoxConstraints cellConstraints = MakeCellConstraints(vertical, crossExtent, mainExtent);
        for (int i = first; i <= last; i++)
        {
            Element element = Realize(i, widget);
            FlexLayoutEngine.MeasureNode(element.LayoutNode, cellConstraints);

            int row = i / crossCount;
            int col = i - (row * crossCount);
            float mainOffset = (row * mainStride) - scroll;
            float crossOffset = col * crossStride;
            element.LayoutNode.OffsetX = vertical ? crossOffset : mainOffset;
            element.LayoutNode.OffsetY = vertical ? mainOffset : crossOffset;

            _visible.Add(element);
            _node.Add(element.LayoutNode);
        }

        MaybeFireEndReached(widget, last, count);
        return viewport;
    }

    internal void SetScroll(float offset)
    {
        var widget = (GridView)Widget;
        int crossCount = CrossCount(widget);
        int rowCount = widget.ItemCount == 0 ? 0 : (widget.ItemCount + crossCount - 1) / crossCount;

        float vpCross = Vertical ? _node.Size.Width : _node.Size.Height;
        float crossExtent = CellCrossExtent(widget, crossCount, vpCross);
        float mainExtent = CellMainExtent(widget, crossExtent);
        float content = ContentExtent(rowCount, mainExtent, widget.MainAxisSpacing);
        float vpMain = Vertical ? _node.Size.Height : _node.Size.Width;

        float clamped = Math.Clamp(offset, 0f, Math.Max(0f, content - vpMain));
        if (clamped == ScrollOffset)
        {
            return;
        }

        ScrollOffset = clamped;
        Context.Owner?.MarkDirty();
    }

    // --- 仮想化ヘルパ ---

    private static float CellCrossExtent(GridView widget, int crossCount, float vpCross)
    {
        float available = vpCross - (widget.CrossAxisSpacing * (crossCount - 1));
        return Math.Max(0f, available / crossCount);
    }

    private static float CellMainExtent(GridView widget, float crossExtent)
    {
        if (widget.MainAxisExtent is float ext)
        {
            return ext;
        }

        float aspect = widget.ChildAspectRatio <= 0f ? 1f : widget.ChildAspectRatio;
        return crossExtent / aspect;
    }

    private static float ContentExtent(int rowCount, float mainExtent, float mainSpacing) =>
        rowCount == 0 ? 0f : (rowCount * mainExtent) + ((rowCount - 1) * mainSpacing);

    private static void FindVisibleRows(int rowCount, float mainStride, float scroll, float vpMain, out int firstRow, out int lastRow)
    {
        if (rowCount == 0 || mainStride <= 0f)
        {
            firstRow = 0;
            lastRow = -1;
            return;
        }

        firstRow = Math.Max(0, (int)MathF.Floor(scroll / mainStride));
        lastRow = Math.Min(rowCount - 1, (int)MathF.Ceiling((scroll + vpMain) / mainStride) - 1);
        if (lastRow < firstRow)
        {
            lastRow = firstRow;
        }
    }

    private Element Realize(int index, GridView widget)
    {
        Widget built = widget.Builder(index);
        if (_active.TryGetValue(index, out Element? existing))
        {
            if (Widget.CanUpdate(existing.Widget, built))
            {
                existing.Update(built);
                return existing;
            }

            existing.Unmount();
        }

        Element created = built.CreateElement();
        created.Mount(this, Context);
        _active[index] = created;
        return created;
    }

    private void RemoveOutOfRange(int first, int last)
    {
        List<int>? remove = null;
        foreach (int idx in _active.Keys)
        {
            if (idx < first || idx > last)
            {
                (remove ??= new List<int>()).Add(idx);
            }
        }

        if (remove is null)
        {
            return;
        }

        for (int i = 0; i < remove.Count; i++)
        {
            _active[remove[i]].Unmount();
            _active.Remove(remove[i]);
        }
    }

    private void MaybeFireEndReached(GridView widget, int last, int count)
    {
        if (widget.OnEndReached is null || count == 0)
        {
            return;
        }

        if (last >= count - 1 && _lastEndCount != count)
        {
            _lastEndCount = count;
            widget.OnEndReached();
        }
    }

    private static BoxConstraints MakeCellConstraints(bool vertical, float cross, float main) =>
        vertical
            ? new BoxConstraints(cross, cross, main, main)
            : new BoxConstraints(main, main, cross, cross);

    private void Attach(GridView widget)
    {
        if (widget.Controller is not null)
        {
            widget.Controller.Target = this;
        }
    }
}
