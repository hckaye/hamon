using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verify FxController (floating text/particle pool/lifespan) and FxLayer drawing (no exceptions).</summary>
public class FxTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
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

    [Fact]
    public void SpawnAndExpire()
    {
        var fx = new FxController();
        fx.SpawnText(new Vec2(10, 10), "123", Color.White, life: 0.5f);
        fx.SpawnBurst(new Vec2(10, 10), 8, Color.Red, life: 0.3f);

        Assert.Equal(1, fx.ActiveFloaterCount);
        Assert.Equal(8, fx.ActiveParticleCount);

        for (int i = 0; i < 20; i++)
        {
            fx.Tick(0.05f); // 1秒経過＝全て寿命切れ
        }

        Assert.Equal(0, fx.ActiveFloaterCount);
        Assert.Equal(0, fx.ActiveParticleCount);
    }

    [Fact]
    public void Pool_DoesNotOverflow()
    {
        var fx = new FxController(maxFloaters: 4, maxParticles: 8);
        for (int i = 0; i < 100; i++)
        {
            fx.SpawnText(new Vec2(0, 0), "x", Color.White, life: 10f);
        }

        Assert.True(fx.ActiveFloaterCount <= 4); // 固定容量を超えない
    }

    [Fact]
    public void FxLayer_RendersWithoutThrowing()
    {
        var fx = new FxController();
        fx.SpawnText(new Vec2(50, 50), "999", Color.White);
        fx.SpawnBurst(new Vec2(50, 50), 12, Color.Goldenrod);

        var host = new HamonRoot(new StubText());
        host.RegisterTicker(fx);
        host.SetRoot(() => new Stack { Fit = StackFit.Expand, Children = new Widget[] { new SizedBox(), new FxLayer { Controller = fx } } });
        host.Update(new Size(200, 200), 0.016f);
        host.Render(new NullPainter());
    }
}
