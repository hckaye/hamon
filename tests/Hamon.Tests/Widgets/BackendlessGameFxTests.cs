using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of backend-independent game effects (text borders, spring physics, multi-stage gradation).</summary>
public class BackendlessGameFxTests
{
    private sealed class CountingText : ITextRenderer
    {
        public int Draws;

        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color) => Draws++;
    }

    private sealed class NullPainter : IPainter
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

    private sealed class GradRecorder : IPainter
    {
        public int Gradients;
        public int Rects;

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color) => Rects++;

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

        public void FillGradient(Rect rect, Color a, Color b, GradientAxis axis) => Gradients++;
    }

    private static readonly Size Viewport = new(200, 200);

    [Fact]
    public void TextOutline_DrawsEightExtraPasses()
    {
        var tr = new CountingText();
        var host = new HamonRoot(tr);
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = new Text("AB") { OutlineColor = Color.Black, OutlineWidth = 2f } });
        host.Update(Viewport);
        host.Render(new NullPainter());
        Assert.Equal(9, tr.Draws); // 1行 ×（8方向の縁取り＋本体1）
    }

    [Fact]
    public void Text_NoOutline_OneDraw()
    {
        var tr = new CountingText();
        var host = new HamonRoot(tr);
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = new Text("AB") });
        host.Update(Viewport);
        host.Render(new NullPainter());
        Assert.Equal(1, tr.Draws);
    }

    [Fact]
    public void Spring_ConvergesToTarget()
    {
        var host = new HamonRoot(new CountingText());
        var spring = new SpringController(host, stiffness: 200f, damping: 28f);
        spring.SetTarget(1f);

        for (int i = 0; i < 600 && spring.IsAnimating; i++)
        {
            spring.Tick(0.016f);
        }

        Assert.False(spring.IsAnimating); // 収束した
        Assert.Equal(1f, spring.Value, 2);
    }

    [Fact]
    public void Spring_Underdamped_Overshoots()
    {
        var host = new HamonRoot(new CountingText());
        var spring = new SpringController(host, stiffness: 320f, damping: 4f);
        spring.SetTarget(1f);

        float max = 0f;
        for (int i = 0; i < 600 && spring.IsAnimating; i++)
        {
            spring.Tick(0.016f);
            max = System.MathF.Max(max, spring.Value);
        }

        Assert.True(max > 1.05f, $"低減衰は行き過ぎるはず: max={max}");
    }

    [Fact]
    public void FillGradientStops_DrawsSegments()
    {
        var rec = new GradRecorder();
        var ctx = new PaintContext(rec);
        var stops = new[]
        {
            new GradientStop(0f, Color.Red),
            new GradientStop(0.5f, Color.White),
            new GradientStop(1f, Color.SkyBlue),
        };
        ctx.FillGradientStops(new Rect(0f, 0f, 100f, 100f), stops, GradientAxis.Vertical);

        Assert.Equal(2, rec.Gradients); // 3停止点 → 2区間
        Assert.Equal(0, rec.Rects);     // 両端 0/1 なので端ベタ塗りなし
    }

    [Fact]
    public void FillGradientStops_FillsEdgesWhenNotSpanningFull()
    {
        var rec = new GradRecorder();
        var ctx = new PaintContext(rec);
        var stops = new[]
        {
            new GradientStop(0.25f, Color.Red),
            new GradientStop(0.75f, Color.SkyBlue),
        };
        ctx.FillGradientStops(new Rect(0f, 0f, 100f, 100f), stops, GradientAxis.Vertical);

        Assert.Equal(1, rec.Gradients); // 中央1区間
        Assert.Equal(2, rec.Rects);     // 前後の端色ベタ塗り
    }
}
