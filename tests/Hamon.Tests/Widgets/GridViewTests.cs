using Hamon.Layout;
using Hamon.Widgets;
using System.Linq;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for virtualized GridView (row-by-row virtualization, equal column division, cell reuse, infinite scrolling).</summary>
public class GridViewTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    // viewport 200x100、4列・主軸長 50px → 1行=50px、可視 ~2行（=8セル＋境界1行で最大12）。
    private static (HamonRoot host, GridViewElement grid) MountFixed(int count, ScrollController? controller = null, Action? onEnd = null)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GridView
        {
            ItemCount = count,
            CrossAxisCount = 4,
            MainAxisExtent = 50f,
            Controller = controller,
            OnEndReached = onEnd,
            Builder = i => new SizedBox(),
        });
        host.Update(new Size(200, 100));
        return (host, (GridViewElement)host.Root!);
    }

    [Fact]
    public void RealizesOnlyVisibleRows_NotAllItems()
    {
        (HamonRoot host, GridViewElement grid) = MountFixed(10_000);

        // 1万件でも実体化は可視行ぶんのみ（4列 * ~3行 = 12 以下）。
        Assert.True(grid.ActiveIndices.Count <= 12, $"realized={grid.ActiveIndices.Count}");
        Assert.Contains(0, grid.ActiveIndices);
        Assert.Contains(3, grid.ActiveIndices); // 先頭行の最終列
        Assert.DoesNotContain(9_999, grid.ActiveIndices);
    }

    [Fact]
    public void FixedExtent_FirstRowCellsLaidAcrossCrossAxis()
    {
        (HamonRoot host, GridViewElement grid) = MountFixed(100);

        // 200 幅 / 4 列 = 50px セル、spacing 0。列 0..3 の X が 0,50,100,150。
        Assert.Equal(0f, grid.ActiveElement(0)!.LayoutNode.Bounds.X);
        Assert.Equal(50f, grid.ActiveElement(1)!.LayoutNode.Bounds.X);
        Assert.Equal(100f, grid.ActiveElement(2)!.LayoutNode.Bounds.X);
        Assert.Equal(150f, grid.ActiveElement(3)!.LayoutNode.Bounds.X);

        // 行0は Y=0、行1（index 4）は Y=50。
        Assert.Equal(0f, grid.ActiveElement(0)!.LayoutNode.Bounds.Y);
        Assert.Equal(50f, grid.ActiveElement(4)!.LayoutNode.Bounds.Y);
    }

    [Fact]
    public void Scrolling_ShiftsRowWindow()
    {
        var controller = new ScrollController();
        (HamonRoot host, GridViewElement grid) = MountFixed(1000, controller);

        Assert.Contains(0, grid.ActiveIndices);

        controller.JumpTo(500f); // 10 行ぶん（50px * 10）
        host.Update(new Size(200, 100));

        // 先頭行が 10 → index 40..43。0..3 はもう可視外。
        Assert.Contains(40, grid.ActiveIndices);
        Assert.DoesNotContain(0, grid.ActiveIndices);
        Assert.True(grid.ActiveIndices.All(i => i >= 40));
        Assert.Equal(0f, grid.ActiveElement(40)!.LayoutNode.Bounds.Y); // viewport 上端に揃う
    }

    [Fact]
    public void CellReuse_SameElementForStillVisibleIndex()
    {
        var controller = new ScrollController();
        (HamonRoot host, GridViewElement grid) = MountFixed(1000, controller);

        Element? before = grid.ActiveElement(4);
        Assert.NotNull(before);

        controller.JumpTo(50f); // 1 行ぶん。index 4 はまだ可視
        host.Update(new Size(200, 100));

        Assert.Same(before, grid.ActiveElement(4));
    }

    [Fact]
    public void Spacing_AddsGapsBetweenCells()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GridView
        {
            ItemCount = 50,
            CrossAxisCount = 2,
            MainAxisExtent = 40f,
            CrossAxisSpacing = 10f,
            MainAxisSpacing = 6f,
            Builder = i => new SizedBox(),
        });
        host.Update(new Size(210, 100));
        var grid = (GridViewElement)host.Root!;

        // 2 列、cross spacing 10 → セル幅 = (210-10)/2 = 100。列0 X=0、列1 X=110。
        Assert.Equal(0f, grid.ActiveElement(0)!.LayoutNode.Bounds.X);
        Assert.Equal(110f, grid.ActiveElement(1)!.LayoutNode.Bounds.X);
        Assert.Equal(100f, grid.ActiveElement(0)!.LayoutNode.Bounds.Width);

        // 行1（index 2）は Y = 40 + 6（main spacing）= 46。
        Assert.Equal(46f, grid.ActiveElement(2)!.LayoutNode.Bounds.Y);
    }

    [Fact]
    public void AspectRatio_DerivesMainExtentFromCross()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GridView
        {
            ItemCount = 50,
            CrossAxisCount = 4,
            ChildAspectRatio = 2f, // cross/main=2 → 主軸長=cross/2
            Builder = i => new SizedBox(),
        });
        host.Update(new Size(200, 100));
        var grid = (GridViewElement)host.Root!;

        // cross = 200/4 = 50、aspect 2 → main = 25。行1（index 4）の Y=25。
        Assert.Equal(50f, grid.ActiveElement(0)!.LayoutNode.Bounds.Width);
        Assert.Equal(25f, grid.ActiveElement(0)!.LayoutNode.Bounds.Height);
        Assert.Equal(25f, grid.ActiveElement(4)!.LayoutNode.Bounds.Y);
    }

    [Fact]
    public void OnEndReached_FiresOnceAtEnd()
    {
        int fired = 0;
        var controller = new ScrollController();
        (HamonRoot host, GridViewElement grid) = MountFixed(20, controller, onEnd: () => fired++);

        // 20 件 / 4 列 = 5 行 * 50 = 250 > viewport 100。初回は末尾不可視。
        Assert.Equal(0, fired);

        controller.JumpTo(9999f); // 末尾までスクロール
        host.Update(new Size(200, 100));
        Assert.Equal(1, fired);

        host.Update(new Size(200, 100));
        Assert.Equal(1, fired); // 件数変わらず再発火しない
    }

    [Fact]
    public void Empty_NoChildrenNoCrash()
    {
        (HamonRoot host, GridViewElement grid) = MountFixed(0);
        Assert.Empty(grid.Children);
        Assert.Equal(0f, grid.ScrollOffset);
    }
}
