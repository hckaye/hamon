using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of Text measurement cache (do not repeat FontStash measurements on the same input). </summary>
public class TextMeasureCacheTests
{
    // Measure 呼び出し回数を数える計測スタブ。
    private sealed class CountingTextRenderer : ITextRenderer
    {
        public int Calls { get; private set; }

        public Vec2 Measure(string text, float pixelSize)
        {
            Calls++;
            return new Vec2(text.Length * pixelSize, pixelSize);
        }

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 200);

    [Fact]
    public void RepeatedLayout_SameInput_DoesNotRemeasure()
    {
        var renderer = new CountingTextRenderer();
        var host = new HamonRoot(renderer);
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = new Text("Hello world") { FontSize = 16f } });
        host.Update(Viewport); // 初回計測

        int afterFirst = renderer.Calls;
        Assert.True(afterFirst > 0);

        // 同一サイズ・同一 widget で再レイアウトを促す（全再構築）。
        for (int i = 0; i < 5; i++)
        {
            host.MarkDirty();
            host.Update(Viewport);
        }

        // キャッシュが効いていれば、再計測は発生しない（呼び出し回数が増えない）。
        Assert.Equal(afterFirst, renderer.Calls);
    }

    [Fact]
    public void ChangedText_Remeasures()
    {
        var renderer = new CountingTextRenderer();
        var host = new HamonRoot(renderer);
        string data = "A";
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = new Text(data) { FontSize = 16f } });
        host.Update(Viewport);
        int afterFirst = renderer.Calls;

        data = "AB"; // 内容変更
        host.MarkDirty();
        host.Update(Viewport);

        Assert.True(renderer.Calls > afterFirst); // 再計測される
    }
}
