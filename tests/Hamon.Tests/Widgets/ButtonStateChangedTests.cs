using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of Button.OnStateChanged (hover/pressed/focused transition notification = starting point of game custom animation/sound effect).</summary>
public class ButtonStateChangedTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);
    private static readonly Color Bg = new(40, 44, 54);

    private static (HamonRoot Host, List<WidgetState> States) Mount()
    {
        var states = new List<WidgetState>();
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Button
            {
                Node = new FocusNode(),
                OnPressed = () => { },
                Background = Bg,
                OnStateChanged = s => states.Add(s),
                Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
            },
        });
        host.Update(Viewport);
        return (host, states);
    }

    [Fact]
    public void Hover_FiresHovered()
    {
        (HamonRoot host, List<WidgetState> states) = Mount();
        host.DispatchHover(new Vec2(50, 50));
        host.Update(Viewport);
        Assert.Contains(states, s => s.Has(WidgetState.Hovered));
    }

    [Fact]
    public void Press_FiresPressed()
    {
        (HamonRoot host, List<WidgetState> states) = Mount();
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport);
        Assert.Contains(states, s => s.Has(WidgetState.Pressed));
    }

    [Fact]
    public void Focus_FiresFocused()
    {
        (HamonRoot host, List<WidgetState> states) = Mount();
        host.MoveFocus(FocusDirection.Down); // ボタンへフォーカス
        host.Update(Viewport);
        Assert.Contains(states, s => s.Has(WidgetState.Focused));
    }

    [Fact]
    public void Release_ClearsPressed()
    {
        (HamonRoot host, List<WidgetState> states) = Mount();
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.1f));
        host.Update(Viewport);
        Assert.False(states[^1].Has(WidgetState.Pressed)); // 離したら pressed は消える（クリックでフォーカスは残る）
    }

    [Fact]
    public void SlotButton_OnStateChanged_FiresOnHover()
    {
        var states = new List<WidgetState>();
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new SlotButton { OnStateChanged = s => states.Add(s), OnPressed = () => { } },
        });
        host.Update(Viewport);

        host.DispatchHover(new Vec2(20, 20)); // スロット上（56x56）
        host.Update(Viewport);
        Assert.Contains(states, s => s.Has(WidgetState.Hovered)); // Button へ委譲されている
    }

    [Fact]
    public void Checkbox_OnStateChanged_FiresOnHover()
    {
        var states = new List<WidgetState>();
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Checkbox { Value = true, OnStateChanged = s => states.Add(s) },
        });
        host.Update(Viewport);

        host.DispatchHover(new Vec2(10, 10)); // チェックボックス上（24x24）
        host.Update(Viewport);
        // hover かつ選択中なので Hovered|Selected を含む状態が通知される。
        Assert.Contains(states, s => s.Has(WidgetState.Hovered) && s.Has(WidgetState.Selected));
    }

    private sealed class FakeSound : ISoundPlayer
    {
        public List<SoundId> Played { get; } = new();

        public void Play(SoundId sound, float volume = 1f) => Played.Add(sound);
    }

    [Fact]
    public void Button_Sounds_PlayOnHoverAndPress()
    {
        var sfx = new FakeSound();
        var host = new HamonRoot(new StubText()) { Sound = sfx };
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Button
            {
                Node = new FocusNode(),
                OnPressed = () => { },
                Background = Bg,
                Sounds = new InteractionSounds { Hover = 1, Press = 2 }, // int → SoundId? 暗黙変換
                Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
            },
        });
        host.Update(Viewport);

        host.DispatchHover(new Vec2(50, 50));
        host.Update(Viewport);
        Assert.Contains(new SoundId(1), sfx.Played);

        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0f));
        host.Update(Viewport);
        Assert.Contains(new SoundId(2), sfx.Played);
    }

    [Fact]
    public void Checkbox_OnStateChanged_FiresOnFocus()
    {
        var states = new List<WidgetState>();
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Checkbox { OnStateChanged = s => states.Add(s) },
        });
        host.Update(Viewport);

        host.MoveFocus(FocusDirection.Down);
        host.Update(Viewport);
        Assert.Contains(states, s => s.Has(WidgetState.Focused));
    }
}
