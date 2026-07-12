using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Layout determinism test for Image/NineSlice (confirmed with Sandbox PNG since drawing is GPU dependent).</summary>
public class ImageTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static Element MountChild0(Widget child)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Column { CrossAxisAlignment = CrossAxisAlignment.Start, Children = new[] { child } });
        host.Update(new Size(300, 300));
        return host.Root!.Children[0];
    }

    [Fact]
    public void Image_UsesExplicitSize()
    {
        Element img = MountChild0(new Image { Width = Dimension.Px(40), Height = Dimension.Px(40) });
        Assert.Equal(40f, img.LayoutNode.Bounds.Width, 0.5f);
        Assert.Equal(40f, img.LayoutNode.Bounds.Height, 0.5f);
    }

    [Fact]
    public void Image_NullTexture_NoCrashZeroSize()
    {
        Element img = MountChild0(new Image()); // テクスチャ無し＝原寸0
        Assert.Equal(0f, img.LayoutNode.Bounds.Width, 0.5f);
    }

    [Fact]
    public void NineSlice_InsetsChildByBorder()
    {
        Element ns = MountChild0(new NineSlice
        {
            Border = EdgeInsets.All(10f),
            Width = Dimension.Px(100),
            Height = Dimension.Px(80),
            Child = new SizedBox { Width = Dimension.Px(80), Height = Dimension.Px(60) },
        });

        Assert.Equal(100f, ns.LayoutNode.Bounds.Width, 0.5f);
        Rect child = ns.Children[0].LayoutNode.Bounds;
        Assert.Equal(10f, child.X, 0.5f); // 枠 Border の内側へ
        Assert.Equal(10f, child.Y, 0.5f);
        Assert.Equal(80f, child.Width, 0.5f);
    }
}
