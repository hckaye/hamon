using Hamon.Layout;
using Hamon.Widgets;
using System.Diagnostics;

namespace Hamon.Testing.Perf;

/// <summary>
/// Drive the widget/screen multi-frame,<b>By frame</b>Collect the allocation amount, number of draw calls, and number of materializations.
/// The drawing is<see cref="CountingPainter"/>(Records only the number of calls without actually drawing) = MonoGame independent.
/// <see cref="InputScript"/>emulate via.
/// </summary>
public static class PerfHarness
{
    public static PerfReport Measure(
        Func<Widget> build,
        Size size,
        Action<HamonRoot, int>? perFrame = null,
        int frames = 60,
        int warmup = 12,
        float dt = 0.016f)
    {
        var painter = new CountingPainter();
        var text = new CountingTextRenderer();
        var host = new HamonRoot(text);
        host.SetRoot(build);

        // ウォームアップで reconcile/layout を済ませ定常状態にする（入力は与えない）。
        for (int i = 0; i < warmup; i++)
        {
            host.Update(size, dt);
            host.Render(painter);
        }

        var alloc = new long[frames];
        var draws = new int[frames];
        var realized = new int[frames];

        var sw = Stopwatch.StartNew();
        for (int f = 0; f < frames; f++)
        {
            painter.Reset();
            text.Reset();
            long before = GC.GetAllocatedBytesForCurrentThread();
            perFrame?.Invoke(host, f); // 操作のエミュレート（その操作のアロケも計測窓に含める）
            host.Update(size, dt);
            host.Render(painter);
            long after = GC.GetAllocatedBytesForCurrentThread();

            alloc[f] = after - before;
            draws[f] = painter.TotalDrawCalls + text.DrawCalls; // 塗り/描画＋テキスト＝可視要素あたりの仕事量
            realized[f] = host.Root?.Children.Count ?? 0;
        }

        sw.Stop();
        return new PerfReport(alloc, draws, realized, sw.Elapsed.TotalMilliseconds);
    }
}
