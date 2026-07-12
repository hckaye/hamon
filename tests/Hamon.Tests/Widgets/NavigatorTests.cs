using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for Navigator (push/pop, return result, focus trap to front route, return with pop).</summary>
public class NavigatorTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static Widget FocusBox(FocusNode node) => new Focus
    {
        Node = node,
        Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) },
    };

    [Fact]
    public void Push_Pop_TracksCountAndCanPop()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var nav = new NavigatorController(host, () => new SizedBox());
        host.SetRoot(() => new Navigator { Controller = nav });
        host.Update(new Size(200, 200));

        Assert.Equal(1, nav.Count);
        Assert.False(nav.CanPop);

        nav.Push(() => new SizedBox());
        host.Update(new Size(200, 200));
        Assert.Equal(2, nav.Count);
        Assert.True(nav.CanPop);

        Assert.True(nav.Pop());
        host.Update(new Size(200, 200));
        Assert.Equal(1, nav.Count);
        Assert.False(nav.CanPop);
    }

    [Fact]
    public void Pop_Home_IsRefused()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var nav = new NavigatorController(host, () => new SizedBox());
        host.SetRoot(() => new Navigator { Controller = nav });
        host.Update(new Size(200, 200));

        Assert.False(nav.Pop()); // home は降ろせない
        Assert.Equal(1, nav.Count);
    }

    [Fact]
    public void Pop_ReturnsResultToPusher()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var nav = new NavigatorController(host, () => new SizedBox());
        host.SetRoot(() => new Navigator { Controller = nav });
        host.Update(new Size(200, 200));

        object? received = null;
        nav.Push(() => new SizedBox(), result => received = result);
        host.Update(new Size(200, 200));

        Assert.True(nav.Pop("ok"));
        Assert.Equal("ok", received);
    }

    [Fact]
    public void Focus_TrappedToTopRoute_AndReturnsOnPop()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var home = new FocusNode();
        var pushed = new FocusNode();

        var nav = new NavigatorController(host, () => FocusBox(home));
        host.SetRoot(() => new Navigator { Controller = nav });
        host.Update(new Size(200, 200));

        // home ルートのフォーカスへ引き込み（最前面＝home）
        Assert.Same(home, host.Focus.Focused);

        // 新ルートを push → フォーカスは新ルートへ移る（トラップ）
        nav.Push(() => FocusBox(pushed));
        host.Update(new Size(200, 200));
        Assert.Same(pushed, host.Focus.Focused);

        // 新ルートからは home へ方向移動できない（トラップ）
        Assert.False(host.MoveFocus(FocusDirection.Down));
        Assert.Same(pushed, host.Focus.Focused);

        // pop → フォーカスは home へ復帰
        Assert.True(nav.Pop());
        host.Update(new Size(200, 200));
        Assert.Same(home, host.Focus.Focused);
    }

    [Fact]
    public void TopRoute_PaintsOverLower_HitTestWins()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int homeTaps = 0;
        int topTaps = 0;

        var nav = new NavigatorController(host, () => new GestureDetector
        {
            OnTap = () => homeTaps++,
            Child = new SizedBox { Width = Dimension.Px(200), Height = Dimension.Px(200) },
        });
        host.SetRoot(() => new Navigator { Controller = nav });
        host.Update(new Size(200, 200));

        nav.Push(() => new GestureDetector
        {
            OnTap = () => topTaps++,
            Child = new SizedBox { Width = Dimension.Px(200), Height = Dimension.Px(200) },
        });
        host.Update(new Size(200, 200));

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up));

        Assert.Equal(0, homeTaps); // 下のルートには届かない
        Assert.Equal(1, topTaps);
    }
}
