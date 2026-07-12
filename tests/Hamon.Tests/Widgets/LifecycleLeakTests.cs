using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Detection of resource leaks during long-term operation: Ensure that tickers (anime), atom subscriptions, and overlays are released when unmounted.
/// </summary>
public class LifecycleLeakTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    [Fact]
    public void Switch_AnimatingThenUnmounted_DoesNotLeakTicker()
    {
        var host = new HamonRoot(new StubTextRenderer());
        bool show = true;
        bool value = false;
        host.SetRoot(() => show
            ? new Switch { Value = value, OnChanged = _ => { } }
            : new SizedBox());
        host.Update(Viewport);

        int baseline = host.ActiveTickerCount;

        // 値を変えてノブアニメ開始（ティッカー登録）。
        value = true;
        host.MarkDirty();
        host.Update(Viewport, 0.01f);
        Assert.True(host.ActiveTickerCount > baseline, "knob animation should register a ticker");

        // アニメ途中で取り外す。
        show = false;
        host.MarkDirty();
        host.Update(Viewport, 0.01f);
        Assert.Equal(baseline, host.ActiveTickerCount); // リークしない
    }

    [Fact]
    public void TextField_Unmounted_StopsBlinkTicker()
    {
        var host = new HamonRoot(new StubTextRenderer());
        bool show = true;
        var ctrl = new TextEditingController();
        host.SetRoot(() => show
            ? new TextField { Controller = ctrl, Node = new FocusNode(), Autofocus = true }
            : new SizedBox());
        host.Update(Viewport);
        host.Update(Viewport, 0.01f);
        int withField = host.ActiveTickerCount;
        Assert.True(withField > 0); // 点滅ティッカー

        show = false;
        host.MarkDirty();
        host.Update(Viewport, 0.01f);
        Assert.Equal(0, host.ActiveTickerCount);
    }

    [Fact]
    public void HookAtomSubscription_RemovedOnUnmount()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var atom = new Atom<int>(0);
        bool show = true;
        host.SetRoot(() => show
            ? new WatchWidget(atom)
            : new SizedBox());
        host.Update(Viewport);

        AtomCell cell = host.GlobalStore.Cell(atom);
        Assert.True(cell.ListenerCount >= 1); // 購読中

        show = false;
        host.MarkDirty();
        host.Update(Viewport);
        Assert.Equal(0, cell.ListenerCount); // 解除された
    }

    [Fact]
    public void Overlays_PushedThenRemoved_LeaveNoResidue()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new SizedBox());
        host.Update(Viewport);

        var entries = new OverlayEntry[5];
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = host.PushOverlay(() => new SizedBox());
        }

        host.Update(Viewport);
        Assert.Equal(5, host.OverlayCount);

        foreach (OverlayEntry e in entries)
        {
            host.RemoveOverlay(e);
        }

        host.Update(Viewport);
        Assert.Equal(0, host.OverlayCount);
        Assert.Equal(0, host.OverlayRenderedCount); // 退場アニメ残骸も無し（即時removal）
    }

    // atom を購読する最小フックウィジェット。
    private sealed class WatchWidget : HookWidget
    {
        private readonly Atom<int> _atom;

        public WatchWidget(Atom<int> atom) => _atom = atom;

        public override Widget Build(BuildContext context, Hooks hooks)
        {
            (int v, _) = hooks.UseAtom(_atom);
            return new SizedBox { Width = Dimension.Px(v + 1), Height = Dimension.Px(10) };
        }
    }
}
