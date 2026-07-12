using Hamon.Layout;
using Hamon.Testing.Perf;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Performance;

/// <summary>
/// Performance regression for individual components.
/// Fixed past actual bugs (D&D drag start spikes and scroll spikes) with ``spike in operation frame'' and ``maintain virtualization.''
/// Allocation is based on thread-specific counter differences = deterministic (independent of GC real time).
/// </summary>
public class WidgetPerfTests
{
    private static readonly Size Phone = new(360, 640);

    // ---- 定常状態（操作なし）：ほぼゼロアロケ ----

    [Fact]
    public void SteadyState_MixedControls_LowAlloc()
    {
        PerfReport r = PerfHarness.Measure(
            () => new Column
            {
                Children = new Widget[]
                {
                    new Text("Title") { FontSize = 22f },
                    new Button { OnPressed = () => { }, Background = new Color(40, 44, 54), Child = new Text("OK") },
                    new Slider { Value = 0.5f, OnChanged = _ => { } },
                    new Checkbox { Value = true, OnChanged = _ => { } },
                    new Switch { Value = false, OnChanged = _ => { } },
                },
            },
            Phone,
            InputScript.None,
            frames: 120);

        r.AssertSteadyAllocBelow(512); // 定常フレームはほぼゼロ
        Assert.True(r.MaxDrawCalls > 0);
    }

    // ---- D&D ドラッグ開始のスパイク（過去バグ） ----

    [Fact]
    public void Drag_Start_DoesNotSpike_AndMoveIsAllocationFree()
    {
        PerfReport r = PerfHarness.Measure(
            DragScene,
            Phone,
            InputScript.PointerDrag(new Vec2(40, 20), new Vec2(60, 160), startFrame: 3, holdFrames: 20),
            frames: 40);

        // ドラッグ開始フレーム（startFrame+1=4）は DragLayer の局所再構築のみ＝小さく収まる
        // （全ツリー再構築へ退行すると大きく超える）。
        r.AssertNoSpikeAt(operationFrame: 4, allocBudgetBytes: 8 * 1024, drawCallBudget: 12);

        // ドラッグ移動中（定常）はほぼゼロアロケ＝毎フレームのカクつきが無いこと。
        r.AssertSteadyAllocBelow(256);
    }

    // ---- スクロールのスパイク／仮想化（過去バグ） ----

    [Fact]
    public void ListView_DragScroll_StaysVirtualized_NoBlowup()
    {
        var ctrl = new ScrollController();
        PerfReport r = PerfHarness.Measure(
            () => ListScene(ctrl),
            Phone,
            InputScript.DragScroll(new Vec2(180, 300), 30f, startFrame: 3, frames: 12),
            frames: 50);

        Assert.True(ctrl.Offset > 100f, $"スクロールが起きていない (offset={ctrl.Offset})"); // テストが空振りでない証拠
        r.AssertRealizedBelow(40);          // 1000 件でも可視のみ実体化（仮想化維持）
        r.AssertDrawCallsBelow(60);         // 描画コールが可視数のオーダーに収まる
        r.AssertMaxAllocBelow(64 * 1024);   // 全 1000 件再構築などへの退行（~MB級スパイク）を弾く
    }

    [Fact]
    public void ListView_TenThousand_RealizesOnlyVisible()
    {
        PerfReport r = PerfHarness.Measure(
            () => new ListView
            {
                ItemCount = 10000,
                ItemExtent = 40f,
                Builder = _ => new Container { Height = Dimension.Px(40), Color = new Color(40, 44, 54) },
            },
            Phone,
            InputScript.None,
            frames: 10);

        r.AssertRealizedBelow(40);
        r.AssertDrawCallsBelow(60);
    }

    private static Widget DragScene() => new Stack
    {
        Children = new Widget[]
        {
            new Column
            {
                Children = new Widget[]
                {
                    new Draggable<int>
                    {
                        Data = 1,
                        Child = new Container { Width = Dimension.Px(80), Height = Dimension.Px(40), Color = new Color(80, 120, 200) },
                        Feedback = new Container { Width = Dimension.Px(80), Height = Dimension.Px(40), Color = new Color(80, 120, 200, 180) },
                    },
                    new SizedBox { Height = Dimension.Px(40) },
                    new DragTarget<int> { OnAccept = _ => { }, Child = new Container { Width = Dimension.Px(160), Height = Dimension.Px(100), Color = new Color(60, 60, 70) } },
                },
            },
            new DragLayer(),
        },
    };

    private static Widget ListScene(ScrollController ctrl) => new ListView
    {
        ItemCount = 1000,
        ItemExtent = 40f,
        Controller = ctrl,
        Builder = i => new Container
        {
            Height = Dimension.Px(40),
            Color = (i % 2 == 0) ? new Color(40, 44, 54) : new Color(50, 54, 64),
            Child = new Text("row") { FontSize = 14f },
        },
    };
}
