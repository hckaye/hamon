using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Shared element transition<see cref="Hero"/>Verify that the registration lifecycle, transition flags, and push/pop are exception-free.</summary>
public class HeroTests
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

    private static readonly Size Viewport = new(300, 300);

    private static Widget HeroBox(string tag, float size) => new Align
    {
        Alignment = Alignment.TopLeft,
        Child = new Hero
        {
            Tag = tag,
            Child = new Container { Color = new Color(100, 150, 200), Child = new SizedBox { Width = Dimension.Px(size), Height = Dimension.Px(size) } },
        },
    };

    private static (HamonRoot Host, NavigatorController Nav) Setup()
    {
        var host = new HamonRoot(new StubText());
        var nav = new NavigatorController(host, () => HeroBox("img", 50f)) { TransitionDuration = 0.2f };
        host.SetRoot(() => new Navigator { Controller = nav });
        host.Update(Viewport);
        return (host, nav);
    }

    [Fact]
    public void Hero_RegistersOnMount()
    {
        (HamonRoot host, _) = Setup();
        Assert.True(host.Heroes.ByTag.TryGetValue("img", out List<IHeroSource>? list));
        Assert.Single(list!);
    }

    [Fact]
    public void Push_AddsSecondHero_AndActivatesTransition()
    {
        (HamonRoot host, NavigatorController nav) = Setup();
        nav.Push(() => HeroBox("img", 100f));
        host.Update(Viewport, 0.01f); // 新ルートをマウント＝2つ目の Hero が登録される

        Assert.Equal(2, host.Heroes.ByTag["img"].Count);
        Assert.True(host.Heroes.TransitionActive, "遷移中フラグが立つ");
        Assert.True(host.Heroes.IsFlying("img"), "同タグ2つ＝flight 中");
    }

    [Fact]
    public void Transition_Completes_ClearsFlag()
    {
        (HamonRoot host, NavigatorController nav) = Setup();
        nav.Push(() => HeroBox("img", 100f));
        for (int i = 0; i < 10; i++)
        {
            host.Update(Viewport, 0.1f); // 入場アニメを完了させる
        }

        Assert.False(host.Heroes.TransitionActive, "完了後はフラグが下りる");
        Assert.False(host.Heroes.IsFlying("img"));
    }

    [Fact]
    public void Pop_RemovesSecondHero_AfterExit()
    {
        (HamonRoot host, NavigatorController nav) = Setup();
        nav.Push(() => HeroBox("img", 100f));
        for (int i = 0; i < 10; i++)
        {
            host.Update(Viewport, 0.1f);
        }

        nav.Pop();
        for (int i = 0; i < 10; i++)
        {
            host.Update(Viewport, 0.1f); // 退場アニメ完了＝上ルートの Hero が unmount→登録解除
        }

        Assert.Single(host.Heroes.ByTag["img"]);
    }

    [Fact]
    public void Flight_RendersWithoutThrowing()
    {
        (HamonRoot host, NavigatorController nav) = Setup();
        nav.Push(() => HeroBox("img", 100f));
        host.Update(Viewport, 0.05f);

        var painter = new NullPainter();
        host.Render(painter); // flight 中の HeroLayer 描画が例外を投げない
        host.Update(Viewport, 0.05f);
        host.Render(painter);
    }
}
