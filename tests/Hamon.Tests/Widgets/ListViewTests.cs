using Hamon.Layout;
using Hamon.Widgets;
using System.Linq;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test of virtualized ListView (materialization of visible only, window movement, cell reuse, extent cache, infinite scrolling).</summary>
public class ListViewTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static (HamonRoot host, ListViewElement list) MountFixed(int count, ScrollController? controller = null, Action? onEnd = null)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = count,
            ItemExtent = 30f,
            Controller = controller,
            OnEndReached = onEnd,
            Builder = i => new SizedBox { Height = Dimension.Px(30) },
        });
        host.Update(new Size(200, 100)); // viewport 高 100 → 可視 ~4 行
        return (host, (ListViewElement)host.Root!);
    }

    [Fact]
    public void RealizesOnlyVisible_NotAllItems()
    {
        (HamonRoot host, ListViewElement list) = MountFixed(10_000);

        // 1万件でも実体化は数件のみ
        Assert.True(list.ActiveIndices.Count <= 6, $"realized={list.ActiveIndices.Count}");
        Assert.Contains(0, list.ActiveIndices);
        Assert.DoesNotContain(9_999, list.ActiveIndices);
    }

    [Fact]
    public void FixedExtent_FirstItemAtViewportTop()
    {
        var controller = new ScrollController();
        (HamonRoot host, ListViewElement list) = MountFixed(1000, controller);

        Assert.Equal(0f, list.Children[0].LayoutNode.Bounds.Y);

        controller.JumpTo(300f); // ちょうど 10 行ぶん
        host.Update(new Size(200, 100));

        // index 10 が先頭・viewport 上端に揃う
        Assert.Contains(10, list.ActiveIndices);
        Assert.DoesNotContain(0, list.ActiveIndices);
        Assert.Equal(0f, list.ActiveElement(10)!.LayoutNode.Bounds.Y);
    }

    [Fact]
    public void Scrolling_ShiftsWindow()
    {
        var controller = new ScrollController();
        (HamonRoot host, ListViewElement list) = MountFixed(1000, controller);

        int[] before = list.ActiveIndices.OrderBy(i => i).ToArray();
        Assert.Equal(0, before[0]);

        controller.JumpTo(600f);
        host.Update(new Size(200, 100));

        int[] after = list.ActiveIndices.OrderBy(i => i).ToArray();
        Assert.Equal(20, after[0]); // 600/30 = 20
        Assert.True(after.All(i => i >= 20));
    }

    [Fact]
    public void CellReuse_SameElementForStillVisibleIndex()
    {
        var controller = new ScrollController();
        (HamonRoot host, ListViewElement list) = MountFixed(1000, controller);

        Element? before = list.ActiveElement(2);
        Assert.NotNull(before);

        controller.JumpTo(30f); // 1 行ぶんスクロール → index 2 はまだ可視
        host.Update(new Size(200, 100));

        Element? after = list.ActiveElement(2);
        Assert.Same(before, after); // 同一実体を再利用（作り直さない）
    }

    [Fact]
    public void VariableExtent_CachesMeasuredAndStacks()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = 100,
            EstimatedExtent = 20f,
            Builder = i => new SizedBox { Height = Dimension.Px(10 + (i * 10)) }, // 行0=10, 行1=20, 行2=30...
        });
        host.Update(new Size(200, 100));
        var list = (ListViewElement)host.Root!;

        // 行0(10),行1(20),行2(30),行3(40)=計100 でちょうど埋まる
        Assert.Equal(0f, list.ActiveElement(0)!.LayoutNode.Bounds.Y);
        Assert.Equal(10f, list.ActiveElement(1)!.LayoutNode.Bounds.Y);
        Assert.Equal(30f, list.ActiveElement(2)!.LayoutNode.Bounds.Y); // 10+20
        Assert.Equal(60f, list.ActiveElement(3)!.LayoutNode.Bounds.Y); // 10+20+30
    }

    [Fact]
    public void OnEndReached_FiresOnceAtEnd()
    {
        int fired = 0;
        var controller = new ScrollController();
        (HamonRoot host, ListViewElement list) = MountFixed(5, controller, onEnd: () => fired++);

        // 5 行 * 30 = 150 > viewport 100。末尾までスクロール
        controller.JumpTo(9999f);
        host.Update(new Size(200, 100));
        Assert.Equal(1, fired);

        // 同じ件数のままなら再発火しない
        host.Update(new Size(200, 100));
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Empty_NoChildrenNoCrash()
    {
        (HamonRoot host, ListViewElement list) = MountFixed(0);
        Assert.Empty(list.Children);
        Assert.Equal(0f, list.ScrollOffset);
    }
}
