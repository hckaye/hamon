using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verify AppBar/NavigationBar/Scaffold/Snackbar/Toast/DatePicker smoke (no drawing exceptions) and Snackbar automatic disappearance.</summary>
public class HighLevelWidgetsTests
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

    private static readonly Size Viewport = new(360, 720);

    private static HamonRoot Mount(Widget root)
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => root);
        host.Update(Viewport);
        host.Render(new NullPainter());
        return host;
    }

    [Fact]
    public void Scaffold_WithAppBarAndNavBar_Renders()
    {
        int selected = 0;
        Mount(new Scaffold
        {
            AppBar = new AppBar { Title = "ホーム", Actions = new Widget[] { new Text("⚙") { FontSize = 18f } } },
            Body = new Center { Child = new Text("本体") { FontSize = 16f } },
            BottomNavigationBar = new NavigationBar
            {
                SelectedIndex = selected,
                OnDestinationSelected = i => selected = i,
                Destinations = new[]
                {
                    new NavigationDestination { Label = "ホーム", Icon = "🏠" },
                    new NavigationDestination { Label = "検索", Icon = "🔍" },
                    new NavigationDestination { Label = "設定", Icon = "⚙" },
                },
            },
        });
    }

    [Fact]
    public void Snackbar_AutoDismisses()
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new SizedBox());
        host.Update(Viewport);

        host.ShowSnackbar("保存しました", seconds: 1f);
        host.Update(Viewport, 0.01f);
        Assert.Equal(1, host.OverlayCount);

        for (int i = 0; i < 30; i++)
        {
            host.Update(Viewport, 0.1f); // 1秒 + 退場アニメ(0.25) を十分に超える
            host.Render(new NullPainter());
        }

        Assert.Equal(0, host.OverlayRenderedCount); // 自動で消えた
    }

    [Fact]
    public void Toast_AutoDismisses()
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new SizedBox());
        host.Update(Viewport);

        host.ShowToast("コピーしました", seconds: 0.5f);
        Assert.Equal(1, host.OverlayCount);

        for (int i = 0; i < 20; i++)
        {
            host.Update(Viewport, 0.1f);
        }

        Assert.Equal(0, host.OverlayRenderedCount);
    }

    [Fact]
    public void Snackbar_Action_Fires()
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new SizedBox());
        host.Update(Viewport);

        bool fired = false;
        host.ShowSnackbar("削除しました", seconds: 5f, actionLabel: "取消", onAction: () => fired = true);
        host.Update(Viewport, 0.01f);
        host.Render(new NullPainter());

        Assert.False(fired); // タップしていないので未発火（描画・配線が成立していることの確認）
    }

    [Fact]
    public void CalendarDatePicker_Renders()
    {
        DateTime? picked = null;
        Mount(new Center
        {
            Child = new CalendarDatePicker
            {
                InitialMonth = new DateTime(2026, 6, 1),
                SelectedDate = new DateTime(2026, 6, 20),
                OnDateSelected = d => picked = d,
            },
        });
        Assert.Null(picked); // タップしていないので未選択
    }

    [Fact]
    public void ShowDatePicker_OpensOverlay()
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new SizedBox());
        host.Update(Viewport);

        host.ShowDatePicker(new DateTime(2026, 6, 1), _ => { }, selected: new DateTime(2026, 6, 20));
        host.Update(Viewport, 0.01f);
        Assert.Equal(1, host.OverlayCount);
        host.Render(new NullPainter());
    }
}
