using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary><see cref="FocusableActionDetector"/>(focus/hover/press/gamepad aggregation/status notification/gating) deterministic test.</summary>
public class FocusableActionDetectorTests
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
    public void Tap_FiresOnActivate()
    {
        int activations = 0;
        HamonRoot host = Mount(new FocusableActionDetector
        {
            OnActivate = () => activations++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.01f));

        Assert.Equal(1, activations);
    }

    [Fact]
    public void Disabled_IgnoresTapAndIsNotFocusable()
    {
        int activations = 0;
        var node = new FocusNode();
        HamonRoot host = Mount(new FocusableActionDetector
        {
            Enabled = false,
            Node = node,
            OnActivate = () => activations++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.01f));
        Assert.Equal(0, activations);

        Assert.False(host.MoveFocus(FocusDirection.Down)); // 登録されていない＝移動先が無い
        Assert.False(node.HasFocus);
    }

    [Fact]
    public void Hover_TogglesHoverHighlight()
    {
        var log = new List<bool>();
        HamonRoot host = Mount(new FocusableActionDetector
        {
            OnShowHoverHighlight = on => log.Add(on),
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.DispatchHover(new Vec2(50, 50));   // enter
        host.DispatchHover(new Vec2(250, 250)); // exit（viewport 外）

        Assert.Equal(new[] { true, false }, log);
    }

    [Fact]
    public void Focus_FiresFocusChangeAndHighlight()
    {
        var focusLog = new List<bool>();
        var highlightLog = new List<bool>();
        HamonRoot host = Mount(new FocusableActionDetector
        {
            Autofocus = true,
            OnFocusChange = f => focusLog.Add(f),
            OnShowFocusHighlight = h => highlightLog.Add(h),
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        Assert.Equal(new[] { true }, focusLog);
        Assert.Equal(new[] { true }, highlightLog);
    }

    [Fact]
    public void Gamepad_ActivateAndDismiss_FireWhenFocused()
    {
        int activate = 0;
        int dismiss = 0;
        HamonRoot host = Mount(new FocusableActionDetector
        {
            Autofocus = true,
            OnActivate = () => activate++,
            OnDismiss = () => dismiss++,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.HandleButtonDown(GamepadButton.A); // 既定 Activate
        host.HandleButtonDown(GamepadButton.B); // 既定 Dismiss

        Assert.Equal(1, activate);
        Assert.Equal(1, dismiss);
    }

    [Fact]
    public void Shortcuts_MapButtonToAction()
    {
        int reloads = 0;
        HamonRoot host = Mount(new FocusableActionDetector
        {
            Autofocus = true,
            Shortcuts = new Dictionary<GamepadButton, string> { [GamepadButton.X] = "reload" },
            Actions = new Dictionary<string, System.Action> { ["reload"] = () => reloads++ },
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });

        host.HandleButtonDown(GamepadButton.X);
        Assert.Equal(1, reloads);
    }

    [Fact]
    public void Builder_RebuildsChildOnPressState()
    {
        var seen = new List<WidgetState>();
        HamonRoot host = Mount(new FocusableActionDetector
        {
            Builder = states =>
            {
                seen.Add(states);
                return new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) };
            },
            Child = null,
        });

        seen.Clear();
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport); // 局所 rebuild が走る
        Assert.Contains(seen, s => s.Has(WidgetState.Pressed));
    }

    [Fact]
    public void PressCancel_ClearsPressedAndRebuilds()
    {
        WidgetState last = WidgetState.None;
        HamonRoot host = Mount(new FocusableActionDetector
        {
            Builder = states =>
            {
                last = states;
                return new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) };
            },
        });

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport);
        Assert.True(last.Has(WidgetState.Pressed));

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Cancel, 0.01f));
        host.Update(Viewport);
        Assert.False(last.Has(WidgetState.Pressed));
    }

    [Fact]
    public void DescendantsAreFocusable_False_SuppressesInnerFocus()
    {
        var inner = new FocusNode();
        Mount(new FocusableActionDetector
        {
            DescendantsAreFocusable = false,
            Child = new Focus { Node = inner, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) } },
        });

        Assert.False(inner.HasFocus); // 子孫のフォーカスが無効化されている
    }

    [Fact]
    public void DescendantsAreFocusable_True_AllowsInnerFocus()
    {
        var inner = new FocusNode();
        Mount(new FocusableActionDetector
        {
            DescendantsAreFocusable = true,
            Child = new Focus { Node = inner, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(50) } },
        });

        Assert.True(inner.HasFocus);
    }
}
