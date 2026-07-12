using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic layout test for RichText (span concatenation, word wrapping, height).</summary>
public class RichTextTests
{
    // 1 文字 = 10px 幅、行高 20px の決定論スタブ。
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * 10f, 20f);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static (HamonRoot host, Element rich) Mount(RichText rich, Size viewport)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = rich });
        host.Update(viewport);
        return (host, host.Root!.Children[0]);
    }

    [Fact]
    public void SingleLine_WidthIsSumOfSpans()
    {
        // "ab"(20) + "cd"(20) 連結＝幅 40・高さ 20。
        (_, Element rich) = Mount(
            new RichText { Wrap = false, Spans = new[] { new TextSpan("ab"), new TextSpan("cd") } },
            new Size(500, 200));

        Rect b = rich.LayoutNode.Bounds;
        Assert.Equal(40f, b.Width, 0.5f);
        Assert.Equal(20f, b.Height, 0.5f);
    }

    [Fact]
    public void Wraps_AtSpaces_WhenWidthExceeded()
    {
        // 幅 100。"hello world foo"：hello(50) world(50→ぴったり 100) foo は次行。高さ 40。
        (_, Element rich) = Mount(
            new RichText { Wrap = true, Spans = new[] { new TextSpan("hello world foo") } },
            new Size(100, 200));

        Rect b = rich.LayoutNode.Bounds;
        Assert.Equal(40f, b.Height, 0.5f); // 2 行
    }

    [Fact]
    public void NoWrap_KeepsSingleLine()
    {
        (_, Element rich) = Mount(
            new RichText { Wrap = false, Spans = new[] { new TextSpan("hello world foo") } },
            new Size(100, 200));

        Rect b = rich.LayoutNode.Bounds;
        Assert.Equal(20f, b.Height, 0.5f); // 1 行のまま
    }

    [Fact]
    public void Spans_ConcatenateWithoutImplicitSpace()
    {
        // "Hello"+"World"（スパン境界に空白なし）＝幅 100・1 行（連結）。
        (_, Element rich) = Mount(
            new RichText { Wrap = true, Spans = new[] { new TextSpan("Hello"), new TextSpan("World") } },
            new Size(500, 200));

        Rect b = rich.LayoutNode.Bounds;
        Assert.Equal(100f, b.Width, 0.5f); // 10 文字ぶん＝連結（空白なし）
        Assert.Equal(20f, b.Height, 0.5f);
    }
}
