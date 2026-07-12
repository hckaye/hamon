using Xunit;

namespace Hamon.Testing.Perf;

/// <summary>
/// <see cref="PerfHarness"/>measurement results.<b>By frame</b>Maintain the allocation amount and number of draw calls (do not crush spikes on average),
/// Provides asserts that deterministically verify ``spikes in operation frames,'' ``stationary upper limits,'' and ``maintenance of virtualization.''
/// </summary>
public sealed class PerfReport
{
    public PerfReport(long[] perFrameAllocBytes, int[] perFrameDrawCalls, int[] realizedTopLevelChildren, double elapsedMs)
    {
        PerFrameAllocBytes = perFrameAllocBytes;
        PerFrameDrawCalls = perFrameDrawCalls;
        RealizedTopLevelChildren = realizedTopLevelChildren;
        ElapsedMs = elapsedMs;
    }

    public long[] PerFrameAllocBytes { get; }

    public int[] PerFrameDrawCalls { get; }

    public int[] RealizedTopLevelChildren { get; }

    /// <summary>Actual time of measurement interval (reference value, not used for pass/fail).</summary>
    public double ElapsedMs { get; }

    public long MaxFrameAllocBytes => PerFrameAllocBytes.Max();

    public int MaxAllocFrameIndex => Array.IndexOf(PerFrameAllocBytes, MaxFrameAllocBytes);

    public long SteadyAllocBytes => Median(PerFrameAllocBytes);

    public int MaxDrawCalls => PerFrameDrawCalls.Max();

    public int SteadyDrawCalls
    {
        get
        {
            long[] vals = new long[PerFrameDrawCalls.Length];
            for (int i = 0; i < vals.Length; i++)
            {
                vals[i] = PerFrameDrawCalls[i];
            }

            return (int)Median(vals);
        }
    }

    /// <summary>Verify that the allocation/draw call of the operation frame is within "steady + budget" (directly fix single spike).</summary>
    public void AssertNoSpikeAt(int operationFrame, long allocBudgetBytes, int? drawCallBudget = null)
    {
        long steady = SteadyAllocBytes;
        long got = PerFrameAllocBytes[operationFrame];
        Assert.True(
            got <= steady + allocBudgetBytes,
            $"操作フレーム {operationFrame} でアロケスパイク: {got} bytes > 定常 {steady} + 予算 {allocBudgetBytes}。" +
            $"（全フレーム最大 {MaxFrameAllocBytes}@{MaxAllocFrameIndex}）");

        if (drawCallBudget is int budget)
        {
            int steadyCalls = SteadyDrawCalls;
            int gotCalls = PerFrameDrawCalls[operationFrame];
            Assert.True(
                gotCalls <= steadyCalls + budget,
                $"操作フレーム {operationFrame} で描画コールスパイク: {gotCalls} > 定常 {steadyCalls} + 予算 {budget}");
        }
    }

    /// <summary>Single allocation is within the upper limit in every frame (there are no spikes anywhere).</summary>
    public void AssertMaxAllocBelow(long bytes) =>
        Assert.True(MaxFrameAllocBytes <= bytes, $"単発アロケ最大 {MaxFrameAllocBytes}@{MaxAllocFrameIndex} > 上限 {bytes}");

    /// <summary>Steady (median) allocation is within the upper limit.</summary>
    public void AssertSteadyAllocBelow(long bytes) =>
        Assert.True(SteadyAllocBytes <= bytes, $"定常アロケ {SteadyAllocBytes} > 上限 {bytes}");

    /// <summary>The maximum drawing call is within the upper limit (virtualization collapse = detection of rapid increase in calls).</summary>
    public void AssertDrawCallsBelow(int maxCalls) =>
        Assert.True(MaxDrawCalls <= maxCalls, $"描画コール最大 {MaxDrawCalls} > 上限 {maxCalls}（仮想化崩れの可能性）");

    /// <summary>The number of top-level materializations is within the upper limit (when root is a virtual scroll = only visible objects are materialized).</summary>
    public void AssertRealizedBelow(int maxChildren)
    {
        int max = RealizedTopLevelChildren.Max();
        Assert.True(max <= maxChildren, $"実体化トップレベル数 {max} > 上限 {maxChildren}（仮想化崩れの可能性）");
    }

    private static long Median(long[] values)
    {
        long[] sorted = (long[])values.Clone();
        Array.Sort(sorted);
        return sorted.Length == 0 ? 0 : sorted[sorted.Length / 2];
    }
}
