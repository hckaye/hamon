using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test of animation infrastructure (AnimationController/Curve/Tween/Transform2D + PaintContext opacity/transform composition).</summary>
public class AnimationTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    [Fact]
    public void Controller_Forward_AdvancesAndCompletes()
    {
        var host = new HamonRoot(new StubTextRenderer());
        AnimationController ctrl = host.CreateAnimation(1f);

        ctrl.Forward();
        Assert.True(ctrl.Tick(0.25f));
        Assert.Equal(0.25f, ctrl.Value, 0.001f);
        Assert.True(ctrl.IsAnimating);

        Assert.True(ctrl.Tick(0.5f));
        Assert.Equal(0.75f, ctrl.Value, 0.001f);

        Assert.False(ctrl.Tick(0.5f)); // 端で終了（登録解除）
        Assert.Equal(1f, ctrl.Value, 0.001f);
        Assert.False(ctrl.IsAnimating);
    }

    [Fact]
    public void Controller_Completed_FiresOnceAtEnd()
    {
        var host = new HamonRoot(new StubTextRenderer());
        AnimationController ctrl = host.CreateAnimation(1f);
        int done = 0;
        ctrl.OnCompleted = () => done++;

        ctrl.Forward();
        ctrl.Tick(0.5f);
        Assert.Equal(0, done);
        ctrl.Tick(0.6f); // 到達
        Assert.Equal(1, done);
    }

    [Fact]
    public void Controller_AdvancesViaHostUpdate()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new SizedBox());
        host.Update(new Size(100, 100));

        AnimationController ctrl = host.CreateAnimation(2f);
        ctrl.Forward();
        host.Update(new Size(100, 100), 0.5f); // dt=0.5 / 2s = 0.25

        Assert.Equal(0.25f, ctrl.Value, 0.001f);
    }

    [Fact]
    public void Controller_JumpTo_SetsValueAndStops()
    {
        var host = new HamonRoot(new StubTextRenderer());
        AnimationController ctrl = host.CreateAnimation(1f);
        ctrl.Forward();
        ctrl.JumpTo(0.4f);

        Assert.Equal(0.4f, ctrl.Value, 0.001f);
        Assert.False(ctrl.IsAnimating);
    }

    [Fact]
    public void Curve_EaseInOut_MapsProgress()
    {
        var host = new HamonRoot(new StubTextRenderer());
        AnimationController ctrl = host.CreateAnimation(1f, Curves.EaseInOut);
        ctrl.JumpTo(0.25f);

        Assert.Equal(0.25f, ctrl.Value, 0.001f);
        Assert.Equal(0.125f, ctrl.Curved, 0.001f); // 2*0.25^2
    }

    [Fact]
    public void Tween_And_ColorTween_Lerp()
    {
        var size = new Tween(0.6f, 1f);
        Assert.Equal(0.8f, size.Lerp(0.5f), 0.001f);

        var color = new ColorTween(Color.Black, Color.White);
        Color mid = color.Lerp(0.5f);
        Assert.InRange((int)mid.R, 126, 128);
    }

    [Fact]
    public void Transform2D_ScaleAbout_KeepsPivotFixed()
    {
        Transform2D t = Transform2D.ScaleAbout(new Vec2(0.5f, 0.5f), new Vec2(50, 50), Vec2.Zero);

        Assert.Equal(new Vec2(50, 50), t.Apply(new Vec2(50, 50)));   // 支点は不動
        Assert.Equal(new Vec2(25, 25), t.Apply(new Vec2(0, 0)));      // 角は支点へ寄る

        Rect r = t.Apply(new Rect(0, 0, 100, 100));
        Assert.Equal(25f, r.X, 0.001f);
        Assert.Equal(50f, r.Width, 0.001f); // 0.5 倍
    }

    [Fact]
    public void Transform2D_Compose_NestsOuterThenInner()
    {
        Transform2D outer = new(new Vec2(2, 2), Vec2.Zero);
        Transform2D inner = new(new Vec2(3, 3), new Vec2(1, 1));
        Transform2D c = Transform2D.Compose(outer, inner);

        // outer.Apply(inner.Apply(p)) と一致。
        Assert.Equal(outer.Apply(inner.Apply(new Vec2(5, 5))), c.Apply(new Vec2(5, 5)));
    }

    [Fact]
    public void PaintContext_Opacity_MultipliesAlphaOnly()
    {
        var ctx = new PaintContext(null!, null!).WithOpacity(0.5f);
        Color faded = ctx.ApplyOpacity(new Color(200, 100, 50, 255));

        Assert.Equal(200, faded.R); // RGB は保つ（黒化しない）
        Assert.Equal(127, faded.A); // アルファのみ半分

        // 入れ子で乗算される。
        Color twice = ctx.WithOpacity(0.5f).ApplyOpacity(new Color(255, 255, 255, 255));
        Assert.Equal(63, twice.A); // 255 * 0.25
    }

    [Fact]
    public void PaintContext_Transform_AppliesToPoint()
    {
        Transform2D t = Transform2D.ScaleAbout(new Vec2(2, 2), Vec2.Zero, new Vec2(10, 0));
        var ctx = new PaintContext(null!, null!).WithTransform(t);

        Assert.Equal(new Vec2(30, 20), ctx.ApplyTransform(new Vec2(10, 10))); // 10*2+10, 10*2+0
        Assert.Equal(2f, ctx.ScaleY, 0.001f);
    }

    // キャレット点滅（ping-pong）の回帰テスト：端で OnCompleted が逆方向へ再駆動したら
    // Tick は「継続中(true)」を返し続けねばならない（false だとオーナーが登録解除し1半周期で固まる）。
    [Fact]
    public void Controller_PingPong_RedriveOnCompleted_StaysAnimating()
    {
        var host = new HamonRoot(new StubTextRenderer());
        AnimationController ctrl = host.CreateAnimation(0.5f);
        ctrl.OnCompleted = () =>
        {
            if (ctrl.Value <= 0f)
            {
                ctrl.Forward();
            }
            else
            {
                ctrl.Reverse();
            }
        };

        ctrl.JumpTo(1f);
        ctrl.Reverse(); // 1 → 0

        bool aliveAtZero = ctrl.Tick(0.5f); // 0 到達 → OnCompleted が Forward 再駆動
        Assert.Equal(0f, ctrl.Value, 0.001f);
        Assert.True(aliveAtZero);  // 再駆動したので継続（修正前は false ＝点滅が止まる）
        Assert.True(ctrl.IsAnimating);

        bool aliveAtOne = ctrl.Tick(0.5f); // 0 → 1 到達 → OnCompleted が Reverse 再駆動
        Assert.Equal(1f, ctrl.Value, 0.001f);
        Assert.True(aliveAtOne);
        Assert.True(ctrl.IsAnimating);
    }
}
