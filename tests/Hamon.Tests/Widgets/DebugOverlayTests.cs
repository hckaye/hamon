using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of DebugOverlay (drawing runtime statistics).</summary>
public class DebugOverlayTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class TextRecorder : ITextRenderer
    {
        public List<string> Drawn { get; } = new();

        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color) => Drawn.Add(text);
    }

    [Fact]
    public void DrawsRuntimeStats()
    {
        var renderer = new TextRecorder();
        var host = new HamonRoot(renderer);
        host.SetRoot(() => new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                new Focus { Node = new FocusNode { Id = 3 }, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) } },
                new Align { Alignment = Alignment.TopLeft, Child = new DebugOverlay() },
            },
        });
        host.Update(new Size(200, 200), 0.016f);

        host.Render(new StubPainter());

        Assert.Contains(renderer.Drawn, s => s.StartsWith("elements:"));
        Assert.Contains(renderer.Drawn, s => s.StartsWith("tickers:"));
        Assert.Contains(renderer.Drawn, s => s.StartsWith("overlays:"));
        Assert.Contains(renderer.Drawn, s => s.Contains("id=3")); // フォーカス中ノード
        Assert.Contains(renderer.Drawn, s => s.StartsWith("fps:"));
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
    }
}
