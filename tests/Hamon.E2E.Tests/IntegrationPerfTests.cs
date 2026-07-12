using Hamon.Layout;
using Hamon.Testing.Perf;
using Hamon.Widgets;
using Xunit;

namespace Hamon.E2E.Tests;

/// <summary>
/// Performance regression of integration (full screen compositing).
/// Verify single spikes, virtualization maintenance (draw call limit), and no exceptions.
/// </summary>
public class IntegrationPerfTests
{
    private static readonly Size Phone = new(360, 720);

    [Fact]
    public void Dashboard_MixedInput_NoSteadyAlloc_NoSpike()
    {
        Action<HamonRoot, int> script = InputScript.Combine(
            InputScript.HoverSweep(360, 720),
            InputScript.Tap(new Vec2(330, 30), frame: 20),   // ヘッダのスイッチ
            InputScript.Tap(new Vec2(180, 300), frame: 50));  // ゴールカード

        PerfReport r = PerfHarness.Measure(Screens.Dashboard, Phone, script, frames: 90);

        r.AssertSteadyAllocBelow(4 * 1024);
        r.AssertMaxAllocBelow(64 * 1024);
        Assert.True(r.MaxDrawCalls > 0);
    }

    [Fact]
    public void Stress_ManyControls_SteadyLowAlloc()
    {
        PerfReport r = PerfHarness.Measure(Screens.Stress, Phone, InputScript.HoverSweep(360, 720), frames: 90);
        r.AssertSteadyAllocBelow(2 * 1024);
        r.AssertMaxAllocBelow(48 * 1024);
    }

    [Fact]
    public void List_DragScroll_StaysVirtualized_NoBlowup()
    {
        var ctrl = new ScrollController();
        PerfReport r = PerfHarness.Measure(
            () => Screens.VirtualList(1000, ctrl),
            Phone,
            InputScript.DragScroll(new Vec2(180, 360), 30f, startFrame: 3, frames: 14),
            frames: 60);

        Assert.True(ctrl.Offset > 80f, $"スクロールしていない (offset={ctrl.Offset})");
        r.AssertDrawCallsBelow(160);       // 可視セルのオーダー（崩れたら ~1000セル分に膨らむ）
        r.AssertMaxAllocBelow(96 * 1024);  // 全件再構築（~MB級）への退行を弾く
    }

    [Fact]
    public void Inventory_DragStart_NoLargeSpike()
    {
        PerfReport r = PerfHarness.Measure(
            Screens.Inventory,
            new Size(360, 480),
            InputScript.PointerDrag(new Vec2(48, 92), new Vec2(180, 300), startFrame: 3, holdFrames: 20),
            frames: 40);

        r.AssertNoSpikeAt(operationFrame: 4, allocBudgetBytes: 16 * 1024, drawCallBudget: 24);
        r.AssertSteadyAllocBelow(512);
    }
}
