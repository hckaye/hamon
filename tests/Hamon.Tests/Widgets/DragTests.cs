using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of Draggable/DragTarget (drag and drop → OnAccept, tap below slop, reject with CanAccept).</summary>
public class DragTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static HamonRoot Build(Action<string>? onAccept, Func<string, bool>? canAccept, Action? onTap, out HamonRoot host, float slop = DraggableElement.DefaultSlop, bool withLayer = false)
    {
        host = new HamonRoot(new StubText());
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new Draggable<string> { Data = "item", OnTap = onTap, Slop = slop, Child = new SizedBox() },
                },
                new Positioned
                {
                    Left = Dimension.Px(200f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new DragTarget<string> { OnAccept = onAccept, CanAccept = canAccept, Child = new SizedBox() },
                },
                withLayer ? new DragLayer() : new SizedBox(),
            },
        });
        host.Update(new Size(400, 400));
        return host;
    }

    [Fact]
    public void Drag_DropsOnTarget()
    {
        object? accepted = null;
        Build(d => accepted = d, null, null, out HamonRoot host);

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Move, 0.05f)); // ターゲット上へ（slop 超）
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Up, 0.1f));

        Assert.Equal("item", accepted);
    }

    [Fact]
    public void Slop_Large_PreventsDrag()
    {
        // slop を大きくすると、ターゲットまで動かしても閾値未満でドラッグ開始しない（→ ドロップ受理されない）。
        object? accepted = null;
        Build(d => accepted = d, null, null, out HamonRoot host, slop: 300f);

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Move, 0.05f)); // 200px < 300px slop
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Up, 0.1f));

        Assert.Null(accepted); // ドラッグ未開始＝移動なし
    }

    [Fact]
    public void DragLayer_Present_StillDrops()
    {
        // DragLayer をツリーに置いても（フィードバックは層が描く＝局所再構築）D&D は成立する。
        object? accepted = null;
        Build(d => accepted = d, null, null, out HamonRoot host, withLayer: true);

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Move, 0.05f));
        host.Update(new Size(400, 400)); // 層の局所再構築を進める
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Up, 0.1f));

        Assert.Equal("item", accepted);
    }

    [Fact]
    public void Drag_OutsideTarget_NoAccept()
    {
        object? accepted = null;
        Build(d => accepted = d, null, null, out HamonRoot host);

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(120, 300), PointerPhase.Move, 0.05f)); // 空き地へ
        host.DispatchPointer(new PointerEvent(new Vec2(120, 300), PointerPhase.Up, 0.1f));

        Assert.Null(accepted);
    }

    [Fact]
    public void CanAccept_False_Rejects()
    {
        object? accepted = null;
        Build(d => accepted = d, _ => false, null, out HamonRoot host);

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Move, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Up, 0.1f));

        Assert.Null(accepted); // 受け取り拒否
    }

    private sealed class NullPainter : IPainter
    {
        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color)
        {
        }

        public void FillRoundedRect(Rect rect, Color color, float radius)
        {
        }

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private sealed class BoundsProbe : Widget
    {
        public required Action<Rect> OnPaint { get; init; }

        public float Size { get; init; } = 50f;

        public override Element CreateElement() => new BoundsProbeElement(this);
    }

    private sealed class BoundsProbeElement : Element
    {
        private readonly LayoutNode _node;

        public BoundsProbeElement(BoundsProbe widget)
            : base(widget)
        {
            _node = new LayoutNode(measure: _ => new Size(((BoundsProbe)Widget).Size, ((BoundsProbe)Widget).Size));
        }

        public override LayoutNode LayoutNode => _node;

        public override void Paint(in PaintContext context) => ((BoundsProbe)Widget).OnPaint(_node.Bounds);
    }

    [Fact]
    public void DragLayer_Feedback_StaysNaturalSize_UnderExpandStack()
    {
        // StackFit.Expand 内の DragLayer は tight な全画面制約を受けるが、feedback は自然サイズを保つ（巨大化しない）。
        Rect fb = default;
        var feedback = new BoundsProbe { Size = 50f, OnPaint = b => fb = b };
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new Draggable<string> { Data = "x", Feedback = feedback, Child = new SizedBox() },
                },
                new DragLayer(),
            },
        });
        host.Update(new Size(400, 400));

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(200, 200), PointerPhase.Move, 0.05f)); // ドラッグ開始
        host.Update(new Size(400, 400)); // DragLayer が局所再構築
        host.Render(new NullPainter());

        Assert.Equal(50f, fb.Width, 1); // 全画面(400)に伸びていない
        Assert.Equal(50f, fb.Height, 1);
    }

    [Fact]
    public void Tap_WithoutDrag_FiresOnTap()
    {
        bool tapped = false;
        object? accepted = null;
        Build(d => accepted = d, null, () => tapped = true, out HamonRoot host);

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(41, 41), PointerPhase.Up, 0.05f)); // slop 未満＝タップ

        Assert.True(tapped);
        Assert.Null(accepted);
    }

    [Fact]
    public void TypeMismatch_TargetIgnoresOtherDataType()
    {
        // DragTarget<int> は string を運ぶドラッグを自動で無視する（手動の型ガード不要）。
        bool accepted = false;
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new Draggable<string> { Data = "item", Child = new SizedBox() },
                },
                new Positioned
                {
                    Left = Dimension.Px(200f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new DragTarget<int> { OnAccept = _ => accepted = true, Child = new SizedBox() },
                },
            },
        });
        host.Update(new Size(400, 400));

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Move, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Up, 0.1f));

        Assert.False(accepted);
    }

    [Fact]
    public void DragLifecycle_StartedAndEnded_Accepted()
    {
        bool started = false;
        bool? endedAccepted = null;
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new Draggable<string>
                    {
                        Data = "item",
                        OnDragStarted = () => started = true,
                        OnDragEnd = a => endedAccepted = a,
                        Child = new SizedBox(),
                    },
                },
                new Positioned
                {
                    Left = Dimension.Px(200f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new DragTarget<string> { Child = new SizedBox() },
                },
            },
        });
        host.Update(new Size(400, 400));

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Move, 0.05f));
        Assert.True(started); // ドラッグ開始で発火

        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Up, 0.1f));
        Assert.Equal(true, endedAccepted); // ターゲット上でドロップ＝受理
    }

    [Fact]
    public void DragLifecycle_Ended_NotAccepted_WhenDroppedOffTarget()
    {
        bool? endedAccepted = null;
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new Draggable<string> { Data = "item", OnDragEnd = a => endedAccepted = a, Child = new SizedBox() },
                },
                new Positioned
                {
                    Left = Dimension.Px(200f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new DragTarget<string> { Child = new SizedBox() },
                },
            },
        });
        host.Update(new Size(400, 400));

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(120, 300), PointerPhase.Move, 0.05f)); // 空き地へ
        host.DispatchPointer(new PointerEvent(new Vec2(120, 300), PointerPhase.Up, 0.1f));

        Assert.Equal(false, endedAccepted); // 圏外ドロップ＝非受理
    }

    [Fact]
    public void OnLeave_FiresWhenDragLeavesTarget()
    {
        int leaves = 0;
        string? lastLeft = null;
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new Draggable<string> { Data = "item", Child = new SizedBox() },
                },
                new Positioned
                {
                    Left = Dimension.Px(200f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new DragTarget<string> { OnLeave = s => { leaves++; lastLeft = s; }, Child = new SizedBox() },
                },
            },
        });
        host.Update(new Size(400, 400));

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Move, 0.05f)); // ターゲット上へ
        host.DispatchPointer(new PointerEvent(new Vec2(120, 300), PointerPhase.Move, 0.1f)); // ターゲットから外れる→OnLeave

        Assert.Equal(1, leaves);
        Assert.Equal("item", lastLeft);

        host.DispatchPointer(new PointerEvent(new Vec2(120, 300), PointerPhase.Up, 0.15f));
        Assert.Equal(1, leaves); // 圏外ドロップでは追加発火なし
    }

    [Fact]
    public void ChildWhenDragging_ShownWhileDraggingThenRestored()
    {
        string painted = "";
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new Draggable<string>
                    {
                        Data = "x",
                        Feedback = new SizedBox(), // フィードバックは無描画にして元位置の子だけ観測する
                        Child = new PaintTag { Tag = "normal", Report = t => painted = t },
                        ChildWhenDragging = new PaintTag { Tag = "dragging", Report = t => painted = t },
                    },
                },
            },
        });
        host.Update(new Size(400, 400));
        host.Render(new NullPainter());
        Assert.Equal("normal", painted);

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(200, 200), PointerPhase.Move, 0.05f)); // ドラッグ開始
        host.Update(new Size(400, 400)); // 元位置を ChildWhenDragging へ局所差し替え＋再レイアウト
        painted = "";
        host.Render(new NullPainter());
        Assert.Equal("dragging", painted);

        host.DispatchPointer(new PointerEvent(new Vec2(200, 200), PointerPhase.Up, 0.1f)); // ドロップ
        host.Update(new Size(400, 400)); // 元の Child へ復帰
        painted = "";
        host.Render(new NullPainter());
        Assert.Equal("normal", painted);
    }

    [Fact]
    public void Builder_ReflectsHoverState()
    {
        // DragTarget<T>.Builder（escape hatch）は hover 状態に応じて見た目を建て直す。
        string painted = "";
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new Draggable<string> { Data = "item", Feedback = new SizedBox(), Child = new SizedBox() },
                },
                new Positioned
                {
                    Left = Dimension.Px(200f), Top = Dimension.Px(0f), Width = Dimension.Px(80f), Height = Dimension.Px(80f),
                    Child = new DragTarget<string>
                    {
                        Builder = state => new PaintTag { Tag = state.IsActive ? $"active:{state.Candidate}" : "idle", Report = t => painted = t },
                    },
                },
            },
        });
        host.Update(new Size(400, 400));
        host.Render(new NullPainter());
        Assert.Equal("idle", painted);

        host.DispatchPointer(new PointerEvent(new Vec2(40, 40), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(240, 40), PointerPhase.Move, 0.05f)); // ターゲット上へ
        host.Update(new Size(400, 400)); // 局所再構築で active を反映
        painted = "";
        host.Render(new NullPainter());
        Assert.Equal("active:item", painted); // 候補データも渡る

        host.DispatchPointer(new PointerEvent(new Vec2(120, 300), PointerPhase.Move, 0.1f)); // 外れる
        host.Update(new Size(400, 400));
        painted = "";
        host.Render(new NullPainter());
        Assert.Equal("idle", painted);

        host.DispatchPointer(new PointerEvent(new Vec2(120, 300), PointerPhase.Up, 0.15f));
    }

    private sealed class PaintTag : Widget
    {
        public required string Tag { get; init; }

        public required Action<string> Report { get; init; }

        public float Size { get; init; } = 50f;

        public override Element CreateElement() => new PaintTagElement(this);
    }

    private sealed class PaintTagElement : Element
    {
        private readonly LayoutNode _node;

        public PaintTagElement(PaintTag widget)
            : base(widget)
        {
            _node = new LayoutNode(measure: _ => new Size(((PaintTag)Widget).Size, ((PaintTag)Widget).Size));
        }

        public override LayoutNode LayoutNode => _node;

        public override void Paint(in PaintContext context) => ((PaintTag)Widget).Report(((PaintTag)Widget).Tag);
    }
}
