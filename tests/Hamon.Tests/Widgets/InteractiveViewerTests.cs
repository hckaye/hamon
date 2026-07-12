using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// <see cref="InteractiveViewer"/>Determinism test (while panning/zooming the child remains interactive).
/// Hit judgment (<see cref="Element.ChildHitTestTransform"/>Verify that the inverse transformation (via) and pointer delivery match what is displayed.
/// </summary>
public class InteractiveViewerTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    // ビューポートを充填する Stack に、レイアウト座標 (10,10)-(50,50) の 40×40 ボタンを1つ置く。
    private static HamonRoot Mount(InteractiveViewerController controller, Action onTap)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new InteractiveViewer
        {
            Controller = controller,
            Child = new Stack
            {
                Fit = StackFit.Expand,
                Children =
                [
                    new Positioned
                    {
                        Left = Dimension.Px(10),
                        Top = Dimension.Px(10),
                        Child = new Button { OnPressed = onTap, Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) } },
                    },
                ],
            },
        });
        host.Update(Viewport);
        return host;
    }

    private static void Tap(HamonRoot host, Vec2 at, float t)
    {
        host.DispatchPointer(new PointerEvent(at, PointerPhase.Down, t));
        host.DispatchPointer(new PointerEvent(at, PointerPhase.Up, t + 0.005f));
    }

    [Fact]
    public void Identity_Tap_HitsChild()
    {
        int taps = 0;
        var controller = new InteractiveViewerController();
        HamonRoot host = Mount(controller, () => taps++);

        Tap(host, new Vec2(30, 30), 0f); // ボタン中心（10..50）
        Assert.Equal(1, taps);
    }

    [Fact]
    public void Zoomed_Tap_HitsAtTransformedLocation()
    {
        int taps = 0;
        var controller = new InteractiveViewerController();
        HamonRoot host = Mount(controller, () => taps++);

        // 原点支点に 2倍ズーム＝scale 2/translate 0。ボタン表示は (20..100)、中心 (60,60)。
        controller.ZoomAbout(new Vec2(0, 0), 2f);
        host.Update(Viewport);

        Tap(host, new Vec2(60, 60), 0.1f); // 表示中心 → 逆変換で (30,30) → ヒット
        Assert.Equal(1, taps);

        Tap(host, new Vec2(90, 90), 0.2f); // 表示右下寄り → 逆変換 (45,45)（10..50 内）→ ヒット
        Assert.Equal(2, taps);

        Tap(host, new Vec2(150, 150), 0.3f); // 表示外（最大 100）→ 逆変換 (75,75) は範囲外 → ヒットしない
        Assert.Equal(2, taps);
    }

    [Fact]
    public void Pan_MovesContent_HitTestFollows()
    {
        int taps = 0;
        var controller = new InteractiveViewerController();
        HamonRoot host = Mount(controller, () => taps++);

        // 空き領域から +30 右へドラッグ＝コンテンツが右へ 30 動く（背面 GestureDetector がパンを受ける）。
        host.DispatchPointer(new PointerEvent(new Vec2(120, 120), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(150, 120), PointerPhase.Move, 0.02f)); // +30 >slop
        host.DispatchPointer(new PointerEvent(new Vec2(150, 120), PointerPhase.Up, 0.03f));
        host.Update(Viewport);

        Tap(host, new Vec2(60, 30), 0.1f); // ボタン中心 (30,30) → +30 で (60,30) へ移動 → ヒット
        Assert.Equal(1, taps);
    }

    [Fact]
    public void ZoomIn_ZoomOut_Reset_AdjustScale()
    {
        var controller = new InteractiveViewerController();
        HamonRoot host = Mount(controller, () => { });

        Assert.Equal(1f, controller.Scale, 0.001f);

        controller.ZoomIn(2f); // ビューポート中心支点に 2倍
        host.Update(Viewport);
        Assert.Equal(2f, controller.Scale, 0.001f);

        controller.ZoomOut(2f);
        host.Update(Viewport);
        Assert.Equal(1f, controller.Scale, 0.001f);

        controller.ZoomIn(100f); // 既定上限 MaxScale=4 でクランプ
        host.Update(Viewport);
        Assert.Equal(4f, controller.Scale, 0.001f);

        controller.Reset();
        host.Update(Viewport);
        Assert.Equal(1f, controller.Scale, 0.001f);
    }
}
