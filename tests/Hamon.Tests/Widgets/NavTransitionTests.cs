using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for route transition animation (push/pop/use subtrees even during exit).</summary>
public class NavTransitionTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 200);

    private static (HamonRoot host, NavigatorController nav) Mount(float duration)
    {
        var host = new HamonRoot(new StubTextRenderer());
        var nav = new NavigatorController(host, () => new SizedBox());
        nav.TransitionDuration = duration;
        nav.Curve = Curves.Linear;
        host.SetRoot(() => new Navigator { Controller = nav });
        host.Update(Viewport);
        return (host, nav);
    }

    [Fact]
    public void PushedRoute_AbsorbsPointer_NotPassingToBackground()
    {
        int homeTaps = 0;
        var host = new HamonRoot(new StubTextRenderer());
        var nav = new NavigatorController(host, () => new Button
        {
            Node = new FocusNode(),
            OnPressed = () => homeTaps++,
            Child = new SizedBox { Width = Dimension.Px(100f), Height = Dimension.Px(100f) },
        })
        { TransitionDuration = 0f };
        host.SetRoot(() => new Navigator { Controller = nav });
        host.Update(Viewport);

        nav.Push(() => new SizedBox()); // 中身が空のルートを push（全画面バリアが吸収するはず）
        host.Update(Viewport);

        // 背面ホームのボタン位置(10,10)をタップ → 前面ルートが吸収し背面へ貫通しない。
        host.DispatchPointer(new PointerEvent(new Vec2(10f, 10f), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(10f, 10f), PointerPhase.Up, 0.02f));
        Assert.Equal(0, homeTaps);
    }

    [Fact]
    public void Push_WithTransition_RendersBothRoutes()
    {
        (HamonRoot host, NavigatorController nav) = Mount(0.2f);

        nav.Push(() => new SizedBox());
        host.Update(Viewport);

        Assert.Equal(2, nav.Count);
        Assert.Equal(2, nav.RenderedCount);
    }

    [Fact]
    public void Pop_WithTransition_KeepsExitingMountedUntilComplete()
    {
        (HamonRoot host, NavigatorController nav) = Mount(0.2f);
        nav.Push(() => new SizedBox());
        host.Update(Viewport);
        host.Update(Viewport, 0.2f); // 入場アニメ完了（進捗1）
        Assert.Equal(2, nav.RenderedCount);

        bool popped = nav.Pop();
        Assert.True(popped);

        // 論理スタックは即時に減るが、描画は退場アニメ完了まで残る。
        Assert.Equal(1, nav.Count);
        Assert.False(nav.CanPop);
        Assert.Equal(2, nav.RenderedCount);

        host.Update(Viewport, 0.1f); // 退場アニメの途中
        Assert.Equal(2, nav.RenderedCount);

        host.Update(Viewport, 0.2f); // 退場アニメ完了
        Assert.Equal(1, nav.RenderedCount);
    }

    [Fact]
    public void Pop_ReturnsResultImmediately_EvenWithTransition()
    {
        (HamonRoot host, NavigatorController nav) = Mount(0.2f);
        object? result = null;
        nav.Push(() => new SizedBox(), r => result = r);
        host.Update(Viewport);

        nav.Pop("ok"); // 結果はアニメを待たず即時に返る
        Assert.Equal("ok", result);
    }

    [Fact]
    public void Instant_NoTransition_RemovesImmediately()
    {
        (HamonRoot host, NavigatorController nav) = Mount(0f); // アニメ無効
        nav.Push(() => new SizedBox());
        host.Update(Viewport);
        Assert.Equal(2, nav.RenderedCount);

        nav.Pop();
        Assert.Equal(1, nav.RenderedCount); // 即時に消える
    }
}
