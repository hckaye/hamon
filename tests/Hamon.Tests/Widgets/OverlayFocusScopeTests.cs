using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for overlay (push/remove/front-most hit test) and FocusScope (focus trap).</summary>
public class OverlayFocusScopeTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static Widget FocusBox(FocusNode node) => new Focus
    {
        Node = node,
        Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) },
    };

    [Fact]
    public void PushOverlay_ThenRemove_TogglesPresence()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new SizedBox { Width = Dimension.Px(10), Height = Dimension.Px(10) });
        host.Update(new Size(200, 200));
        Assert.Equal(0, host.OverlayCount);

        OverlayEntry entry = host.PushOverlay(() => new SizedBox());
        host.Update(new Size(200, 200));
        Assert.Equal(1, host.OverlayCount);

        host.RemoveOverlay(entry);
        host.Update(new Size(200, 200));
        Assert.Equal(0, host.OverlayCount);
    }

    [Fact]
    public void Overlay_ScrimCatchesPointer_OverBaseApp()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int baseTaps = 0;
        int scrimTaps = 0;
        host.SetRoot(() => new GestureDetector
        {
            OnTap = () => baseTaps++,
            Child = new SizedBox { Width = Dimension.Px(200), Height = Dimension.Px(200) },
        });
        host.Update(new Size(200, 200));

        // 基底をタップ → 基底が受ける
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up));
        Assert.Equal(1, baseTaps);

        // 全画面 scrim のオーバーレイを出す
        host.PushOverlay(() => new GestureDetector
        {
            OnTap = () => scrimTaps++,
            Child = new SizedBox { Width = Dimension.Px(200), Height = Dimension.Px(200) },
        });
        host.Update(new Size(200, 200));

        // 同じ位置をタップ → 最前面の scrim が受け、基底へは届かない
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up));
        Assert.Equal(1, baseTaps); // 増えない
        Assert.Equal(1, scrimTaps);
    }

    [Fact]
    public void FocusScope_Trap_LimitsDirectionalMoveToScope()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var outside = new FocusNode();
        var inA = new FocusNode();
        var inB = new FocusNode();

        host.SetRoot(() => new Column
        {
            Children = new Widget[]
            {
                FocusBox(outside),
                new FocusScope
                {
                    Child = new Column
                    {
                        Children = new Widget[] { FocusBox(inA), FocusBox(inB) },
                    },
                },
            },
        });
        host.Update(new Size(200, 400));

        // トラップにより最初の登録（スコープ内 inA）へ引き込まれている
        Assert.Same(inA, host.Focus.Focused);

        // 下方向移動はスコープ内の inB まで（outside へは出ない）
        Assert.True(host.MoveFocus(FocusDirection.Down));
        Assert.Same(inB, host.Focus.Focused);

        // さらに下：スコープ内に候補が無いので移動しない（外へ漏れない）
        Assert.False(host.MoveFocus(FocusDirection.Down));
        Assert.Same(inB, host.Focus.Focused);

        // 上方向も inA まで（outside へは出ない）
        Assert.True(host.MoveFocus(FocusDirection.Up));
        Assert.Same(inA, host.Focus.Focused);
        Assert.False(host.MoveFocus(FocusDirection.Up));
        Assert.Same(inA, host.Focus.Focused);
    }

    [Fact]
    public void FocusScope_NonTrap_DoesNotLimitMovement()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var outside = new FocusNode();
        var inside = new FocusNode();

        host.SetRoot(() => new Column
        {
            Children = new Widget[]
            {
                FocusBox(outside),
                new FocusScope
                {
                    Trap = false,
                    Child = FocusBox(inside),
                },
            },
        });
        host.Update(new Size(200, 400));

        host.Focus.RequestFocus(inside);
        Assert.True(host.MoveFocus(FocusDirection.Up));
        Assert.Same(outside, host.Focus.Focused); // 非トラップなら外へ出られる
    }

    [Fact]
    public void FocusScope_PopRestoresMovementOutside()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var outside = new FocusNode();
        var inside = new FocusNode();
        OverlayEntry? entry = null;

        host.SetRoot(() => FocusBox(outside));
        host.Update(new Size(200, 400));
        host.Focus.RequestFocus(outside);

        // モーダルを開く（FocusScope トラップ）
        entry = host.PushOverlay(() => new FocusScope { Child = FocusBox(inside) });
        host.Update(new Size(200, 400));
        Assert.Same(inside, host.Focus.Focused); // 引き込み
        Assert.False(host.MoveFocus(FocusDirection.Up)); // 外へ出られない

        // モーダルを閉じる → トラップ解除
        host.RemoveOverlay(entry);
        host.Update(new Size(200, 400));

        // フォーカスは外（残った唯一の候補）へ戻る
        Assert.Same(outside, host.Focus.Focused);
    }

    [Fact]
    public void Trap_BlocksRequestFocusToOutsideNode()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var outside = new FocusNode();
        var inside = new FocusNode();

        host.SetRoot(() => FocusBox(outside));
        host.Update(new Size(200, 400));

        OverlayEntry entry = host.PushOverlay(() => new FocusScope { Child = FocusBox(inside) });
        host.Update(new Size(200, 400));
        Assert.Same(inside, host.Focus.Focused);

        // モーダル中に外側ノードへ RequestFocus（タップ相当）してもトラップで弾かれる
        host.Focus.RequestFocus(outside);
        Assert.Same(inside, host.Focus.Focused);

        // 閉じれば再び外側へ移せる
        host.RemoveOverlay(entry);
        host.Update(new Size(200, 400));
        host.Focus.RequestFocus(outside);
        Assert.Same(outside, host.Focus.Focused);
    }

    [Fact]
    public void NestedTraps_ConfineToTopmost_AndRestoreOnPop()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var baseNode = new FocusNode();
        var modalA = new FocusNode();
        var modalB = new FocusNode();

        host.SetRoot(() => FocusBox(baseNode));
        host.Update(new Size(200, 400));
        host.Focus.RequestFocus(baseNode);

        // モーダル A を開く → A へ引き込み
        OverlayEntry entryA = host.PushOverlay(() => new FocusScope { Child = FocusBox(modalA) });
        host.Update(new Size(200, 400));
        Assert.Same(modalA, host.Focus.Focused);

        // モーダル B を A の上に重ねる → B へ引き込み、A/base へは移せない
        OverlayEntry entryB = host.PushOverlay(() => new FocusScope { Child = FocusBox(modalB) });
        host.Update(new Size(200, 400));
        Assert.Same(modalB, host.Focus.Focused);
        host.Focus.RequestFocus(modalA);
        Assert.Same(modalB, host.Focus.Focused); // 一段下のトラップへも漏れない

        // B を閉じる → A のトラップへ戻る（base へは戻らない）
        host.RemoveOverlay(entryB);
        host.Update(new Size(200, 400));
        Assert.Same(modalA, host.Focus.Focused);
        host.Focus.RequestFocus(baseNode);
        Assert.Same(modalA, host.Focus.Focused); // まだ A のトラップ内

        // A を閉じる → base へ戻る
        host.RemoveOverlay(entryA);
        host.Update(new Size(200, 400));
        Assert.Same(baseNode, host.Focus.Focused);
    }
}
