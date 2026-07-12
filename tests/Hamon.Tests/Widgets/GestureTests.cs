using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test of gesture extension (velocity estimation, inertial fling, gesture arena = scroll vs. tap arbitration).</summary>
public class GestureTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 120);

    // 縦リスト（20件×40px＝800、viewport 120）。index0 だけ Button（残りは素のセル）。
    private static (HamonRoot host, ListViewElement list) MountListWithButton(FocusNode buttonNode, Action onTap, ScrollController? controller = null)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = 20,
            ItemExtent = 40f,
            Controller = controller,
            Builder = i => i == 0
                ? new Button { Node = buttonNode, OnPressed = onTap, Child = new SizedBox { Width = Dimension.Px(180), Height = Dimension.Px(40) } }
                : new SizedBox { Height = Dimension.Px(40) },
        });
        host.Update(Viewport);
        return (host, (ListViewElement)host.Root!);
    }

    [Fact]
    public void VelocityTracker_EstimatesPxPerSecond()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0f, 100f);
        tracker.Add(0.1f, 60f); // 40px を 0.1s で = -400 px/s

        Assert.Equal(-400f, tracker.Velocity(), 1f);
    }

    [Fact]
    public void VelocityTracker_TooFewSamples_IsZero()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0f, 100f);
        Assert.Equal(0f, tracker.Velocity());
    }

    [Fact]
    public void Fling_ContinuesScrollAfterRelease()
    {
        var controller = new ScrollController();
        (HamonRoot host, ListViewElement list) = MountListWithButton(new FocusNode(), () => { }, controller);

        // 速いドラッグ（指を上へ 60px / 0.05s）→ 離して慣性。
        host.DispatchPointer(new PointerEvent(new Vec2(50, 80), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 20), PointerPhase.Move, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 20), PointerPhase.Up, 0.06f));

        float afterDrag = list.ScrollOffset; // ドラッグ分のみ（60px）
        Assert.Equal(60f, afterDrag, 0.5f);

        host.Update(Viewport, 0.1f); // 慣性で前進
        Assert.True(list.ScrollOffset > afterDrag, $"after fling tick={list.ScrollOffset}");
    }

    [Fact]
    public void Fling_DecaysAndStops()
    {
        var controller = new ScrollController();
        (HamonRoot host, ListViewElement list) = MountListWithButton(new FocusNode(), () => { }, controller);

        host.DispatchPointer(new PointerEvent(new Vec2(50, 80), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 20), PointerPhase.Move, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 20), PointerPhase.Up, 0.06f));

        // 十分な時間進めると停止して落ち着く。
        for (int i = 0; i < 60; i++)
        {
            host.Update(Viewport, 0.05f);
        }

        float settled = list.ScrollOffset;
        host.Update(Viewport, 0.05f);
        Assert.Equal(settled, list.ScrollOffset); // もう動かない
    }

    [Fact]
    public void NewTouch_StopsActiveFling()
    {
        var controller = new ScrollController();
        (HamonRoot host, ListViewElement list) = MountListWithButton(new FocusNode(), () => { }, controller);

        host.DispatchPointer(new PointerEvent(new Vec2(50, 80), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 20), PointerPhase.Move, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 20), PointerPhase.Up, 0.06f));
        host.Update(Viewport, 0.1f); // 慣性が動いている

        // 新しいタッチ（押さえる）で慣性を止める。
        host.DispatchPointer(new PointerEvent(new Vec2(50, 60), PointerPhase.Down, 0.2f));
        float atDown = list.ScrollOffset;
        host.Update(Viewport, 0.1f);
        Assert.Equal(atDown, list.ScrollOffset); // 止まったまま
    }

    [Fact]
    public void Arena_TapInsideScrollable_FiresTap_NoScroll()
    {
        int taps = 0;
        (HamonRoot host, ListViewElement list) = MountListWithButton(new FocusNode(), () => taps++);

        // 動かさずに離す＝タップ。
        host.DispatchPointer(new PointerEvent(new Vec2(50, 20), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(52, 22), PointerPhase.Move, 0.02f)); // slop 未満
        host.DispatchPointer(new PointerEvent(new Vec2(52, 22), PointerPhase.Up, 0.03f));

        Assert.Equal(1, taps);
        Assert.Equal(0f, list.ScrollOffset);
    }

    [Fact]
    public void Arena_DragInsideScrollable_Scrolls_CancelsTap()
    {
        int taps = 0;
        (HamonRoot host, ListViewElement list) = MountListWithButton(new FocusNode(), () => taps++);

        // ボタン上で押して主軸に slop 超でドラッグ＝スクロールへ移譲、タップは打ち切り。
        host.DispatchPointer(new PointerEvent(new Vec2(50, 30), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 10), PointerPhase.Move, 0.05f)); // 20px 上＝slop 超
        host.DispatchPointer(new PointerEvent(new Vec2(50, 10), PointerPhase.Up, 0.06f));

        Assert.Equal(0, taps); // タップは発火しない
        Assert.True(list.ScrollOffset > 0f, $"offset={list.ScrollOffset}"); // スクロールした
    }

    [Fact]
    public void Arena_CrossAxisDrag_DoesNotScrollVerticalList()
    {
        int taps = 0;
        (HamonRoot host, ListViewElement list) = MountListWithButton(new FocusNode(), () => taps++);

        // 縦リストに対し横方向ドラッグ（主軸＝縦は動かない）→ 移譲されずタップ成立。
        host.DispatchPointer(new PointerEvent(new Vec2(40, 20), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(90, 20), PointerPhase.Move, 0.05f)); // 横 50px、縦 0
        host.DispatchPointer(new PointerEvent(new Vec2(90, 20), PointerPhase.Up, 0.06f));

        Assert.Equal(1, taps);
        Assert.Equal(0f, list.ScrollOffset);
    }

    [Fact]
    public void GestureDetector_Cancel_SuppressesTap()
    {
        int taps = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector { OnTap = () => taps++, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Cancel, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.02f));

        Assert.Equal(0, taps);
    }

    [Fact]
    public void GestureDetector_Cancel_FiresOnTapCancel()
    {
        int cancels = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector { OnTapCancel = () => cancels++, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Cancel, 0.01f));

        Assert.Equal(1, cancels);
    }

    [Fact]
    public void GestureDetector_ReleaseOutsideBounds_FiresOnTapCancel_NotTap()
    {
        int taps = 0;
        int cancels = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector
        {
            OnTap = () => taps++,
            OnTapCancel = () => cancels++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(250, 50), PointerPhase.Up, 0.02f)); // viewport 外で離す

        Assert.Equal(0, taps);
        Assert.Equal(1, cancels);
    }

    [Fact]
    public void GestureDetector_Pan_EmitsDeltas_StartAndEnd()
    {
        var deltas = new System.Collections.Generic.List<Vec2>();
        int starts = 0;
        int ends = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector
        {
            OnPanStart = _ => starts++,
            OnPanUpdate = d => deltas.Add(d.Delta),
            OnPanEnd = _ => ends++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(20, 20), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(45, 20), PointerPhase.Move, 0.02f)); // 25px 右＝slop 超でパン開始
        host.DispatchPointer(new PointerEvent(new Vec2(55, 30), PointerPhase.Move, 0.03f)); // +10,+10
        host.DispatchPointer(new PointerEvent(new Vec2(55, 30), PointerPhase.Up, 0.04f));

        Assert.Equal(1, starts);
        Assert.Equal(1, ends);
        Assert.Equal(2, deltas.Count);
        Assert.Equal(25f, deltas[0].X, 0.5f); // 初回 delta は開始位置からの移動量
        Assert.Equal(0f, deltas[0].Y, 0.5f);
        Assert.Equal(10f, deltas[1].X, 0.5f);
        Assert.Equal(10f, deltas[1].Y, 0.5f);
    }

    [Fact]
    public void GestureDetector_Pan_SuppressesTap()
    {
        int taps = 0;
        int panEnds = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector
        {
            OnTap = () => taps++,
            OnPanUpdate = _ => { },
            OnPanEnd = _ => panEnds++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(20, 20), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(60, 20), PointerPhase.Move, 0.02f)); // slop 超＝ドラッグ
        host.DispatchPointer(new PointerEvent(new Vec2(60, 20), PointerPhase.Up, 0.03f));

        Assert.Equal(0, taps); // ドラッグはタップを出さない
        Assert.Equal(1, panEnds);
    }

    [Fact]
    public void GestureDetector_QuickTap_DoesNotStartPan()
    {
        int taps = 0;
        int panStarts = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector
        {
            OnTap = () => taps++,
            OnPanStart = _ => panStarts++,
            OnPanUpdate = _ => { },
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(20, 20), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(22, 21), PointerPhase.Up, 0.02f)); // slop 未満

        Assert.Equal(1, taps);
        Assert.Equal(0, panStarts);
    }

    [Fact]
    public void GestureDetector_LongPress_FiresTapCancelBeforeLongPress()
    {
        var log = new System.Collections.Generic.List<string>();
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector
        {
            OnTap = () => log.Add("tap"),
            OnTapCancel = () => log.Add("cancel"),
            OnLongPress = () => log.Add("long"),
            LongPressDuration = 0.5f,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport, 0.6f); // 長押し閾値超え
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.7f));

        Assert.Equal(new[] { "cancel", "long" }, log); // tap は発火しない
    }
}
