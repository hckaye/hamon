using Hamon.Layout;
using Hamon.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Hamon.MonoGame;

/// <summary>
/// Reads MonoGame's Mouse/Keyboard/GamePad every frame,<see cref="HamonRoot"/>Input pump for delivery to.
/// PollMouse/PollKeys/PollPad, which were duplicated in each sample, were unified into one.<see cref="HamonApp"/>is driven internally.
/// </summary>
public sealed class HamonInput
{
    private readonly HamonRoot _ui;

    private MouseState _prevMouse;
    private KeyboardState _prevKeys;
    private GamePadState _prevPad;
    private int _prevWheel;
    private bool _hasWheelBaseline;

    public HamonInput(HamonRoot ui) => _ui = ui;

    /// <summary>
    /// Handler called by "Back" operation (Esc / Gamepad B).
    /// default policy).
    /// </summary>
    public Action? OnBack { get; set; }

    /// <summary>Wheel 1 notch (OS 120 units) → px magnification (default 0.22 = conservative).</summary>
    public float WheelScale { get; set; } = 0.22f;

    // タッチ ID をマウス（ID 0）と衝突させないためのオフセット。各指は location.Id + この値で配送される。
    private const int TouchIdOffset = 1;

    /// <summary>Read mouse/touch/wheel/keyboard/gamepad,<see cref="HamonRoot"/>(once per frame).</summary>
    public void Update()
    {
        PollMouse();
        PollTouch();
        PollKeys();
        PollPad();
    }

    private void Back() => OnBack?.Invoke();

    private void PollMouse()
    {
        MouseState m = Mouse.GetState();
        var pos = new Vec2(m.X, m.Y);
        bool down = m.LeftButton == ButtonState.Pressed;
        bool wasDown = _prevMouse.LeftButton == ButtonState.Pressed;
        if (down && !wasDown)
        {
            _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Down));
        }
        else if (down && wasDown)
        {
            _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Move));
        }
        else if (!down && wasDown)
        {
            _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Up));
        }

        // hover：マウスが動いたフレームだけ再計算（静止時に毎フレーム全ツリーを走査しない）。
        if (m.X != _prevMouse.X || m.Y != _prevMouse.Y)
        {
            _ui.DispatchHover(pos);
        }

        // ホイール：直下のスクロール要素へ配送。初回フレームは基準値を取るだけで配送しない（巨大な初期デルタ回避）。
        if (!_hasWheelBaseline)
        {
            _prevWheel = m.ScrollWheelValue;
            _hasWheelBaseline = true;
        }

        int wheel = m.ScrollWheelValue - _prevWheel;
        if (wheel != 0)
        {
            _ui.DispatchScroll(pos, wheel * WheelScale);
        }

        _prevWheel = m.ScrollWheelValue;
        _prevMouse = m;
    }

    /// <summary>
    /// Deliver each finger on the touch panel with an ID (multi-touch = pressing VPad and skill button at the same time, etc.).<see cref="TouchLocation"/>of
    /// Since State has a phase, there is no need to compare the previous frame. <see cref="TouchIdOffset"/>shift.
    /// Note:<c>TouchPanel.EnableMouseTouchEmulation</c>If you enable it, mouse operations will also come as synthetic touches, resulting in double delivery.
    /// (The mouse is<see cref="PollMouse"/>(read separately), so do not enable it on the desktop.
    /// </summary>
    private void PollTouch()
    {
        TouchCollection touches = TouchPanel.GetState();
        for (int i = 0; i < touches.Count; i++)
        {
            TouchLocation t = touches[i];
            var pos = new Vec2(t.Position.X, t.Position.Y);
            int id = t.Id + TouchIdOffset;
            switch (t.State)
            {
                case TouchLocationState.Pressed:
                    _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Down, 0f, id));
                    break;
                case TouchLocationState.Moved:
                    _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Move, 0f, id));
                    break;
                case TouchLocationState.Released:
                    _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Up, 0f, id));
                    break;
                case TouchLocationState.Invalid:
                    _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Cancel, 0f, id));
                    break;
            }
        }
    }

    private void PollKeys()
    {
        KeyboardState k = Keyboard.GetState();

        // IME 入力中は方向キー＝候補選択／Enter＝確定 を IME に任せ、アプリのフォーカス移動/決定/編集には流さない（横取り防止）。
        if (_ui.IsImeActive)
        {
            _prevKeys = k;
            return;
        }

        if (Edge(k, Keys.Up))
        {
            _ui.MoveFocus(FocusDirection.Up);
        }

        if (Edge(k, Keys.Down))
        {
            _ui.MoveFocus(FocusDirection.Down);
        }

        if (Edge(k, Keys.Left))
        {
            _ui.MoveFocus(FocusDirection.Left);
        }

        if (Edge(k, Keys.Right))
        {
            _ui.MoveFocus(FocusDirection.Right);
        }

        if (Edge(k, Keys.Enter))
        {
            _ui.HandleButtonDown(GamepadButton.A);
            _ui.DispatchEditKey(TextEditKey.Enter);
        }

        if (Edge(k, Keys.Escape))
        {
            Back();
        }

        if (Edge(k, Keys.Back))
        {
            _ui.DispatchEditKey(TextEditKey.Backspace);
        }

        if (Edge(k, Keys.Delete))
        {
            _ui.DispatchEditKey(TextEditKey.Delete);
        }

        if (Edge(k, Keys.Home))
        {
            _ui.DispatchEditKey(TextEditKey.Home);
        }

        if (Edge(k, Keys.End))
        {
            _ui.DispatchEditKey(TextEditKey.End);
        }

        _prevKeys = k;
    }

    private bool Edge(KeyboardState now, Keys key) => now.IsKeyDown(key) && _prevKeys.IsKeyUp(key);

    private void PollPad()
    {
        GamePadState p = GamePad.GetState(PlayerIndex.One);
        if (!p.IsConnected)
        {
            _prevPad = p;
            return;
        }

        if (PadEdge(p, Buttons.DPadUp))
        {
            _ui.MoveFocus(FocusDirection.Up);
        }

        if (PadEdge(p, Buttons.DPadDown))
        {
            _ui.MoveFocus(FocusDirection.Down);
        }

        if (PadEdge(p, Buttons.DPadLeft))
        {
            _ui.MoveFocus(FocusDirection.Left);
        }

        if (PadEdge(p, Buttons.DPadRight))
        {
            _ui.MoveFocus(FocusDirection.Right);
        }

        if (PadEdge(p, Buttons.A))
        {
            _ui.HandleButtonDown(GamepadButton.A);
        }

        if (PadEdge(p, Buttons.B))
        {
            Back();
        }

        _prevPad = p;
    }

    private bool PadEdge(GamePadState now, Buttons button) => now.IsButtonDown(button) && _prevPad.IsButtonUp(button);
}
