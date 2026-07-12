using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic testing for themes (BuildContext propagation of default Dark/HamonRoot.Theme). </summary>
public class ThemeTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class CaptureTheme : StatelessWidget
    {
        private readonly Action<HamonTheme> _capture;

        public CaptureTheme(Action<HamonTheme> capture) => _capture = capture;

        public override Widget Build(BuildContext context)
        {
            _capture(context.Theme);
            return new SizedBox();
        }
    }

    [Fact]
    public void DefaultTheme_IsRippleLight()
    {
        HamonTheme? seen = null;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new CaptureTheme(t => seen = t));
        host.Update(new Size(100, 100));

        Assert.Same(HamonTheme.Default, seen);
        Assert.Equal(Brightness.Light, seen!.Brightness);
    }

    [Fact]
    public void CustomTheme_PropagatesToContext()
    {
        HamonTheme? seen = null;
        var host = new HamonRoot(new StubTextRenderer()) { Theme = HamonTheme.Dark };
        host.SetRoot(() => new CaptureTheme(t => seen = t));
        host.Update(new Size(100, 100));

        Assert.Same(HamonTheme.Dark, seen);
    }

    [Fact]
    public void DarkMode_IsOptIn_StaysLightUntilDarkThemeSet()
    {
        // System モード＋OS=Dark でも、DarkTheme 未設定ならライトのまま（ゲーム既定の opt-in）。
        var host = new HamonRoot(new StubTextRenderer())
        {
            ThemeMode = ThemeMode.System,
            PlatformBrightness = Brightness.Dark,
        };
        Assert.Same(host.Theme, host.EffectiveTheme);

        // ThemeMode.Dark を強制しても、DarkTheme 未設定なら Theme にフォールバック。
        host.ThemeMode = ThemeMode.Dark;
        Assert.Same(host.Theme, host.EffectiveTheme);
    }

    [Fact]
    public void EffectiveTheme_ResolvesModeAndPlatformBrightness()
    {
        var host = new HamonRoot(new StubTextRenderer())
        {
            Theme = HamonTheme.Default,
            DarkTheme = HamonTheme.Dark, // opt-in でダーク有効化
        };

        // 明示 Dark → DarkTheme。
        host.ThemeMode = ThemeMode.Dark;
        Assert.Same(HamonTheme.Dark, host.EffectiveTheme);

        // 明示 Light → Theme（OS が Dark でも従わない）。
        host.ThemeMode = ThemeMode.Light;
        host.PlatformBrightness = Brightness.Dark;
        Assert.Same(host.Theme, host.EffectiveTheme);

        // System → OS 明るさに追従。
        host.ThemeMode = ThemeMode.System;
        host.PlatformBrightness = Brightness.Dark;
        Assert.Same(HamonTheme.Dark, host.EffectiveTheme);
        host.PlatformBrightness = Brightness.Light;
        Assert.Same(host.Theme, host.EffectiveTheme);
    }

    [Fact]
    public void EffectiveTheme_PropagatesToContext_WhenDarkOptedIn()
    {
        HamonTheme? seen = null;
        var host = new HamonRoot(new StubTextRenderer())
        {
            DarkTheme = HamonTheme.Dark,
            ThemeMode = ThemeMode.Dark,
        };
        host.SetRoot(() => new CaptureTheme(t => seen = t));
        host.Update(new Size(100, 100));

        Assert.Same(HamonTheme.Dark, seen);
    }
}
