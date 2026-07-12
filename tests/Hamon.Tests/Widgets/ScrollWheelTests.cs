using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Deterministic testing of mouse wheel/trackpad continuous scrolling: continuous (no inertia), rubber bands at edges,
/// Pull-and-hold/bounce opt-out.
/// </summary>
public class ScrollWheelTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    // 量を厳密に検証するため感度 1.0（入力 px をそのまま目標へ）に固定。動きの調整可能性は別テストで確認。
    private static readonly ScrollPhysics UnitPhysics = new() { WheelSensitivity = 1f };

    private static (HamonRoot host, ListViewElement list) Mount(bool bounce = true, ScrollPhysics? physics = null)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = 100,
            ItemExtent = 40f, // content = 4000, viewport 200 → max 3800
            Bounce = bounce,
            Physics = physics ?? UnitPhysics,
            Builder = i => new SizedBox { Height = Dimension.Px(40) },
        });
        host.Update(Viewport);
        return (host, (ListViewElement)host.Root!);
    }

    private static void Settle(HamonRoot host)
    {
        for (int i = 0; i < 80; i++)
        {
            host.Update(Viewport, 0.05f);
        }
    }

    [Fact]
    public void Wheel_AccumulatesToTarget_NoMomentumOvershoot()
    {
        (HamonRoot host, ListViewElement list) = Mount();
        host.DispatchScroll(new Vec2(100, 100), -100f);
        host.DispatchScroll(new Vec2(100, 100), -100f);
        host.DispatchScroll(new Vec2(100, 100), -100f);
        Settle(host);
        Assert.Equal(300f, list.ScrollOffset, 1f); // 入力合計でぴったり停止＝行き過ぎない
    }

    [Fact]
    public void Wheel_StopsWhenInputStops()
    {
        (HamonRoot host, ListViewElement list) = Mount();
        host.DispatchScroll(new Vec2(100, 100), -200f);
        Settle(host);
        float settled = list.ScrollOffset;
        Assert.Equal(200f, settled, 1f);
        host.Update(Viewport, 0.5f);
        Assert.Equal(settled, list.ScrollOffset, 0.01f); // 慣性で滑り続けない
    }

    [Fact]
    public void Wheel_GlidesContinuously_NotInstantJump()
    {
        (HamonRoot host, ListViewElement list) = Mount();
        host.DispatchScroll(new Vec2(100, 100), -400f);
        host.Update(Viewport, 0.016f);
        Assert.True(list.ScrollOffset > 0f && list.ScrollOffset < 400f, $"offset={list.ScrollOffset}");
    }

    [Fact]
    public void Wheel_OverscrollAtTop_RubberBandsThenSpringsBack()
    {
        (HamonRoot host, ListViewElement list) = Mount();
        Assert.Equal(0f, list.ScrollOffset); // 先頭

        // 先頭でさらに上へ（delta 正＝上ホイール）＝オーバースクロール。
        host.DispatchScroll(new Vec2(100, 100), 300f);
        for (int i = 0; i < 3; i++)
        {
            host.Update(Viewport, 0.016f);
        }

        Assert.True(list.Overscroll < -1f, $"overscroll={list.Overscroll}"); // 上端を越えてゴムバンド変位（負）

        Settle(host); // 入力が止まればバネで戻る
        Assert.Equal(0f, list.Overscroll, 0.5f);
        Assert.Equal(0f, list.ScrollOffset, 0.5f);
    }

    [Fact]
    public void Wheel_PullAndHold_KeepsOverscrollWhileInputContinues()
    {
        (HamonRoot host, ListViewElement list) = Mount();

        // 先頭で連続的に上へ引っ張り続ける（トラックパッド二本指相当）。各フレーム入力＝ホールド。
        for (int i = 0; i < 12; i++)
        {
            host.DispatchScroll(new Vec2(100, 100), 40f);
            host.Update(Viewport, 0.016f);
        }

        Assert.True(list.Overscroll < -10f, $"held overscroll={list.Overscroll}"); // 引っ張ったまま静止保持
    }

    [Fact]
    public void Wheel_Bounce_OptOut_NoOverscroll()
    {
        (HamonRoot host, ListViewElement list) = Mount(bounce: false);

        host.DispatchScroll(new Vec2(100, 100), 300f); // 先頭で上へ
        for (int i = 0; i < 5; i++)
        {
            host.Update(Viewport, 0.016f);
        }

        Assert.Equal(0f, list.Overscroll, 0.001f); // opt-out＝ゴムバンドしない
        Assert.Equal(0f, list.ScrollOffset, 0.5f);
    }

    [Fact]
    public void WheelSensitivity_ScalesMovement()
    {
        // 同じ入力でも感度が小さいほど移動量が減る（鈍くなる）。
        (HamonRoot host1, ListViewElement list1) = Mount(physics: new ScrollPhysics { WheelSensitivity = 1f });
        host1.DispatchScroll(new Vec2(100, 100), -200f);
        Settle(host1);

        (HamonRoot host2, ListViewElement list2) = Mount(physics: new ScrollPhysics { WheelSensitivity = 0.5f });
        host2.DispatchScroll(new Vec2(100, 100), -200f);
        Settle(host2);

        Assert.Equal(200f, list1.ScrollOffset, 1f);   // 感度 1.0 → 200
        Assert.Equal(100f, list2.ScrollOffset, 1f);   // 感度 0.5 → 100（鈍い）
    }

    [Fact]
    public void ThemeScrollPhysics_AppliesWhenWidgetUnset()
    {
        // ウィジェットで Physics 未指定なら、テーマの ScrollPhysics が使われる。
        var host = new HamonRoot(new StubTextRenderer())
        {
            Theme = new HamonTheme { ScrollPhysics = new ScrollPhysics { WheelSensitivity = 0.25f } },
        };
        host.SetRoot(() => new ListView
        {
            ItemCount = 100,
            ItemExtent = 40f,
            Builder = i => new SizedBox { Height = Dimension.Px(40) },
        });
        host.Update(Viewport);
        var list = (ListViewElement)host.Root!;

        host.DispatchScroll(new Vec2(100, 100), -400f);
        Settle(host);
        Assert.Equal(100f, list.ScrollOffset, 1f); // 400 × 0.25 = 100
    }
}
