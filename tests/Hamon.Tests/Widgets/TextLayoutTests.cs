using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test for text omission (ellipsis)/wrapping (softwrap)/line limit (stub is number of characters x size width). </summary>
public class TextLayoutTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static TextElement Mount(Text text, float width)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => text);
        host.Update(new Size(width, 100));
        return (TextElement)host.Root!;
    }

    [Fact]
    public void Ellipsis_TruncatesToFit()
    {
        // FontSize10・幅55。"abcdefgh"(80) は溢れる → "abcd…"(50) が最長。
        TextElement el = Mount(new Text("abcdefgh") { FontSize = 10f, Overflow = TextOverflow.Ellipsis }, 55f);
        Assert.Single(el.Lines);
        Assert.Equal("abcd…", el.Lines[0]);
    }

    [Fact]
    public void Clip_KeepsFullStringOnOneLine()
    {
        TextElement el = Mount(new Text("abcdefgh") { FontSize = 10f, Overflow = TextOverflow.Clip }, 55f);
        Assert.Single(el.Lines);
        Assert.Equal("abcdefgh", el.Lines[0]); // クリップは省略せず1行（親がクリップ）
    }

    [Fact]
    public void Softwrap_BreaksIntoLines()
    {
        // 幅25・FontSize10 → 1行2文字。"abcdef" → ab / cd / ef。
        TextElement el = Mount(new Text("abcdef") { FontSize = 10f, Softwrap = true }, 25f);
        Assert.Equal(3, el.Lines.Count);
        Assert.Equal("ab", el.Lines[0]);
        Assert.Equal("ef", el.Lines[2]);
    }

    [Fact]
    public void Softwrap_MaxLines_EllipsizesLast()
    {
        TextElement el = Mount(
            new Text("abcdef") { FontSize = 10f, Softwrap = true, MaxLines = 2, Overflow = TextOverflow.Ellipsis },
            25f);
        Assert.Equal(2, el.Lines.Count);
        Assert.Equal("ab", el.Lines[0]);
        Assert.Equal("c…", el.Lines[1]); // 残り"cdef"を省略
    }
}
