using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Testing focus appearance customization (cursor renderer replacement, scale, individual decoration).</summary>
public class FocusVisualTests
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
        public List<Rect> RoundedRects { get; } = new();

        public List<Rect> Rects { get; } = new();

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color) => Rects.Add(rect);

        public void FillRoundedRect(Rect rect, Color color, float radius) => RoundedRects.Add(rect);

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    [Fact]
    public void CustomRenderer_IsInvoked_WhenCursorEnabled()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.Cursor.Enabled = true;
        Rect? drawn = null;
        float seenPulse = -1f;
        host.Cursor.Renderer = (ctx, rect, pulse) =>
        {
            drawn = rect;
            seenPulse = pulse;
        };
        host.SetRoot(() => new Focus { Node = new FocusNode(), Autofocus = true, Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) } });
        host.Update(Viewport);

        host.Render(new RecordingPainter());
        Assert.NotNull(drawn); // カスタム描画が呼ばれた
        Assert.True(seenPulse >= 0f);
    }

    [Fact]
    public void CursorScale_GrowsCursorRect()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.Cursor.Enabled = true;
        host.Cursor.Padding = 0f;
        host.Cursor.Scale = 2f;
        Rect drawn = default;
        host.Cursor.Renderer = (ctx, rect, pulse) => drawn = rect;
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Focus { Node = new FocusNode(), Autofocus = true, Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) } },
        });
        host.Update(Viewport);
        host.Render(new RecordingPainter());

        // 40x40 を 2 倍 → 80x80、中心固定で左上 (-20,-20)。
        Assert.Equal(80f, drawn.Width, 0.5f);
        Assert.Equal(80f, drawn.Height, 0.5f);
    }

    [Fact]
    public void FocusDecoration_DrawsBackground_WhenFocused()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var painter = new RecordingPainter();
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Focus
            {
                Node = new FocusNode(),
                Autofocus = true,
                Decoration = new FocusDecoration(new Color(255, 0, 0), 2f, new Color(0, 0, 255), 6f),
                Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) },
            },
        });
        host.Update(Viewport);
        host.Render(painter);

        // Decoration 背景は FillRoundedRect で描かれる（40x40 の矩形が含まれる）。
        Assert.Contains(painter.RoundedRects, r => r.Width is >= 39f and <= 41f && r.Height is >= 39f and <= 41f);
    }
}
