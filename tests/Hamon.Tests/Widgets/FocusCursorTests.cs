using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test of focus cursor (single overlay = moving animation sliding from previous frame to next frame).</summary>
public class FocusCursorTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 100);

    // 横並びの2ボタン（各 100x40）：A=(0,0,100,40) 自動フォーカス、B=(100,0,100,40)。
    private static (HamonRoot host, FocusNode a, FocusNode b) MountTwoButtons(bool enableCursor)
    {
        var a = new FocusNode();
        var b = new FocusNode();
        var host = new HamonRoot(new StubTextRenderer());
        if (enableCursor)
        {
            host.Cursor.Enabled = true;
            host.Cursor.Padding = 0f;
            host.Cursor.GlideDuration = 0.1f;
            host.Cursor.Curve = Curves.Linear;
        }

        host.SetRoot(() => new Row
        {
            Children = new Widget[]
            {
                new Button { Node = a, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(40) } },
                new Button { Node = b, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(40) } },
            },
        });
        host.Update(Viewport);
        return (host, a, b);
    }

    [Fact]
    public void Disabled_NoCursorRect()
    {
        (HamonRoot host, FocusNode a, FocusNode b) = MountTwoButtons(enableCursor: false);
        Assert.Null(host.FocusCursorRect);
    }

    [Fact]
    public void Enabled_SnapsToFirstFocusedRect()
    {
        (HamonRoot host, FocusNode a, FocusNode b) = MountTwoButtons(enableCursor: true);

        Assert.True(a.HasFocus);
        Rect cursor = host.FocusCursorRect!.Value;
        Assert.Equal(0f, cursor.X, 0.5f);
        Assert.Equal(100f, cursor.Width, 0.5f);
        Assert.Equal(40f, cursor.Height, 0.5f);
    }

    [Fact]
    public void MoveFocus_GlidesFromOldToNewRect()
    {
        (HamonRoot host, FocusNode a, FocusNode b) = MountTwoButtons(enableCursor: true);

        host.MoveFocus(FocusDirection.Right); // A→B
        host.Update(Viewport, 0f);            // 変化検知＝グライド開始（t=0）
        Assert.True(b.HasFocus);
        Assert.Equal(0f, host.FocusCursorRect!.Value.X, 1f); // まだ A の位置

        host.Update(Viewport, 0.05f); // 半分（0.05/0.1）
        float midX = host.FocusCursorRect!.Value.X;
        Assert.InRange(midX, 10f, 90f); // A と B の途中

        host.Update(Viewport, 0.1f); // 到達
        Assert.Equal(100f, host.FocusCursorRect!.Value.X, 1f); // B の位置
    }

    [Fact]
    public void SameNode_TracksLiveBounds_NoGlideArtifacts()
    {
        (HamonRoot host, FocusNode a, FocusNode b) = MountTwoButtons(enableCursor: true);

        // 同じノードにフォーカスし続ける限り、カーソルはその枠にぴったり。
        host.Update(Viewport, 0.2f);
        Rect cursor = host.FocusCursorRect!.Value;
        Assert.Equal(0f, cursor.X, 0.5f);
        Assert.Equal(100f, cursor.Width, 0.5f);
    }
}
