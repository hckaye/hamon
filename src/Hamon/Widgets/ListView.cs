using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A virtualizing list (equivalent to Flutter's <c>ListView.builder</c>). Of the <see cref="ItemCount"/> items, only
/// the cells in the <b>visible range</b> are materialized via <see cref="Builder"/>, and are recreated as scrolling
/// proceeds (lightweight even with a huge number of items). When <see cref="ItemExtent"/> is specified, a fast
/// O(visible) path is used (fixed height). Otherwise, <see cref="EstimatedExtent"/> is used as a placeholder for
/// unmeasured cells, and actual measured values are cached as cells come into view (variable height).
/// <see cref="OnEndReached"/> supports infinite scroll.
/// </summary>
public sealed class ListView : Widget
{
    public int ItemCount { get; init; }

    /// <summary>Maps an index to a cell widget.</summary>
    public Func<int, Widget> Builder { get; init; } = static _ => new SizedBox();

    /// <summary>Scroll direction (default <see cref="Axis.Vertical"/>).</summary>
    public Axis Axis { get; init; } = Axis.Vertical;

    /// <summary>A fixed cell length along the main axis (px).</summary>
    public float? ItemExtent { get; init; }

    /// <summary>Estimated cell length when variable height (temporary placement of unmeasured cells).</summary>
    public float EstimatedExtent { get; init; } = 48f;

    public ScrollController? Controller { get; init; }

    /// <summary>Overscroll at the edges (rubber-band effect with return).</summary>
    public bool Bounce { get; init; } = true;

    /// <summary>
    /// Whether manual scrolling (drag/wheel) is enabled. Setting this to false disables user-interaction scrolling,
    /// but programmatic control (via <see cref="Controller"/>, scroll-to-focus, etc.) remains possible. Equivalent
    /// to Flutter's <c>NeverScrollableScrollPhysics</c>.
    /// </summary>
    public bool ManualScroll { get; init; } = true;

    /// <summary>Scroll movement constants (sensitivity/following/rubber-band/inertia). See <see cref="HamonTheme.ScrollPhysics"/>.</summary>
    public ScrollPhysics? Physics { get; init; }

    public Dimension Width { get; init; }

    public Dimension Height { get; init; }

    /// <summary>Fires once when visibility reaches near the end of the list (for infinite scroll, e.g. to increase ItemCount).</summary>
    public Action? OnEndReached { get; init; }

    /// <summary>How many items before the end <see cref="OnEndReached"/> should fire in advance (prefetch; default 0 means it fires when the last item becomes visible).</summary>
    public int EndReachedThreshold { get; init; }

    public override Element CreateElement() => new ListViewElement(this);
}

/// <summary>
/// The holding entity for <see cref="ListView"/>.
/// Maintains the scroll offset, updated via drag or controller, and draws with a rectangular clip.
/// </summary>
internal sealed class ListViewElement : Element, IVirtualLayout, IScrollable
{
    private readonly LayoutNode _node;
    private readonly Dictionary<int, Element> _active = new();
    private readonly Dictionary<int, float> _extents = new();
    private readonly List<Element> _visible = new();
    private readonly DragScroller _drag;

    private int _lastEndCount = -1;

    // 可変高の累積長を O(log n) で扱う Fenwick（BIT）。各要素は (実測 extent − 推定) の差分を保持し、
    // offset(i)=推定*i+prefix(i)、content=推定*count+prefix(count)。ItemCount/推定が変わったら作り直す。
    private float[] _bit = System.Array.Empty<float>();
    private int _bitCount = -1;
    private float _bitEstimated = float.NaN;

    public ListViewElement(ListView widget)
        : base(widget)
    {
        _node = new LayoutNode { Virtual = this };
        _drag = new DragScroller(this);
    }

    public override LayoutNode LayoutNode => _node;

    public override IReadOnlyList<Element> Children => _visible;

    public float ScrollOffset { get; private set; }

    /// <summary>The overscroll displacement (the amount of rubber-band travel past the edge; for inspection/testing purposes).</summary>
    internal float Overscroll => _drag.Overscroll;

    Axis IScrollable.ScrollAxis => ((ListView)Widget).Axis;

    ScrollPhysics IScrollable.Physics => ((ListView)Widget).Physics ?? Context.Theme.ScrollPhysics;

    /// <summary>The set of currently materialized indices (for inspection/testing).</summary>
    internal IReadOnlyCollection<int> ActiveIndices => _active.Keys;

    /// <summary>The element for the specified index, or null if it does not exist (for inspection/testing).</summary>
    internal Element? ActiveElement(int index) => _active.TryGetValue(index, out Element? e) ? e : null;

    void IScrollable.SetScroll(float offset) => SetScroll(offset);

    void IScrollable.RevealRect(Rect target) => ScrollReveal.Reveal(this, _node.Bounds, target);

    void IScrollable.ScrollByAnimated(float offsetDelta) => _drag.ScrollBy(offsetDelta);

    void IScrollable.AnimateScrollTo(float offset) => _drag.GlideTo(offset);

    float IScrollable.MaxScroll
    {
        get
        {
            var widget = (ListView)Widget;
            float content = ContentExtent(widget.ItemCount, widget);
            float vpMain = Vertical ? _node.Size.Height : _node.Size.Width;
            return Math.Max(0f, content - vpMain);
        }
    }

    bool IScrollable.BounceEnabled => ((ListView)Widget).Bounce;

    bool IScrollable.ManualScrollEnabled => ((ListView)Widget).ManualScroll;

    public override bool WantsPointer => true;

    private bool Vertical => ((ListView)Widget).Axis == Axis.Vertical;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        _drag.Owner = Context.Owner;
        Attach((ListView)Widget);
    }

    public override void Update(Widget newWidget)
    {
        var old = (ListView)Widget;
        base.Update(newWidget);
        var updated = (ListView)newWidget;
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
        var widget = (ListView)Widget;
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
        if (!((ListView)Widget).ManualScroll)
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

    /// <summary>Virtualized measurement: builds and measures only the cells in the visible range, sets children and offsets, and returns the viewport size.</summary>
    public Size Measure(LayoutNode node, BoxConstraints constraints)
    {
        var widget = (ListView)Widget;
        bool vertical = Vertical;

        float? fixedMain = vertical ? widget.Height.Resolve(constraints.MaxHeight) : widget.Width.Resolve(constraints.MaxWidth);
        float? fixedCross = vertical ? widget.Width.Resolve(constraints.MaxWidth) : widget.Height.Resolve(constraints.MaxHeight);

        float maxMain = vertical ? constraints.MaxHeight : constraints.MaxWidth;
        float maxCross = vertical ? constraints.MaxWidth : constraints.MaxHeight;

        float vpMain = fixedMain ?? (float.IsFinite(maxMain) ? maxMain : 0f);
        float vpCross = fixedCross ?? (float.IsFinite(maxCross) ? maxCross : 0f);
        Size viewport = constraints.Constrain(vertical ? new Size(vpCross, vpMain) : new Size(vpMain, vpCross));
        vpMain = vertical ? viewport.Height : viewport.Width;
        vpCross = vertical ? viewport.Width : viewport.Height;

        int count = widget.ItemCount;
        float content = ContentExtent(count, widget);
        ScrollOffset = Math.Clamp(ScrollOffset, 0f, Math.Max(0f, content - vpMain));
        float scroll = ScrollOffset;

        FindVisibleRange(count, widget, scroll, vpMain, out int first, out int last);

        _visible.Clear();
        _node.Clear();
        RealizeRange(widget, first, last, vertical, vpCross, scroll);

        MaybeFireEndReached(widget, last, count);
        return viewport;
    }

    internal void SetScroll(float offset)
    {
        var widget = (ListView)Widget;
        float content = ContentExtent(widget.ItemCount, widget);
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

    /// <summary>
    /// Materializes the visible range. <b>Rows with a Key reuse the same element across index changes</b>
    /// (preserving in-row state even when items are deleted or reordered — keyed virtualization).
    /// </summary>
    private void RealizeRange(ListView widget, int first, int last, bool vertical, float vpCross, float scroll)
    {
        Dictionary<object, Element>? oldByKey = null;
        foreach (KeyValuePair<int, Element> kv in _active)
        {
            if (kv.Value.Widget.Key is object key)
            {
                (oldByKey ??= new Dictionary<object, Element>()).TryAdd(key, kv.Value);
            }
        }

        var used = new HashSet<Element>();
        var next = new Dictionary<int, Element>();
        BoxConstraints itemConstraints = MakeItemConstraints(vertical, vpCross, widget.ItemExtent);

        for (int i = first; i <= last; i++)
        {
            Widget built = widget.Builder(i);
            Element element = ReuseOrCreate(built, i, oldByKey, used);

            FlexLayoutEngine.MeasureNode(element.LayoutNode, itemConstraints);
            SetExtent(i, vertical ? element.LayoutNode.Size.Height : element.LayoutNode.Size.Width, widget);

            float itemOffset = OffsetOf(i, widget) - scroll;
            element.LayoutNode.OffsetX = vertical ? 0f : itemOffset;
            element.LayoutNode.OffsetY = vertical ? itemOffset : 0f;

            next[i] = element;
            _visible.Add(element);
            _node.Add(element.LayoutNode);
        }

        foreach (KeyValuePair<int, Element> kv in _active)
        {
            if (!used.Contains(kv.Value))
            {
                kv.Value.Unmount(); // 可視外になった/再利用されなかった行
            }
        }

        _active.Clear();
        foreach (KeyValuePair<int, Element> kv in next)
        {
            _active[kv.Key] = kv.Value;
        }
    }

    private Element ReuseOrCreate(Widget built, int index, Dictionary<object, Element>? oldByKey, HashSet<Element> used)
    {
        // Key 付き：同じ Key の旧要素を index 跨ぎで再利用（並べ替え/削除で identity 維持）。
        if (built.Key is object key
            && oldByKey is not null
            && oldByKey.TryGetValue(key, out Element? byKey)
            && Widget.CanUpdate(byKey.Widget, built)
            && used.Add(byKey))
        {
            byKey.Update(built);
            return byKey;
        }

        // Key 無し：同じ index の旧要素を再利用（従来の仮想化）。
        if (built.Key is null
            && _active.TryGetValue(index, out Element? byIndex)
            && byIndex.Widget.Key is null
            && Widget.CanUpdate(byIndex.Widget, built)
            && used.Add(byIndex))
        {
            byIndex.Update(built);
            return byIndex;
        }

        Element created = built.CreateElement();
        created.Mount(this, Context);
        return created;
    }

    private float Extent(int index, ListView widget) =>
        widget.ItemExtent ?? (_extents.TryGetValue(index, out float e) ? e : widget.EstimatedExtent);

    private float OffsetOf(int index, ListView widget)
    {
        if (widget.ItemExtent is float ext)
        {
            return index * ext;
        }

        EnsureExtentIndex(widget.ItemCount, widget.EstimatedExtent);
        return (widget.EstimatedExtent * index) + BitPrefix(index);
    }

    private float ContentExtent(int count, ListView widget)
    {
        if (widget.ItemExtent is float ext)
        {
            return count * ext;
        }

        EnsureExtentIndex(count, widget.EstimatedExtent);
        return (widget.EstimatedExtent * count) + BitPrefix(count);
    }

    private void FindVisibleRange(int count, ListView widget, float scroll, float vpMain, out int first, out int last)
    {
        if (count == 0)
        {
            first = 0;
            last = -1;
            return;
        }

        if (widget.ItemExtent is float ext && ext > 0f)
        {
            first = Math.Max(0, (int)MathF.Floor(scroll / ext));
            last = Math.Min(count - 1, (int)MathF.Ceiling((scroll + vpMain) / ext) - 1);
            return;
        }

        // 可変高：Fenwick による offset() の二分探索（O(log^2 n)）。offset は単調増加。
        EnsureExtentIndex(count, widget.EstimatedExtent);
        first = Math.Clamp(FirstOffsetGreater(count, widget, scroll) - 1, 0, count - 1);
        last = Math.Clamp(FirstOffsetAtLeast(count, widget, scroll + vpMain) - 1, first, count - 1);
    }

    // offset(i) > target となる最小の i（0..count）。
    private int FirstOffsetGreater(int count, ListView widget, float target)
    {
        int lo = 0;
        int hi = count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (OffsetOf(mid, widget) <= target)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    // offset(i) >= target となる最小の i（0..count）。
    private int FirstOffsetAtLeast(int count, ListView widget, float target)
    {
        int lo = 0;
        int hi = count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (OffsetOf(mid, widget) < target)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    // --- Fenwick（BIT）：実測差分の累積を O(log n) で ---

    private void EnsureExtentIndex(int count, float estimated)
    {
        if (_bitCount == count && _bitEstimated.Equals(estimated))
        {
            return;
        }

        // ItemCount/推定が変わったら差分インデックスと実測キャッシュを無効化（再測定で埋め直す）。
        _bit = new float[count + 1];
        _bitCount = count;
        _bitEstimated = estimated;
        _extents.Clear();
    }

    private void SetExtent(int index, float extent, ListView widget)
    {
        EnsureExtentIndex(widget.ItemCount, widget.EstimatedExtent);
        if (index < 0 || index >= _bitCount)
        {
            return;
        }

        float oldDelta = _extents.TryGetValue(index, out float prev) ? prev - widget.EstimatedExtent : 0f;
        float newDelta = extent - widget.EstimatedExtent;
        _extents[index] = extent;
        BitAdd(index, newDelta - oldDelta);
    }

    private void BitAdd(int index, float delta)
    {
        if (delta == 0f)
        {
            return;
        }

        for (int i = index + 1; i <= _bitCount; i += i & -i)
        {
            _bit[i] += delta;
        }
    }

    // deltas[0..index-1] の合計（実測差分の prefix）。
    private float BitPrefix(int index)
    {
        float sum = 0f;
        for (int i = Math.Min(index, _bitCount); i > 0; i -= i & -i)
        {
            sum += _bit[i];
        }

        return sum;
    }

    private void MaybeFireEndReached(ListView widget, int last, int count)
    {
        if (widget.OnEndReached is null || count == 0)
        {
            return;
        }

        // 末尾から threshold 件手前で先行発火。count が変わるまで再発火しない（重複防止）。
        if (last >= count - 1 - Math.Max(0, widget.EndReachedThreshold) && _lastEndCount != count)
        {
            _lastEndCount = count;
            widget.OnEndReached();
        }
    }

    private static BoxConstraints MakeItemConstraints(bool vertical, float cross, float? itemExtent)
    {
        if (itemExtent is float ext)
        {
            return vertical
                ? new BoxConstraints(cross, cross, ext, ext)
                : new BoxConstraints(ext, ext, cross, cross);
        }

        return vertical
            ? new BoxConstraints(cross, cross, 0f, float.PositiveInfinity)
            : new BoxConstraints(0f, float.PositiveInfinity, cross, cross);
    }

    private void Attach(ListView widget)
    {
        if (widget.Controller is not null)
        {
            widget.Controller.Target = this;
        }
    }
}
