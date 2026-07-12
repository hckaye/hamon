using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary><see cref="Button"/>Deterministic test of enable/disable, hover, and focus traversal control.</summary>
public class ButtonStateTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    private static HamonRoot Mount(Widget root)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => root);
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Disabled_IgnoresTap()
    {
        int taps = 0;
        HamonRoot host = Mount(new Button
        {
            Enabled = false,
            OnPressed = () => taps++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.01f));

        Assert.Equal(0, taps);
    }

    [Fact]
    public void Disabled_IgnoresGamepadActivate()
    {
        int taps = 0;
        var node = new FocusNode();
        HamonRoot host = Mount(new Button
        {
            Enabled = false,
            FocusableWhenDisabled = true, // フォーカスは当たるが activate はしない
            Node = node,
            Autofocus = true,
            OnPressed = () => taps++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        // FocusableWhenDisabled でもオートフォーカスは取らない（Enabled のみ自動取得）。明示移動でフォーカス。
        host.MoveFocus(FocusDirection.Down);
        host.HandleButtonDown(GamepadButton.A);

        Assert.Equal(0, taps);
    }

    [Fact]
    public void Disabled_ExcludedFromTraversal_ByDefault()
    {
        var disabled = new FocusNode();
        HamonRoot host = Mount(new Button
        {
            Enabled = false,
            Node = disabled,
            OnPressed = () => { },
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        Assert.False(host.MoveFocus(FocusDirection.Down)); // 登録されない＝移動先なし
        Assert.False(disabled.HasFocus);
    }

    [Fact]
    public void Disabled_IncludedInTraversal_WhenFocusableWhenDisabled()
    {
        var disabled = new FocusNode();
        HamonRoot host = Mount(new Button
        {
            Enabled = false,
            FocusableWhenDisabled = true,
            Node = disabled,
            OnPressed = () => { },
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        Assert.True(host.MoveFocus(FocusDirection.Down)); // トラバーサル対象に含まれる
        Assert.True(disabled.HasFocus);
    }

    [Fact]
    public void Hover_DoesNotActivate_ButTracksWithoutError()
    {
        int taps = 0;
        HamonRoot host = Mount(new Button
        {
            OnPressed = () => taps++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.DispatchHover(new Vec2(50, 50));   // hover 開始
        host.DispatchHover(new Vec2(250, 250)); // hover 終了
        Assert.Equal(0, taps); // hover だけでは押下しない
        Assert.Equal(MouseCursor.Basic, host.CurrentCursor); // 領域外なので Basic
    }

    [Fact]
    public void Hover_ShowsClickCursor()
    {
        HamonRoot host = Mount(new Button
        {
            OnPressed = () => { },
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.DispatchHover(new Vec2(50, 50));
        Assert.Equal(MouseCursor.Click, host.CurrentCursor);
    }

    [Fact]
    public void DisabledButton_HoverShowsForbiddenCursor()
    {
        HamonRoot host = Mount(new Button
        {
            Enabled = false,
            OnPressed = () => { },
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.DispatchHover(new Vec2(50, 50));
        Assert.Equal(MouseCursor.Forbidden, host.CurrentCursor);
    }
}
