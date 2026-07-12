using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Dropdown (open/close/item selection/scrim close) determinism test.</summary>
public class DropdownTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 400);

    private static (HamonRoot host, DropdownElement<int> el) Mount(Dropdown<int> dd)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = dd });
        host.Update(Viewport);
        return (host, (DropdownElement<int>)host.Root!.Children[0]);
    }

    private static Dropdown<int> Make(Action<int> onChanged, int value = 1) => new()
    {
        Value = value,
        OnChanged = onChanged,
        Items = new[]
        {
            new DropdownItem<int>(1, new SizedBox { Width = Dimension.Px(120), Height = Dimension.Px(24) }),
            new DropdownItem<int>(2, new SizedBox { Width = Dimension.Px(120), Height = Dimension.Px(24) }),
        },
    };

    [Fact]
    public void Tap_OpensMenu()
    {
        (HamonRoot host, DropdownElement<int> el) = Mount(Make(_ => { }));
        Assert.False(el.IsOpen);

        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Up, 0.01f));
        host.Update(Viewport);

        Assert.True(el.IsOpen);
        Assert.Equal(1, host.OverlayCount);
    }

    [Fact]
    public void ScrimTap_Closes()
    {
        (HamonRoot host, DropdownElement<int> el) = Mount(Make(_ => { }));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Up, 0.01f));
        host.Update(Viewport);
        Assert.Equal(1, host.OverlayCount);

        // 遠く（メニュー外）をタップ＝scrim が受けて閉じる。
        host.DispatchPointer(new PointerEvent(new Vec2(280, 380), PointerPhase.Down, 0.02f));
        host.DispatchPointer(new PointerEvent(new Vec2(280, 380), PointerPhase.Up, 0.03f));
        host.Update(Viewport);

        Assert.Equal(0, host.OverlayCount);
        Assert.False(el.IsOpen);
    }

    [Fact]
    public void SelectItem_FiresOnChanged_AndCloses()
    {
        int? chosen = null;
        (HamonRoot host, DropdownElement<int> el) = Mount(Make(v => chosen = v));

        // トリガーを開く（トリガー高さ＝24+8*2=40、幅 120+8(spacing)+~20(▾)+24 ≒ menu anchor 下）。
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Up, 0.01f));
        host.Update(Viewport);
        Assert.True(el.IsOpen);

        // メニューは anchor.Bottom+2 から。trigger 高さ ~40 → 先頭項目 y≈ 42 + card pad 4 + button pad 8 ≒ 54。
        // 先頭項目の中央あたりをタップ。
        host.DispatchPointer(new PointerEvent(new Vec2(40, 60), PointerPhase.Down, 0.02f));
        host.DispatchPointer(new PointerEvent(new Vec2(40, 60), PointerPhase.Up, 0.03f));
        host.Update(Viewport);

        Assert.Equal(1, chosen);
        Assert.False(el.IsOpen);
    }
}
