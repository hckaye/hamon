using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of large scale, variable height, OnEndReached (duplication prevention/prefetch), and stress of ListView/GridView.</summary>
public class ListViewPerfTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 100);

    [Fact]
    public void VariableHeight_10k_RealizesOnlyVisible_AndScrolls()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = 10000,
            EstimatedExtent = 30f, // 可変高（ItemExtent なし）
            Builder = i => new SizedBox { Height = Dimension.Px(i % 2 == 0 ? 20f : 40f) },
        });
        host.Update(Viewport);
        var list = (ListViewElement)host.Root!;

        Assert.True(list.ActiveIndices.Count < 50, $"realized={list.ActiveIndices.Count}");

        // 奥へスクロールしても破綻せず可視のみ実体化（O(log n) offset）。
        ((IScrollable)list).SetScroll(100000f);
        host.Update(Viewport);
        Assert.True(list.ActiveIndices.Count < 50);
        Assert.True(list.ScrollOffset > 0f);
    }

    [Fact]
    public void OnEndReached_FiresOnce_PerCount()
    {
        int fires = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = 20,
            ItemExtent = 40f,
            OnEndReached = () => fires++,
            Builder = i => new SizedBox { Height = Dimension.Px(40) },
        });
        host.Update(Viewport);
        var list = (ListViewElement)host.Root!;

        ((IScrollable)list).SetScroll(10000f); // 末尾へ
        host.Update(Viewport);
        Assert.Equal(1, fires);

        // 同じ末尾で再レイアウトしても再発火しない。
        host.MarkDirty();
        host.Update(Viewport);
        host.MarkDirty();
        host.Update(Viewport);
        Assert.Equal(1, fires);
    }

    [Fact]
    public void OnEndReached_Threshold_FiresBeforeLastItem()
    {
        int withThreshold = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = 20,
            ItemExtent = 40f,
            EndReachedThreshold = 5,
            OnEndReached = () => withThreshold++,
            Builder = i => new SizedBox { Height = Dimension.Px(40) },
        });
        host.Update(Viewport);
        var list = (ListViewElement)host.Root!;

        // items 13..15 を可視に（last=15）。threshold 5 ＝ last>=14 で発火するはず。
        ((IScrollable)list).SetScroll(520f);
        host.Update(Viewport);
        Assert.Equal(1, withThreshold);

        // 比較：閾値なしなら同じ位置では未発火（last>=19 が必要）。
        int noThreshold = 0;
        var host2 = new HamonRoot(new StubTextRenderer());
        host2.SetRoot(() => new ListView
        {
            ItemCount = 20,
            ItemExtent = 40f,
            OnEndReached = () => noThreshold++,
            Builder = i => new SizedBox { Height = Dimension.Px(40) },
        });
        host2.Update(Viewport);
        ((IScrollable)host2.Root!).SetScroll(520f);
        host2.Update(Viewport);
        Assert.Equal(0, noThreshold);
    }

    [Fact]
    public void GridView_Stress_ScrollThrough_StaysBounded()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GridView
        {
            ItemCount = 10000,
            CrossAxisCount = 5,
            MainAxisExtent = 50f,
            Builder = i => new SizedBox { Height = Dimension.Px(50) },
        });
        host.Update(Viewport);
        var grid = (GridViewElement)host.Root!;

        // 多数のスクロール位置を舐めても安定（クラッシュなし・実体化セルが有界）。
        for (int step = 0; step < 50; step++)
        {
            ((IScrollable)grid).SetScroll(step * 1000f);
            host.Update(Viewport);
            Assert.True(grid.Children.Count < 100, $"realized={grid.Children.Count} at step {step}");
        }
    }
}
