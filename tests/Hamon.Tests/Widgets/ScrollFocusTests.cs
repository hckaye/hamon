using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test for scroll-to-focus (the belonging scroll follows when the focus moves).</summary>
public class ScrollFocusTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    [Fact]
    public void MovingFocusDown_ScrollsToKeepFocusVisible()
    {
        var nodes = new FocusNode[8];
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = new FocusNode();
        }

        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ScrollView
        {
            Child = new Column
            {
                Children = BuildButtons(nodes),
            },
        });

        var viewport = new Size(200, 100); // 高さ100、各ボタン30px×8＝240（はみ出す）
        host.Update(viewport);
        var scroll = (ScrollViewElement)host.Root!;
        Assert.Equal(0f, scroll.ScrollOffset, 0.5f); // 先頭フォーカスはそのまま

        for (int i = 0; i < 5; i++)
        {
            host.MoveFocus(FocusDirection.Down); // 下のボタンへ
            for (int f = 0; f < 6; f++)
            {
                host.Update(viewport, 0.05f); // scroll-to-focus はグライド＝dt で前進
            }
        }

        for (int f = 0; f < 40; f++)
        {
            host.Update(viewport, 0.05f); // グライドの整定
        }

        Assert.True(scroll.ScrollOffset > 0f, $"offset={scroll.ScrollOffset}"); // 追従してスクロールした
        Assert.True(nodes[5].HasFocus);

        // フォーカス中ボタンが viewport（高さ100）内に収まっている。
        Rect focused = host.Focus.FocusedNodeBounds()!.Value;
        Rect vp = scroll.LayoutNode.Bounds;
        Assert.True(focused.Y >= vp.Y - 0.5f && focused.Bottom <= vp.Bottom + 0.5f, $"focused=({focused.Y}..{focused.Bottom}) vp=({vp.Y}..{vp.Bottom})");
    }

    private static Widget[] BuildButtons(FocusNode[] nodes)
    {
        var buttons = new Widget[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            buttons[i] = new Button
            {
                Node = nodes[i],
                Autofocus = i == 0,
                Child = new SizedBox { Width = Dimension.Px(120), Height = Dimension.Px(30) },
            };
        }

        return buttons;
    }
}
