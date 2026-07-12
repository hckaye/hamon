using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of RichText's flow/measure cache (do not repeat FlowSpan/Measure on unchanged input).</summary>
public class RichTextMeasureCacheTests
{
    // Measure 呼び出し回数と Draw された色を記録するスタブ（1 文字 = 10px 幅、行高 20px の決定論スタブ）。
    private sealed class RecordingTextRenderer : ITextRenderer
    {
        public int MeasureCalls { get; private set; }

        public List<Color> DrawnColors { get; } = new();

        public Vec2 Measure(string text, float pixelSize)
        {
            MeasureCalls++;
            return new Vec2(text.Length * 10f, 20f);
        }

        public void Draw(string text, Vec2 position, float pixelSize, Color color) => DrawnColors.Add(color);
    }

    // Render() を素通りさせるための no-op IPainter。
    private sealed class NoOpPainter : IPainter
    {
        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color)
        {
        }

        public void FillRoundedRect(Rect rect, Color color, float radius)
        {
        }

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private static readonly Size Viewport = new(300, 200);

    [Fact]
    public void RepeatedLayout_FreshSpansArrayEachRebuild_SameContent_DoesNotRemeasure()
    {
        // 実運用では Build() のたびに Spans は新しい配列インスタンスで渡される（内容が同じでも参照は変わる）。
        // それでも内容が同じならキャッシュが効き、Measure は再実行されないことを検証する。
        var renderer = new RecordingTextRenderer();
        var host = new HamonRoot(renderer);
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new RichText { Wrap = false, Spans = new[] { new TextSpan("ab"), new TextSpan("cd") } },
        });
        host.Update(Viewport); // 初回計測

        int afterFirst = renderer.MeasureCalls;
        Assert.True(afterFirst > 0);

        for (int i = 0; i < 5; i++)
        {
            host.MarkDirty();
            host.Update(Viewport); // 毎回 SetRoot のラムダが再実行され、新しい Spans 配列が渡される
        }

        Assert.Equal(afterFirst, renderer.MeasureCalls);
    }

    [Fact]
    public void ChangedSpanText_Remeasures()
    {
        var renderer = new RecordingTextRenderer();
        var host = new HamonRoot(renderer);
        string text = "ab";
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new RichText { Wrap = false, Spans = new[] { new TextSpan(text) } },
        });
        host.Update(Viewport);
        int afterFirst = renderer.MeasureCalls;

        text = "abc"; // 内容変更
        host.MarkDirty();
        host.Update(Viewport);

        Assert.True(renderer.MeasureCalls > afterFirst);
    }

    [Fact]
    public void WrapToggled_Remeasures()
    {
        // 幅が有限のとき、Wrap の切り替えで _layoutMaxWidth（無限⇔有限）が変わるため再計測されるべき。
        var renderer = new RecordingTextRenderer();
        var host = new HamonRoot(renderer);
        bool wrap = true;
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new RichText { Wrap = wrap, Spans = new[] { new TextSpan("hello world") } },
        });
        host.Update(new Size(60, 200));
        int afterFirst = renderer.MeasureCalls;

        wrap = false;
        host.MarkDirty();
        host.Update(new Size(60, 200));

        Assert.True(renderer.MeasureCalls > afterFirst);
    }

    [Fact]
    public void MaxWidthChanged_WhenNotWrapped_DoesNotRemeasure()
    {
        // Wrap = false のとき、MaxWidth は計測結果に影響しない（RichText には Ellipsis がない）ため、
        // 幅だけが変わってもキャッシュは有効なままであるべき。
        var renderer = new RecordingTextRenderer();
        var host = new HamonRoot(renderer);
        float width = 60f;
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new RichText { Wrap = false, Spans = new[] { new TextSpan("hello world") } },
        });
        host.Update(new Size(width, 200));
        int afterFirst = renderer.MeasureCalls;

        width = 250f;
        host.MarkDirty();
        host.Update(new Size(width, 200));

        Assert.Equal(afterFirst, renderer.MeasureCalls);
    }

    [Fact]
    public void ColorOnlyChanged_RepaintsWithNewColor()
    {
        // Color はサイズ計測には影響しないが、_placed に焼き込まれ Paint で使われるため、
        // Color だけが変わった場合でもキャッシュヒットで古い色のまま描画されてはいけない。
        var renderer = new RecordingTextRenderer();
        var host = new HamonRoot(renderer);
        Color color = Color.Red;
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new RichText { Wrap = false, Color = color, Spans = new[] { new TextSpan("ab") } },
        });
        host.Update(Viewport);
        host.Render(new NoOpPainter());
        Assert.All(renderer.DrawnColors, c => Assert.Equal(Color.Red, c));

        color = Color.White; // 色だけ変更（テキスト内容・幅・Wrap は同じ）
        host.MarkDirty();
        host.Update(Viewport);
        renderer.DrawnColors.Clear();
        host.Render(new NoOpPainter());

        Assert.NotEmpty(renderer.DrawnColors);
        Assert.All(renderer.DrawnColors, c => Assert.Equal(Color.White, c));
    }
}
