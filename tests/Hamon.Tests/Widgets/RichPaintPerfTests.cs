using Hamon.Layout;
using Hamon.Widgets;
using System;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Allocation regression for stationary frames with rich drawings (gradients/shadows/arcs/rotations/spinners).
/// Verify that the heap allocation per frame is small (ZeroAlloc convention, which prevents junk from causing GC pauses).
/// </summary>
public class RichPaintPerfTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class StubPainter : IPainter
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

        // 新プリミティブも記録せず即 return（描画側のアロケのみを測る）。
        public void DrawLine(Vec2 a, Vec2 b, float thickness, Color color)
        {
        }

        public void FillCircle(Vec2 center, float radius, Color color)
        {
        }

        public void FillGradient(Rect rect, Color a, Color b, GradientAxis axis)
        {
        }

        public void FillShadow(Rect rect, Color color, float radius, float blur)
        {
        }

        public void FillRectRotated(Rect rect, Color color, float radians, Vec2 pivot)
        {
        }

        public void DrawTextureRotated(ITexture texture, Rect dest, RectInt source, Color tint, float radians, Vec2 pivot)
        {
        }
    }

    private static readonly Size Viewport = new(720, 1280);

    [Fact]
    public void RichAnimatedTree_SteadyState_AllocatesMinimally()
    {
        float angle = 0f;
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            Children = new Widget[]
            {
                new Material
                {
                    Color = new Color(99, 102, 241),
                    GradientTo = new Color(168, 85, 247),
                    Elevation = 4f,
                    Child = new SizedBox { Height = Dimension.Px(120) },
                },
                new Row
                {
                    Children = new Widget[]
                    {
                        new Expanded { Child = new Card { Elevation = 2f, Child = new SizedBox { Height = Dimension.Px(60) } } },
                        new Expanded { Child = new Card { Elevation = 2f, Child = new SizedBox { Height = Dimension.Px(60) } } },
                    },
                },
                new CircularProgressIndicator { Value = 0.6f, Diameter = 64f },
                new CircularProgressIndicator { Diameter = 40f },
                new Transform
                {
                    RotationGetter = () => angle,
                    Origin = Alignment.Center,
                    Child = new Material { Color = new Color(251, 191, 36), Radius = 8f, Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) } },
                },
            },
        });

        var painter = new StubPainter();
        for (int i = 0; i < 8; i++)
        {
            host.Update(Viewport, 0.016f);
            host.Render(painter);
        }

        const int frames = 300;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < frames; i++)
        {
            angle += 0.02f; // 回転を進める（描画時 getter が読む）
            host.Update(Viewport, 0.016f); // スピナー/clock ティッカーも進む
            host.Render(painter);
        }

        long perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
        Assert.True(perFrame < 512, $"per-frame allocation = {perFrame} bytes（アニメ稼働中の定常フレーム）");
    }
}
