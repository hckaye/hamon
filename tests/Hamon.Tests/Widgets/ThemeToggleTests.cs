using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification that theme (Light/Dark) switching is reflected in the solution color and extension token consistency. </summary>
public class ThemeToggleTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class RecordingPainter : IPainter
    {
        public List<Color> RoundedFills { get; } = new();

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color)
        {
        }

        public void FillRoundedRect(Rect rect, Color color, float radius) => RoundedFills.Add(color);

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    // テーマの面色で塗る小ウィジェット。
    private sealed class ThemedSurface : StatelessWidget
    {
        public override Widget Build(BuildContext context) =>
            new Container { Width = Dimension.Px(50), Height = Dimension.Px(50), Radius = 4f, Color = context.Theme.Surface };
    }

    private static readonly Size Viewport = new(100, 100);

    [Fact]
    public void SwitchingTheme_ChangesResolvedColors()
    {
        var host = new HamonRoot(new StubTextRenderer()) { Theme = HamonTheme.Dark };
        host.SetRoot(() => new ThemedSurface());
        host.Update(Viewport);

        var dark = new RecordingPainter();
        host.Render(dark);
        Color darkSurface = dark.RoundedFills.Find(c => c.Equals(HamonTheme.Dark.Surface));
        Assert.Equal(HamonTheme.Dark.Surface, darkSurface);

        // ライトへ切替＝再構築して再描画。
        host.Theme = HamonTheme.Light;
        host.MarkDirty();
        host.Update(Viewport);
        var light = new RecordingPainter();
        host.Render(light);

        Assert.Contains(light.RoundedFills, c => c.Equals(HamonTheme.Light.Surface));
        Assert.DoesNotContain(light.RoundedFills, c => c.Equals(HamonTheme.Dark.Surface)); // ダーク面色は出ない
        Assert.NotEqual(HamonTheme.Dark.Surface, HamonTheme.Light.Surface);
    }

    [Fact]
    public void ScaleTokens_AreMonotonic()
    {
        HamonTheme t = HamonTheme.Dark;
        Assert.True(t.SpacingXs < t.SpacingS && t.SpacingS < t.SpacingM && t.SpacingM < t.SpacingL && t.SpacingL < t.SpacingXl);
        Assert.True(t.TextCaption < t.TextLabel && t.TextLabel < t.TextBody && t.TextBody < t.TextTitle && t.TextTitle < t.TextHeadline);
        Assert.True(t.HoverOverlay < t.FocusOverlay && t.FocusOverlay < t.PressedOverlay);
    }
}
