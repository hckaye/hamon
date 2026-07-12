using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verify AnimatedSprite (frame-by-frame advance = source rectangle advances/loop) and Material.ColorGetter (read color when drawing).</summary>
public class SpriteAndColorTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class StubTexture : ITexture
    {
        public StubTexture(int w, int h)
        {
            Width = w;
            Height = h;
        }

        public int Width { get; }

        public int Height { get; }
    }

    private sealed class Recorder : IPainter
    {
        public RectInt LastSource;
        public List<Color> RoundedColors { get; } = new();
        public List<Color> RectColors { get; } = new();

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color) => RectColors.Add(color);

        public void FillRoundedRect(Rect rect, Color color, float radius) => RoundedColors.Add(color);

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint) => LastSource = source;

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    [Fact]
    public void AnimatedSprite_AdvancesFrame()
    {
        var tex = new StubTexture(128, 32); // 4 列 × 1 行
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new AnimatedSprite { Texture = tex, FrameWidth = 32, FrameHeight = 32, FrameCount = 4, Fps = 10f },
        });
        host.Update(Viewport);

        var rec = new Recorder();
        host.Render(rec);
        Assert.Equal(0, rec.LastSource.X); // コマ0

        host.Update(Viewport, 0.15f); // 0.15*10 = 1.5 → コマ1
        host.Render(rec);
        Assert.Equal(32, rec.LastSource.X); // コマ1（次の格子）
    }

    [Fact]
    public void AnimatedSprite_Loops()
    {
        var tex = new StubTexture(128, 32);
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new AnimatedSprite { Texture = tex, FrameWidth = 32, FrameHeight = 32, FrameCount = 4, Fps = 10f, Loop = true },
        });
        host.Update(Viewport);

        host.Update(Viewport, 0.45f); // 4.5 → コマ 4%4 = 0 へ折り返し
        var rec = new Recorder();
        host.Render(rec);
        Assert.Equal(0, rec.LastSource.X);
    }

    [Fact]
    public void Material_ColorGetter_UsedAtPaint()
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Material
            {
                Radius = 8f,
                ColorGetter = () => new Color(11, 22, 33),
                Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(60) },
            },
        });
        host.Update(Viewport);

        var rec = new Recorder();
        host.Render(rec);
        Assert.Contains(rec.RoundedColors, c => c.R == 11 && c.G == 22 && c.B == 33);
    }
}
