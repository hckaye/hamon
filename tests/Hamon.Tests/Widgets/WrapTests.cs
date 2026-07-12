using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic layout test for Wrap (main axis wrapping flow/spacing/runSpacing).</summary>
public class WrapTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static (HamonRoot host, Element wrap) Mount(Wrap wrap, Size viewport)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = wrap });
        host.Update(viewport);
        return (host, host.Root!.Children[0]);
    }

    private static Widget Box(float w, float h) => new SizedBox { Width = Dimension.Px(w), Height = Dimension.Px(h) };

    [Fact]
    public void WrapsToNextLine_WhenMainAxisFull()
    {
        // 幅 100。各 40px の子3つ＋spacing 0 → 2 つで 80、3 つ目（120>100）で改行。
        (_, Element wrap) = Mount(
            new Wrap { Children = new[] { Box(40, 30), Box(40, 30), Box(40, 30) } },
            new Size(100, 200));

        Element c0 = wrap.Children[0];
        Element c1 = wrap.Children[1];
        Element c2 = wrap.Children[2];

        Assert.Equal(0f, c0.LayoutNode.Bounds.Y, 0.5f);
        Assert.Equal(0f, c1.LayoutNode.Bounds.Y, 0.5f);
        Assert.Equal(40f, c1.LayoutNode.Bounds.X, 0.5f); // 同一行
        Assert.Equal(30f, c2.LayoutNode.Bounds.Y, 0.5f); // 2 行目（前行高 30）
        Assert.Equal(0f, c2.LayoutNode.Bounds.X, 0.5f);
    }

    [Fact]
    public void Spacing_And_RunSpacing_Applied()
    {
        // 幅 100、spacing 10、runSpacing 5。子 40px：c0@0, c1@50（40+10）, c1 末端 90 ≤100。c2 は 90+10+40=140>100 → 改行。
        (_, Element wrap) = Mount(
            new Wrap { Spacing = 10f, RunSpacing = 5f, Children = new[] { Box(40, 30), Box(40, 30), Box(40, 30) } },
            new Size(100, 200));

        Assert.Equal(50f, wrap.Children[1].LayoutNode.Bounds.X, 0.5f); // 40 + spacing 10
        Assert.Equal(35f, wrap.Children[2].LayoutNode.Bounds.Y, 0.5f); // 行高 30 + runSpacing 5
    }

    [Fact]
    public void Vertical_WrapsAcrossColumns()
    {
        // 縦方向 Wrap・高さ 100。各 40px 高の子3つ → 2 つで 80、3 つ目で次の列へ。
        (_, Element wrap) = Mount(
            new Wrap { Direction = Axis.Vertical, Children = new[] { Box(30, 40), Box(30, 40), Box(30, 40) } },
            new Size(200, 100));

        Assert.Equal(40f, wrap.Children[1].LayoutNode.Bounds.Y, 0.5f); // 同一列の下
        Assert.Equal(30f, wrap.Children[2].LayoutNode.Bounds.X, 0.5f); // 次の列（前列幅 30）
        Assert.Equal(0f, wrap.Children[2].LayoutNode.Bounds.Y, 0.5f);
    }
}
