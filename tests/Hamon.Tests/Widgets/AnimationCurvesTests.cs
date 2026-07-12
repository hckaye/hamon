using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic verification of additional easing, cubic-bezier, TweenSequence, and Transform rotation (drawing routing).</summary>
public class AnimationCurvesTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class RotRecorder : IPainter
    {
        public int Fills;
        public int RoundedFills;
        public int RotatedFills;

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color) => Fills++;

        public void FillRoundedRect(Rect rect, Color color, float radius) => RoundedFills++;

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }

        public void FillRectRotated(Rect rect, Color color, float radians, Vec2 pivot) => RotatedFills++;
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void Curves_PinEndpoints(float t)
    {
        Assert.Equal(t, Curves.EaseOutCubic(t), 3);
        Assert.Equal(t, Curves.EaseInOutCubic(t), 3);
        Assert.Equal(t, Curves.FastOutSlowIn(t), 3);
        Assert.Equal(t, Curves.BounceOut(t), 3);
        Assert.Equal(t, Curves.Decelerate(t), 3);
    }

    [Fact]
    public void FastOutSlowIn_IsMonotonic()
    {
        float prev = -1f;
        for (int i = 0; i <= 20; i++)
        {
            float v = Curves.FastOutSlowIn(i / 20f);
            Assert.True(v >= prev - 1e-4f, $"非単調 @ {i}: {v} < {prev}");
            prev = v;
        }
    }

    [Fact]
    public void EaseOutBack_Overshoots()
    {
        // どこかで 1 を超える（行き過ぎ）が、終端では 1 に戻る。
        bool over = false;
        for (int i = 0; i <= 20; i++)
        {
            if (Curves.EaseOutBack(i / 20f) > 1.0001f)
            {
                over = true;
            }
        }

        Assert.True(over, "overshoot しない");
        Assert.Equal(1f, Curves.EaseOutBack(1f), 3);
    }

    [Fact]
    public void CubicBezier_PinsAndMonotonic()
    {
        Curve c = Curves.CubicBezier(0.25f, 0.1f, 0.25f, 1f);
        Assert.Equal(0f, c(0f), 3);
        Assert.Equal(1f, c(1f), 3);
        float prev = -1f;
        for (int i = 0; i <= 20; i++)
        {
            float v = c(i / 20f);
            Assert.True(v >= prev - 1e-3f);
            prev = v;
        }
    }

    [Fact]
    public void TweenSequence_InterpolatesKeyframes()
    {
        var seq = new TweenSequence(
            (0f, 0f, Curves.Linear),
            (0.5f, 10f, Curves.Linear),
            (1f, 0f, Curves.Linear));

        Assert.Equal(0f, seq.Evaluate(0f), 3);
        Assert.Equal(5f, seq.Evaluate(0.25f), 3);
        Assert.Equal(10f, seq.Evaluate(0.5f), 3);
        Assert.Equal(5f, seq.Evaluate(0.75f), 3);
        Assert.Equal(0f, seq.Evaluate(1f), 3);
        Assert.Equal(0f, seq.Evaluate(-1f), 3); // クランプ
        Assert.Equal(0f, seq.Evaluate(2f), 3);  // クランプ
    }

    [Fact]
    public void Transform_Rotation_RoutesToRotatedFill()
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Transform
            {
                Rotation = 0.5f,
                Child = new Container
                {
                    Color = new Color(200, 200, 200),
                    Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) },
                },
            },
        });
        host.Update(new Size(100, 100));

        var rec = new RotRecorder();
        host.Render(rec);
        Assert.True(rec.RotatedFills > 0, "回転時は回転塗りへ");
        Assert.Equal(0, rec.RoundedFills);
    }

    [Fact]
    public void Transform_NoRotation_UsesNormalFill()
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Transform
            {
                Scale = 1.2f,
                Child = new Container
                {
                    Color = new Color(200, 200, 200),
                    Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) },
                },
            },
        });
        host.Update(new Size(100, 100));

        var rec = new RotRecorder();
        host.Render(rec);
        Assert.Equal(0, rec.RotatedFills);
        Assert.True(rec.RoundedFills > 0);
    }
}
