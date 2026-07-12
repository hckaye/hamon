using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic testing of ScrollView (viewport clamp, content unbounded measurement, offset placement, drag/controller).</summary>
public class ScrollViewTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    // Build 呼び出し回数を数える透過ウィジェット（スクロールで再構築されないことの検証用）。
    private sealed class BuildCounter : StatelessWidget
    {
        private readonly int[] _count;
        private readonly Widget _child;

        public BuildCounter(int[] count, Widget child)
        {
            _count = count;
            _child = child;
        }

        public override Widget Build(BuildContext context)
        {
            _count[0]++;
            return _child;
        }
    }

    // 主軸に積み上がる固定高の行列（content > viewport を作る）。
    private static Widget Rows(int count, float rowHeight) => new Column
    {
        Children = MakeRows(count, rowHeight),
    };

    private static Widget[] MakeRows(int count, float rowHeight)
    {
        var rows = new Widget[count];
        for (int i = 0; i < count; i++)
        {
            rows[i] = new SizedBox { Height = Dimension.Px(rowHeight) };
        }

        return rows;
    }

    private static HamonRoot Mount(ScrollController controller, out Element scrollElement)
    {
        var host = new HamonRoot(new StubTextRenderer());
        // ルート直下は Tight 制約で固定サイズが上書きされるため、Column の子（緩い制約）として置く。
        host.SetRoot(() => new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new ScrollView
                {
                    Controller = controller,
                    Height = Dimension.Px(100),
                    Width = Dimension.Px(80),
                    Child = Rows(10, 30f), // content 高 = 300 > viewport 100
                },
            },
        });
        host.Update(new Size(400, 400));
        scrollElement = host.Root!.Children[0];
        return host;
    }

    [Fact]
    public void Viewport_ClampsToConstraint_ContentMeasuredUnbounded()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, out Element scroll);

        // viewport は固定 80x100
        Assert.Equal(80f, scroll.LayoutNode.Bounds.Width);
        Assert.Equal(100f, scroll.LayoutNode.Bounds.Height);

        // content（子）は主軸 unbounded で測られ 300 高、交差軸は viewport 幅に揃う
        LayoutNode content = scroll.LayoutNode.Children[0];
        Assert.Equal(300f, content.Size.Height);
        Assert.Equal(80f, content.Size.Width);
    }

    [Fact]
    public void JumpTo_OffsetsContentUp()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, out Element scroll);

        Assert.Equal(0f, scroll.LayoutNode.Children[0].Bounds.Y); // 先頭

        controller.JumpTo(50f);
        host.Update(new Size(400, 400));

        Assert.Equal(50f, controller.Offset);
        Assert.Equal(-50f, scroll.LayoutNode.Children[0].Bounds.Y); // 50px 上へ
    }

    [Fact]
    public void Scroll_DoesNotRebuildChildren()
    {
        // スクロールは全ツリー再構築（MarkDirty）ではなく部分木の再レイアウトで反映される＝Build を回さない（ゼロアロケ）。
        // 重いページ（ダッシュボード等）の慣性スクロールがカクつかないことの回帰防止。
        var controller = new ScrollController();
        int[] builds = { 0 };
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new ScrollView
                {
                    Controller = controller,
                    Height = Dimension.Px(100),
                    Width = Dimension.Px(80),
                    Child = new BuildCounter(builds, Rows(10, 30f)),
                },
            },
        });
        host.Update(new Size(400, 400));
        int initial = builds[0];
        Assert.True(initial > 0);

        controller.JumpTo(50f);
        host.Update(new Size(400, 400));

        Assert.Equal(50f, controller.Offset); // スクロールは反映される
        Assert.Equal(initial, builds[0]);     // 子ウィジェットは再構築されない
    }

    private static HamonRoot MountManual(bool manualScroll, ScrollController controller)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new ScrollView
                {
                    Controller = controller,
                    ManualScroll = manualScroll,
                    Height = Dimension.Px(100),
                    Width = Dimension.Px(80),
                    Child = Rows(10, 30f), // content 高 = 300 > viewport 100
                },
            },
        });
        host.Update(new Size(400, 400));
        return host;
    }

    private static void Settle(HamonRoot host)
    {
        for (int i = 0; i < 30; i++)
        {
            host.Update(new Size(400, 400), 0.05f); // 慣性/グライドのティッカーを十分に進める
        }
    }

    [Fact]
    public void ManualScroll_False_WheelDoesNotScroll()
    {
        var controller = new ScrollController();
        HamonRoot host = MountManual(false, controller);

        host.DispatchScroll(new Vec2(40f, 50f), -30f); // ビューポート内で下方向ホイール（有効なら offset 増）
        Settle(host);

        Assert.Equal(0f, controller.Offset); // 手動無効＝ホイールで動かない
    }

    [Fact]
    public void ManualScroll_False_DragDoesNotScroll()
    {
        var controller = new ScrollController();
        HamonRoot host = MountManual(false, controller);

        host.DispatchPointer(new PointerEvent(new Vec2(40f, 80f), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(40f, 20f), PointerPhase.Move, 0.1f)); // 上へ 60px ドラッグ
        host.DispatchPointer(new PointerEvent(new Vec2(40f, 20f), PointerPhase.Up, 0.2f));
        Settle(host);

        Assert.Equal(0f, controller.Offset); // 手動無効＝ドラッグで動かない
    }

    [Fact]
    public void ManualScroll_False_ProgrammaticStillWorks()
    {
        var controller = new ScrollController();
        HamonRoot host = MountManual(false, controller);

        controller.JumpTo(50f);
        host.Update(new Size(400, 400));

        Assert.Equal(50f, controller.Offset); // プログラム制御（JumpTo）は引き続き効く
    }

    [Fact]
    public void ManualScroll_True_WheelScrolls()
    {
        var controller = new ScrollController();
        HamonRoot host = MountManual(true, controller);

        host.DispatchScroll(new Vec2(40f, 50f), -30f); // 下方向ホイール → offset 増
        Settle(host);

        Assert.True(controller.Offset > 0f, $"既定（手動有効）はホイールで動く: {controller.Offset}");
    }

    [Fact]
    public void JumpTo_BeyondMax_Clamps()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, out _);

        controller.JumpTo(9999f);
        // max = content(300) - viewport(100) = 200
        Assert.Equal(200f, controller.Offset);

        controller.JumpTo(-50f);
        Assert.Equal(0f, controller.Offset);
    }

    [Fact]
    public void Wheel_ScrollsScrollableUnderPointer_WithMomentum()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, out _);
        var viewport = new Size(400, 400);

        // ScrollView は (0,0,80,100)・content 300。ホイールは速度を足す慣性＝即時には飛ばず、数フレームで進む。
        host.DispatchScroll(new Vec2(10f, 50f), -30f); // 下方向の速度
        Assert.Equal(0f, controller.Offset);            // この時点ではまだ動いていない（速度のみ）

        for (int i = 0; i < 5; i++)
        {
            host.Update(viewport, 0.05f);
        }

        Assert.True(controller.Offset > 0f, $"offset={controller.Offset}"); // 慣性で下へ進んだ

        // 整定させて停止位置を得る。
        for (int i = 0; i < 60; i++)
        {
            host.Update(viewport, 0.05f);
        }

        float settled = controller.Offset;
        Assert.True(settled > 0f);

        // 逆方向（上）の強い速度 → 先頭（0）へ戻る。
        host.DispatchScroll(new Vec2(10f, 50f), 2000f);
        for (int i = 0; i < 60; i++)
        {
            host.Update(viewport, 0.05f);
        }

        Assert.True(controller.Offset < settled, $"after up offset={controller.Offset}, settled={settled}");
    }

    [Fact]
    public void Drag_PastTopEdge_OverscrollsThenSpringsBack()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, out _);
        var viewport = new Size(400, 400);
        var scroll = (ScrollViewElement)host.Root!.Children[0];

        // 先頭(offset 0)で下方向に引っ張る（指を下へ＝main 増）→ 上端をオーバースクロール（負）。
        host.DispatchPointer(new PointerEvent(new Vec2(10f, 20f), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(10f, 120f), PointerPhase.Move, 0.05f));
        Assert.True(scroll.Overscroll < 0f, $"overscroll={scroll.Overscroll}"); // ゴムバンドで引っ張れている
        Assert.Equal(0f, controller.Offset);                                    // offset 自体は端でクランプ

        host.DispatchPointer(new PointerEvent(new Vec2(10f, 120f), PointerPhase.Up, 0.06f));
        for (int i = 0; i < 60; i++)
        {
            host.Update(viewport, 0.05f);
        }

        Assert.Equal(0f, scroll.Overscroll, 0.5f); // 指数バネで境界へ復帰
    }

    [Fact]
    public void Bounce_Disabled_NoOverscroll()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new ScrollView { Bounce = false, Height = Dimension.Px(100), Width = Dimension.Px(80), Child = Rows(10, 30f) },
            },
        });
        host.Update(new Size(400, 400));
        var scroll = (ScrollViewElement)host.Root!.Children[0];

        host.DispatchPointer(new PointerEvent(new Vec2(10f, 20f), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(10f, 120f), PointerPhase.Move, 0.05f));
        Assert.Equal(0f, scroll.Overscroll); // Bounce=false ではオーバースクロールしない
    }

    [Fact]
    public void Drag_ScrollsByPointerDelta()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, out _);

        // 上方向ドラッグ（y 80→20＝指を上へ）→ 下へスクロール（offset 増）
        host.DispatchPointer(new PointerEvent(new Vec2(10, 80), PointerPhase.Down));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 20), PointerPhase.Move));
        Assert.Equal(60f, controller.Offset); // 80-20=60

        host.DispatchPointer(new PointerEvent(new Vec2(10, 20), PointerPhase.Up));
    }

    [Fact]
    public void Drag_ClampsAtTop()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, out _);

        // 下方向ドラッグ（既に先頭なので上限 0 でクランプ）
        host.DispatchPointer(new PointerEvent(new Vec2(10, 20), PointerPhase.Down));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 90), PointerPhase.Move));
        Assert.Equal(0f, controller.Offset);
    }

    [Fact]
    public void ScrollOffset_SurvivesRebuild()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, out Element scroll);

        controller.JumpTo(70f);
        host.Update(new Size(400, 400));
        Assert.Equal(70f, controller.Offset);

        // 状態変化で再構築されてもスクロール量は保たれる
        host.Update(new Size(400, 400));
        Assert.Equal(70f, controller.Offset);
        Assert.Equal(-70f, scroll.LayoutNode.Children[0].Bounds.Y);
    }
}
