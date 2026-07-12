using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for Tooltip (hover wait → display, leave → delete, mouse only).  </summary>
public class TooltipTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 300);

    private static HamonRoot Mount(Widget tooltip)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = tooltip });
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Hover_AfterWait_ShowsOverlay()
    {
        HamonRoot host = Mount(new Tooltip
        {
            Message = "info",
            WaitDuration = 0.5f,
            Child = new SizedBox { Width = Dimension.Px(80), Height = Dimension.Px(30) },
        });

        host.DispatchHover(new Vec2(10, 10));
        Assert.Equal(0, host.OverlayCount); // まだ待ち

        host.Update(Viewport, 0.3f);
        Assert.Equal(0, host.OverlayCount);
        host.Update(Viewport, 0.3f); // 累計 0.6s > 0.5
        Assert.Equal(1, host.OverlayCount); // 表示
    }

    [Fact]
    public void Exit_HidesOverlay()
    {
        HamonRoot host = Mount(new Tooltip
        {
            Message = "info",
            WaitDuration = 0.2f,
            Child = new SizedBox { Width = Dimension.Px(80), Height = Dimension.Px(30) },
        });

        host.DispatchHover(new Vec2(10, 10));
        host.Update(Viewport, 0.3f);
        Assert.Equal(1, host.OverlayCount);

        host.DispatchHover(new Vec2(250, 250)); // 離脱
        host.Update(Viewport);
        Assert.Equal(0, host.OverlayCount);
    }

    [Fact]
    public void ExitBeforeWait_NeverShows()
    {
        HamonRoot host = Mount(new Tooltip
        {
            Message = "info",
            WaitDuration = 0.5f,
            Child = new SizedBox { Width = Dimension.Px(80), Height = Dimension.Px(30) },
        });

        host.DispatchHover(new Vec2(10, 10));
        host.Update(Viewport, 0.2f);
        host.DispatchHover(new Vec2(250, 250)); // 待ち時間前に離脱
        host.Update(Viewport, 0.5f);
        Assert.Equal(0, host.OverlayCount);
    }
}
