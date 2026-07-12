using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Deterministic test for accessory controls (ProgressBar/Checkbox/Switch/Slider).</summary>
public class ControlsTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(216, 100);

    private static HamonRoot Mount(Func<Widget> build)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(build);
        host.Update(Viewport);
        return host;
    }

    [Fact]
    public void Checkbox_Tap_Toggles()
    {
        bool? changed = null;
        HamonRoot host = Mount(() => new Checkbox { Value = false, Autofocus = true, OnChanged = v => changed = v });

        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Up, 0.02f));

        Assert.True(changed); // !false
    }

    [Fact]
    public void Checkbox_Activate_Toggles()
    {
        bool? changed = null;
        HamonRoot host = Mount(() => new Checkbox { Value = true, Autofocus = true, OnChanged = v => changed = v });

        host.Activate(); // OK（ゲームパッド/Enter）
        Assert.False(changed); // !true
    }

    [Fact]
    public void Switch_Tap_Toggles_AndAnimatesWithoutCrash()
    {
        var host = new HamonRoot(new StubTextRenderer());
        State<bool> state = host.CreateState(false);
        host.SetRoot(() => new Switch { Value = state.Value, Autofocus = true, OnChanged = v => state.Value = v });
        host.Update(Viewport);

        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Up, 0.02f));
        Assert.True(state.Value); // トグルで true

        host.Update(Viewport);        // 新しい値で再構築（ノブのアニメ開始）
        host.Update(Viewport, 0.2f);  // ノブのスライドを前進（クラッシュしないこと）
        Assert.True(state.Value);
    }

    [Fact]
    public void Slider_Drag_SetsValue()
    {
        float value = 0f;
        HamonRoot host = Mount(() => new Slider { Value = value, Autofocus = true, OnChanged = v => value = v });

        // 幅216・thumb16 → usable200。x=108 で 0.5。
        host.DispatchPointer(new PointerEvent(new Vec2(108, 14), PointerPhase.Down, 0.01f));
        Assert.Equal(0.5f, value, 0.02f);

        host.DispatchPointer(new PointerEvent(new Vec2(1000, 14), PointerPhase.Move, 0.02f));
        Assert.Equal(1f, value, 0.001f); // 右端でクランプ

        host.DispatchPointer(new PointerEvent(new Vec2(-50, 14), PointerPhase.Move, 0.03f));
        Assert.Equal(0f, value, 0.001f); // 左端でクランプ
    }

    [Fact]
    public void Slider_Dpad_StepsValue()
    {
        float right = 0.5f;
        HamonRoot h1 = Mount(() => new Slider { Value = right, Autofocus = true, Step = 0.1f, OnChanged = v => right = v });
        h1.DispatchButtonDown(GamepadButton.DpadRight);
        Assert.Equal(0.6f, right, 0.001f);

        float left = 0.5f;
        HamonRoot h2 = Mount(() => new Slider { Value = left, Autofocus = true, Step = 0.1f, OnChanged = v => left = v });
        h2.DispatchButtonDown(GamepadButton.DpadLeft);
        Assert.Equal(0.4f, left, 0.001f);
    }

    [Fact]
    public void ProgressBar_Builds()
    {
        HamonRoot host = Mount(() => new ProgressBar { Value = 0.5f, Width = Dimension.Px(120) });
        Assert.NotNull(host.Root); // 例外なく構築
    }
}
