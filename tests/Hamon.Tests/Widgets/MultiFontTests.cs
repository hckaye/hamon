using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Multiple fonts:<see cref="Text.Font"/>but<see cref="ITextRenderer"/>Verified that it flows to the font specification overload.</summary>
public class MultiFontTests
{
    // フォント名つきオーバーロードを実装し、計測時に渡されたフォント名を記録する renderer。
    private sealed class RecordingRenderer : ITextRenderer
    {
        public readonly List<string?> MeasuredFonts = new();

        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }

        public Vec2 Measure(string text, float pixelSize, string? font)
        {
            MeasuredFonts.Add(font);
            return Measure(text, pixelSize);
        }

        public void Draw(string text, Vec2 position, float pixelSize, Color color, string? font)
        {
        }
    }

    private static List<string?> MeasuredFontsFor(Text text)
    {
        var renderer = new RecordingRenderer();
        var host = new HamonRoot(renderer);
        host.SetRoot(() => text);
        host.Update(new Size(300, 100));
        return renderer.MeasuredFonts;
    }

    [Fact]
    public void TextFont_FlowsToRenderer()
    {
        List<string?> fonts = MeasuredFontsFor(new Text("こんにちは") { Font = "serif" });
        Assert.Contains("serif", fonts);
        Assert.DoesNotContain(null, fonts);
    }

    [Fact]
    public void TextWithoutFont_UsesDefault()
    {
        List<string?> fonts = MeasuredFontsFor(new Text("hello"));
        Assert.Contains(null, fonts); // font 未指定＝null（実装側で既定フォントへ解決）
    }
}
