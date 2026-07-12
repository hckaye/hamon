using Hamon.Layout;
using Hamon.Widgets;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Single child box (Container/Padding/SizedBox/Focus/GestureDetector=<see cref="LayoutKind.Box"/>)but
/// <b>Convey tight constraints to children</b>(Flutter compliant: Fill under Expanded/Stretch, Padding is conveyed by deflate) Deterministic test.
/// </summary>
public class BoxLayoutTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static Element Mount(Widget root)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => root);
        host.Update(new Size(300, 300));
        return host.Root!;
    }

    [Fact]
    public void Container_FillsWidth_UnderStretchColumn()
    {
        Element col = Mount(new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            Children = new Widget[] { new Container { Child = new SizedBox { Height = Dimension.Px(20f) } } },
        });

        Assert.Equal(300f, col.Children[0].LayoutNode.Bounds.Width, 0.5f); // Stretch の tight 幅が Container→子へ伝わる
    }

    [Fact]
    public void Container_SplitsWidth_UnderExpandedRow()
    {
        Element row = Mount(new Row
        {
            Children = new Widget[]
            {
                new Expanded { Child = new Container { Child = new SizedBox { Height = Dimension.Px(10f) } } },
                new Expanded { Child = new Container { Child = new SizedBox { Height = Dimension.Px(10f) } } },
            },
        });

        Assert.Equal(150f, row.Children[0].LayoutNode.Bounds.Width, 0.5f);
        Assert.Equal(150f, row.Children[1].LayoutNode.Bounds.Width, 0.5f);
    }

    [Fact]
    public void Padding_DeflatesTightToChild()
    {
        Element col = Mount(new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            Children = new Widget[] { new Padding { Insets = EdgeInsets.All(10f), Child = new SizedBox { Height = Dimension.Px(10f) } } },
        });

        Element padding = col.Children[0];
        Assert.Equal(300f, padding.LayoutNode.Bounds.Width, 0.5f);          // Padding は充填
        Assert.Equal(280f, padding.Children[0].LayoutNode.Bounds.Width, 0.5f); // 子は 300 - 左右 padding
    }

    [Fact]
    public void Row_DefaultCrossAlignment_CentersChildren()
    {
        // Flutter 既定＝CrossAxisAlignment.center。高さ100の Row の中で 20px の子は縦中央（Y=40）。
        Element col = Mount(new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new SizedBox
                {
                    Height = Dimension.Px(100f),
                    Child = new Row
                    {
                        Children = new Widget[] { new SizedBox { Width = Dimension.Px(20f), Height = Dimension.Px(20f) } },
                    },
                },
            },
        });

        Element row = col.Children[0].Children[0]; // SizedBox → Row
        Assert.Equal(40f, row.Children[0].LayoutNode.Bounds.Y, 0.5f);
    }

    [Fact]
    public void Center_FillsAndCentersChild()
    {
        Element center = Mount(new Center { Child = new SizedBox { Width = Dimension.Px(40f), Height = Dimension.Px(40f) } });
        Assert.Equal(300f, center.LayoutNode.Bounds.Width, 0.5f); // 充填
        Rect child = center.Children[0].LayoutNode.Bounds;
        Assert.Equal(130f, child.X, 0.5f); // (300-40)/2
        Assert.Equal(130f, child.Y, 0.5f);
    }

    [Fact]
    public void Align_PositionsChild()
    {
        Element align = Mount(new Align
        {
            Alignment = Alignment.CenterLeft,
            Child = new SizedBox { Width = Dimension.Px(40f), Height = Dimension.Px(40f) },
        });

        Rect child = align.Children[0].LayoutNode.Bounds;
        Assert.Equal(0f, child.X, 0.5f);   // 左寄せ
        Assert.Equal(130f, child.Y, 0.5f); // 縦中央
    }

    [Fact]
    public void ContainerAlignment_FillsSlot_AndCentersChild()
    {
        Element row = Mount(new Row
        {
            Children = new Widget[]
            {
                new Expanded { Child = new Container { Alignment = Alignment.Center, Child = new SizedBox { Width = Dimension.Px(40f), Height = Dimension.Px(40f) } } },
            },
        });

        Element container = row.Children[0].Children[0]; // Expanded(StatelessでなくRenderElement) → Container
        Assert.Equal(300f, container.LayoutNode.Bounds.Width, 0.5f); // スロット充填
        Assert.Equal(130f, container.Children[0].LayoutNode.Bounds.X, 0.5f); // 子は中央
    }

    [Fact]
    public void Button_FillsWidth_UnderStretchColumn()
    {
        Element col = Mount(new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            Children = new Widget[] { new Button { Child = new SizedBox { Height = Dimension.Px(10f) } } },
        });

        // Button は単一の ButtonElement（Box＝padding 内側・子は中央）。Stretch の tight 幅がそのまま充填する。
        Element button = col.Children[0]; // ButtonElement
        Assert.Equal(300f, button.LayoutNode.Bounds.Width, 0.5f);
    }
}
