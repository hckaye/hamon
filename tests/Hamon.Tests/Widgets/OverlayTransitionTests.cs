using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for overlay/modal entry/exit animation (keep subtree alive even during exit).</summary>
public class OverlayTransitionTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 200);

    private static HamonRoot Mount()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new SizedBox());
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Push_WithTransition_RendersOverlay()
    {
        HamonRoot host = Mount();
        host.PushOverlay(() => new SizedBox(), 0.2f);
        host.Update(Viewport);

        Assert.Equal(1, host.OverlayCount);
        Assert.Equal(1, host.OverlayRenderedCount);
    }

    [Fact]
    public void Remove_WithTransition_KeepsExitingMountedUntilComplete()
    {
        HamonRoot host = Mount();
        OverlayEntry entry = host.PushOverlay(() => new SizedBox(), 0.2f, curve: Curves.Linear);
        host.Update(Viewport);
        host.Update(Viewport, 0.2f); // 入場完了
        Assert.Equal(1, host.OverlayRenderedCount);

        host.RemoveOverlay(entry);

        // 論理数は即時に減るが、描画は退場アニメ完了まで残る。
        Assert.Equal(0, host.OverlayCount);
        Assert.Equal(1, host.OverlayRenderedCount);

        host.Update(Viewport, 0.1f); // 退場の途中
        Assert.Equal(1, host.OverlayRenderedCount);

        host.Update(Viewport, 0.2f); // 退場完了
        Assert.Equal(0, host.OverlayRenderedCount);
    }

    [Fact]
    public void Instant_NoTransition_RemovesImmediately()
    {
        HamonRoot host = Mount();
        OverlayEntry entry = host.PushOverlay(() => new SizedBox());
        host.Update(Viewport);
        Assert.Equal(1, host.OverlayRenderedCount);

        host.RemoveOverlay(entry);
        Assert.Equal(0, host.OverlayCount);
        Assert.Equal(0, host.OverlayRenderedCount); // 即時に消える
    }

    [Fact]
    public void RemoveTwice_IsNoOp()
    {
        HamonRoot host = Mount();
        OverlayEntry entry = host.PushOverlay(() => new SizedBox(), 0.2f, curve: Curves.Linear);
        host.Update(Viewport);
        host.Update(Viewport, 0.2f);

        host.RemoveOverlay(entry);
        host.RemoveOverlay(entry); // 二重クローズは無害（既に退場中）
        Assert.Equal(1, host.OverlayRenderedCount);

        host.Update(Viewport, 0.3f);
        Assert.Equal(0, host.OverlayRenderedCount);
    }
}
