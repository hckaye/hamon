using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test of composite modal (ShowDialog/ShowBottomSheet = standard UI on top of transition base).</summary>
public class ModalTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(400, 400);

    private static HamonRoot Mount()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new SizedBox());
        host.Update(Viewport);
        return host;
    }

    private static Widget Card(FocusNode? node = null) => new Container
    {
        Width = Dimension.Px(200),
        Height = Dimension.Px(100),
        Color = new Color(40, 44, 52),
        Child = node is null ? new SizedBox() : new Button { Node = node, Autofocus = true, Child = new SizedBox() },
    };

    [Fact]
    public void ShowDialog_Opens_And_CloseDismisses()
    {
        HamonRoot host = Mount();
        Action? close = null;
        host.ShowDialog(c =>
        {
            close = c;
            return Card();
        });
        host.Update(Viewport);
        Assert.Equal(1, host.OverlayCount);

        host.Update(Viewport, 0.2f); // 入場完了
        close!();

        Assert.Equal(0, host.OverlayCount);          // 論理は即時
        Assert.Equal(1, host.OverlayRenderedCount);  // 退場アニメ中は描画継続
        host.Update(Viewport, 0.3f);
        Assert.Equal(0, host.OverlayRenderedCount);  // 退場完了で消える
    }

    [Fact]
    public void ShowDialog_TrapsFocusToContent()
    {
        HamonRoot host = Mount();
        var node = new FocusNode();
        host.ShowDialog(_ => Card(node));
        host.Update(Viewport);

        Assert.True(node.HasFocus); // FocusScope＋autofocus でダイアログ内へ
    }

    [Fact]
    public void ShowDialog_BarrierTap_Dismisses()
    {
        HamonRoot host = Mount();
        host.ShowDialog(_ => Card(), barrierDismissible: true);
        host.Update(Viewport);
        host.Update(Viewport, 0.2f); // 入場完了

        // カードは中央（約 x100..300, y150..250）。隅をタップ＝スクリム→閉じる。
        host.DispatchPointer(new PointerEvent(new Vec2(5, 5), PointerPhase.Down, 0.21f));
        host.DispatchPointer(new PointerEvent(new Vec2(5, 5), PointerPhase.Up, 0.22f));

        Assert.Equal(0, host.OverlayCount);
        host.Update(Viewport, 0.3f);
        Assert.Equal(0, host.OverlayRenderedCount);
    }

    [Fact]
    public void ShowDialog_NotDismissible_BarrierTapKeepsOpen()
    {
        HamonRoot host = Mount();
        host.ShowDialog(_ => Card(), barrierDismissible: false);
        host.Update(Viewport);
        host.Update(Viewport, 0.2f);

        host.DispatchPointer(new PointerEvent(new Vec2(5, 5), PointerPhase.Down, 0.21f));
        host.DispatchPointer(new PointerEvent(new Vec2(5, 5), PointerPhase.Up, 0.22f));

        Assert.Equal(1, host.OverlayCount); // スクリムは吸収するが閉じない
    }

    [Fact]
    public void ShowBottomSheet_Opens_And_CloseDismisses()
    {
        HamonRoot host = Mount();
        Action? close = null;
        host.ShowBottomSheet(
            c =>
            {
                close = c;
                return new Container { Color = new Color(30, 34, 40), Child = new SizedBox() };
            },
            height: 160f);
        host.Update(Viewport);
        Assert.Equal(1, host.OverlayCount);

        host.Update(Viewport, 0.25f); // 入場完了
        close!();
        Assert.Equal(0, host.OverlayCount);
        Assert.Equal(1, host.OverlayRenderedCount);

        host.Update(Viewport, 0.3f);
        Assert.Equal(0, host.OverlayRenderedCount);
    }

    [Fact]
    public void ShowDrawer_Opens_TrapsFocus_AndCloses()
    {
        HamonRoot host = Mount();
        var node = new FocusNode();
        Action? close = null;
        host.ShowDrawer(
            c =>
            {
                close = c;
                return new Container { Color = new Color(30, 34, 40), Child = new Button { Node = node, Autofocus = true, Child = new SizedBox() } };
            },
            width: 240f);
        host.Update(Viewport);

        Assert.Equal(1, host.OverlayCount);
        Assert.True(node.HasFocus); // ドロワー内へフォーカストラップ

        host.Update(Viewport, 0.25f); // 入場完了
        close!();
        Assert.Equal(0, host.OverlayCount);
        host.Update(Viewport, 0.3f);
        Assert.Equal(0, host.OverlayRenderedCount);
    }

    [Fact]
    public void ShowDrawer_FromRight_Opens()
    {
        HamonRoot host = Mount();
        host.ShowDrawer(_ => new Container { Color = new Color(30, 34, 40), Child = new SizedBox() }, width: 200f, fromRight: true);
        host.Update(Viewport);
        Assert.Equal(1, host.OverlayCount);
        Assert.Equal(1, host.OverlayRenderedCount);
    }
}
