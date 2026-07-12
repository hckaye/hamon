using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>AspectRatio / SafeArea layout determinism test.</summary>
public class LayoutWidgetsTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static HamonRoot Mount(Widget root, Size viewport)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => root);
        host.Update(viewport);
        return host;
    }

    [Fact]
    public void AspectRatio_FitsWidth_ComputesHeight()
    {
        // 幅 200 制約 → 16:9 で高さ 112.5。Align で loose を与えると自然サイズに。
        HamonRoot host = Mount(
            new Align { Alignment = Alignment.TopLeft, Child = new SizedBox { Width = Dimension.Px(200), Child = new AspectRatio { Ratio = 16f / 9f, Child = new SizedBox() } } },
            new Size(400, 400));

        // Align(child=SizedBox width=200) → AspectRatio gets width 200, height = 200*9/16 = 112.5
        Element sizedBox = host.Root!.Children[0]; // Align の子 SizedBox
        Element aspect = sizedBox.Children[0];
        Rect b = aspect.LayoutNode.Bounds;
        Assert.Equal(200f, b.Width, 0.5f);
        Assert.Equal(112.5f, b.Height, 0.5f);
    }

    [Fact]
    public void AspectRatio_ClampsToHeight_WhenTooTall()
    {
        // 高さを 100 に固定（幅は loose=0..400）。1:1 なら幅 400 起点→高さ 400>100 で頭打ち→高さ100・幅100。
        HamonRoot host = Mount(
            new Align
            {
                Alignment = Alignment.TopLeft,
                Child = new SizedBox
                {
                    Height = Dimension.Px(100),
                    Child = new AspectRatio { Ratio = 1f, Child = new SizedBox() },
                },
            },
            new Size(400, 400));

        Element sizedBox = host.Root!.Children[0];
        Element aspect = sizedBox.Children[0];
        Rect b = aspect.LayoutNode.Bounds;
        Assert.Equal(100f, b.Height, 0.5f);
        Assert.Equal(100f, b.Width, 0.5f); // 高さに合わせて幅も 100（1:1）
    }

    [Fact]
    public void SafeArea_AppliesInsetsAsPadding()
    {
        var host = new HamonRoot(new StubTextRenderer())
        {
            SafeAreaInsets = new EdgeInsets(10f, 20f, 10f, 30f),
        };
        host.SetRoot(() => new SafeArea { Child = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) } });
        host.Update(new Size(300, 300));

        // SafeArea(Padding) の子（SizedBox）は left=10, top=20 オフセットされる。
        Element padding = host.Root!.Children[0]; // SafeArea(StatelessElement)→Padding... Root は SafeArea の子
        // SafeArea は StatelessWidget→Padding。Root.Children[0] が Padding の子 SizedBox。
        Rect inner = host.Root!.LayoutNode.Bounds;
        // Padding ノードの子オフセットを確認：子 SizedBox の絶対位置 = (10, 20)。
        Element box = FindLeaf(host.Root!);
        Rect bb = box.LayoutNode.Bounds;
        Assert.Equal(10f, bb.X, 0.5f);
        Assert.Equal(20f, bb.Y, 0.5f);
    }

    [Fact]
    public void SafeArea_RespectsMinimum_AndDisabledEdges()
    {
        var host = new HamonRoot(new StubTextRenderer())
        {
            SafeAreaInsets = new EdgeInsets(40f, 40f, 40f, 40f),
        };
        host.SetRoot(() => new SafeArea
        {
            Top = false, // 上辺はインセットを無視
            Minimum = EdgeInsets.All(5f),
            Child = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) },
        });
        host.Update(new Size(300, 300));

        Element box = FindLeaf(host.Root!);
        Rect bb = box.LayoutNode.Bounds;
        Assert.Equal(40f, bb.X, 0.5f); // left インセット適用
        Assert.Equal(5f, bb.Y, 0.5f);  // top は無効化＝Minimum の 5
    }

    private static Element FindLeaf(Element e)
    {
        while (e.Children.Count > 0)
        {
            e = e.Children[0];
        }

        return e;
    }
}
