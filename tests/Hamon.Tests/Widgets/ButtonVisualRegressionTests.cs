using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Draw regression test for Button focused/hovered/pressed/disabled (via draw-command).
/// Verify that opacity is ordered as expected and that disabled does not produce overlapping colors (pixel-free regression).
/// </summary>
public class ButtonVisualRegressionTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class RRectRecorder : IPainter
    {
        public List<Color> RoundedFills { get; } = new();

        public void BeginFrame() => RoundedFills.Clear();

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

    private static readonly Color Bg = new(40, 44, 54);
    private static readonly Size Viewport = new(120, 120);

    // 状態へ駆動し、アニメを落ち着かせて、白いステートレイヤー（暗い背景なので白）の最大アルファを返す。
    private static int OverlayAlpha(System.Action<HamonRoot> drive)
    {
        var host = new HamonRoot(new StubTextRenderer());
        var node = new FocusNode();
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Button { Node = node, OnPressed = () => { }, Background = Bg, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } },
        });
        host.Update(Viewport);

        drive(host);

        for (int i = 0; i < 30; i++)
        {
            host.Update(Viewport, 0.1f); // ステートレイヤーアニメを十分に進める
        }

        var painter = new RRectRecorder();
        host.Render(painter);

        int maxWhiteAlpha = 0;
        foreach (Color c in painter.RoundedFills)
        {
            if (c.R == 255 && c.G == 255 && c.B == 255 && c.A > maxWhiteAlpha)
            {
                maxWhiteAlpha = c.A;
            }
        }

        return maxWhiteAlpha;
    }

    [Fact]
    public void OverlayAlpha_OrderedByState()
    {
        int idle = OverlayAlpha(_ => { });
        int hovered = OverlayAlpha(h => h.DispatchHover(new Vec2(50, 50)));
        int focused = OverlayAlpha(h => h.MoveFocus(FocusDirection.Down));
        int pressed = OverlayAlpha(h => h.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f)));

        Assert.Equal(0, idle);             // 平常時はステートレイヤーなし
        Assert.True(hovered > idle, $"hover={hovered}");
        Assert.True(focused > hovered, $"focus={focused} hover={hovered}");
        Assert.True(pressed > focused, $"press={pressed} focus={focused}");
    }

    [Fact]
    public void PressedOpacity_DimsButton()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Button
            {
                OnPressed = () => { },
                Background = Bg,
                Style = new ButtonStyle { Animation = new ButtonAnimationStyle { PressedOpacity = 0.5f } },
                Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
            },
        });
        host.Update(Viewport);
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        for (int i = 0; i < 30; i++)
        {
            host.Update(Viewport, 0.1f); // 不透明度アニメを落ち着かせる
        }

        var painter = new RRectRecorder();
        host.Render(painter);

        // 背景（40,44,54）の矩形が不透明度 0.5 で描かれる＝アルファが下がる。
        Assert.Contains(painter.RoundedFills, c => c.R == Bg.R && c.G == Bg.G && c.B == Bg.B && c.A < 200);
    }

    [Fact]
    public void CurvedAnimation_TransitionsOverDuration()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Button
            {
                OnPressed = () => { },
                Background = Bg,
                Style = new ButtonStyle { Animation = new ButtonAnimationStyle { Curve = Curves.Linear, Duration = 1.0f } },
                Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
            },
        });
        host.Update(Viewport);
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f)); // pressed へ

        host.Update(Viewport, 0.5f); // 半分の時間
        int mid = WhiteAlpha(host);

        for (int i = 0; i < 10; i++)
        {
            host.Update(Viewport, 0.2f); // 残りを十分進める
        }

        int full = WhiteAlpha(host);
        Assert.True(mid > 0 && mid < full, $"mid={mid} full={full}"); // 途中は部分的、最後に到達
    }

    private static int WhiteAlpha(HamonRoot host)
    {
        var painter = new RRectRecorder();
        host.Render(painter);
        int a = 0;
        foreach (Color c in painter.RoundedFills)
        {
            if (c.R == 255 && c.G == 255 && c.B == 255 && c.A > a)
            {
                a = c.A;
            }
        }

        return a;
    }

    [Fact]
    public void Disabled_NoStateLayer()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Button { Enabled = false, OnPressed = () => { }, Background = Bg, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } },
        });
        host.Update(Viewport);
        host.DispatchHover(new Vec2(50, 50)); // 無効でも hover しても

        var painter = new RRectRecorder();
        host.Render(painter);
        Assert.DoesNotContain(painter.RoundedFills, c => c.R == 255 && c.G == 255 && c.B == 255 && c.A > 0); // 白い重ね色は出ない
    }
}
