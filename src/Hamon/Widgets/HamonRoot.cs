using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Hamon runtime entry point. Relayouts only when size changes (no rebuild/recalculation in steady-state
/// frames). Draw via <see cref="Render"/> (drawing is delegated to the backend through <see cref="IPainter"/>,
/// keeping the core independent of engines such as MonoGame).
/// </summary>
public sealed class HamonRoot : IHamonHost, IDisposable
{
    private readonly ITextRenderer _text;

    private readonly List<OverlayEntry> _overlays = new();
    private readonly List<ITicker> _tickers = new();
    private readonly List<Element> _dirtyElements = new();
    private readonly List<Action> _posted = new();
    private readonly object _postLock = new();
    private volatile bool _hasPosted; // 投稿の有無を lock 無しで見るフラグ（async 未使用時は毎フレームのロックを回避）

    private Func<Widget>? _build;
    private Element? _root;
    private bool _dirty;
    private bool _needsLayout;

    // ポインタID別の独立キャプチャ（マルチタッチ）。指は最大でも十数本ゆえ固定長スロットで線形走査＝ゼロアロケ。
    // 各スロットが Down でヒットした要素・基点・調停状態を保持し、その ID の Move/Up/Cancel をその要素へ配送する。
    private readonly PointerCapture[] _captures = new PointerCapture[16];
    private float _time;         // 単調増加の内部時計（秒。dt の累積）
    private Size _lastSize = new(-1f, -1f);

    private readonly List<HeldButton> _heldButtons = new();    // 押しっぱなしのボタン（リピート/長押し計時）
    private readonly List<IHoverTarget> _hovered = new();      // 現在 hover 中の対象（最前面が末尾）
    private readonly List<IHoverTarget> _hoverScratch = new(); // 再計算用の作業バッファ（毎回 Clear で再利用＝ZeroAlloc）
    private Vec2 _hoverPosition;
    private bool _hasHover; // マウスがウィンドウ内にあるか（false=全 exit 済み）

    private FocusNode? _cursorNode;
    private Rect _cursorFrom;
    private Rect _cursorTo;
    private AnimationController? _cursorAnim;
    private FocusNode? _lastRevealedFocus;
    private int _imeGuardFrames; // >0 の間は IME 入力中（アプリのキー処理を抑止）
    private bool _imeComposing;  // 変換中テキスト（preedit）が存在する間 true＝確実に IME 入力中

    private const float DragSlop = 8f; // スクロール開始の移動しきい値（px）

    private static readonly Func<float> StaticOne = static () => 1f; // 遷移なし時の進捗（常に表示）

    /// <param name="text">Text measurement/drawing (provided by backend, can be stubbed for inspection/testing).</param>
    public HamonRoot(ITextRenderer text)
    {
        _text = text;
    }

    /// <summary>
    /// Root entity of the app body (for testing/inspection): the result of the build function passed to
    /// <see cref="SetRoot"/>.
    /// </summary>
    public Element? Root
    {
        get
        {
            if (_root is null)
            {
                return null;
            }

            IReadOnlyList<Element> children = _root.Children;
            return children.Count > 0 ? children[0] : _root;
        }
    }

    /// <summary>Focus management (directional movement/button/analog delivery).</summary>
    public FocusManager Focus { get; } = new();

    /// <summary>Input assignment for focus movement/OK/Cancel (can be changed in initial settings).</summary>
    public FocusBindings Bindings { get; } = new();

    /// <summary>Settings for gamepad button press and hold behavior (automatic repeat/long press).</summary>
    public GamepadHoldSettings Hold { get; } = new();

    /// <summary>Default (light) UI style; see <see cref="HamonTheme.Default"/>.</summary>
    public HamonTheme Theme { get; set; } = HamonTheme.Default;

    /// <summary>
    /// Dark color scheme (opt-in). When <c>null</c>, <see cref="Theme"/> is used regardless of mode, so
    /// nothing darkens automatically by default (the game default). Example: enable dark mode with
    /// <c>DarkTheme = HamonTheme.Dark</c> (equivalent to Flutter's <c>MaterialApp.darkTheme</c>).
    /// </summary>
    public HamonTheme? DarkTheme { get; set; }

    /// <summary>Theme selection mode (compliant with Flutter's <c>themeMode</c>); defaults to <see cref="Widgets.ThemeMode.System"/>. Always falls back to <see cref="Theme"/> if <see cref="DarkTheme"/> is unset.</summary>
    public ThemeMode ThemeMode { get; set; } = ThemeMode.System;

    /// <summary>OS brightness in System mode (set by the host integration); defaults to <see cref="Brightness.Light"/> in environments where OS dark-mode settings are unavailable.</summary>
    public Brightness PlatformBrightness { get; set; } = Brightness.Light;

    /// <summary>
    /// The theme actually applied: the resolved result of <see cref="ThemeMode"/>, <see cref="DarkTheme"/>,
    /// and <see cref="PlatformBrightness"/>. Even when dark mode is requested, this falls back to
    /// <see cref="Theme"/> if <see cref="DarkTheme"/> is <c>null</c> (dark theming is opt-in).
    /// </summary>
    public HamonTheme EffectiveTheme
    {
        get
        {
            bool wantsDark = ThemeMode switch
            {
                ThemeMode.Light => false,
                ThemeMode.Dark => true,
                _ => PlatformBrightness == Brightness.Dark,
            };
            return wantsDark && DarkTheme is not null ? DarkTheme : Theme;
        }
    }

    /// <summary>Tag-based registry for shared element transitions (<see cref="Hero"/>); <see cref="Navigator"/> drives the flight.</summary>
    internal HeroRegistry Heroes { get; } = new();

    /// <summary>Drag and drop status (<see cref="Draggable{T}"/>/<see cref="DragTarget{T}"/>).</summary>
    internal DragController Drag { get; } = new();

    /// <summary>
    /// Device pixel ratio (physical px / logical pt). Layout uses logical pt (as passed to the
    /// <c>available</c> parameter of <see cref="Update(Size,float)"/>), and drawing is scaled by this ratio
    /// when transferred to physical px, so text is rasterized at this ratio for crisp display
    /// (<c>FontSize × ScaleY</c>). Calculated and set by the backend from the backbuffer/client bounds.
    /// </summary>
    public float DevicePixelRatio
    {
        get => _devicePixelRatio;
        set => _devicePixelRatio = value > 0f ? value : 1f;
    }

    private float _devicePixelRatio = 1f;

    /// <summary>Focus cursor (a single overlay with movement animation and style); enable via <see cref="FocusCursor.Enabled"/>.</summary>
    public FocusCursor Cursor { get; } = new();

    /// <summary><see cref="IHamonHost.CursorEnabled"/>: Suppresses the focus ring when the focus cursor is enabled.</summary>
    public bool CursorEnabled => Cursor.Enabled;

    /// <summary>Platform text input/IME bridging (injected by backend; null = no OS IME coordination).</summary>
    public ITextInput? TextInput { get; set; }

    /// <summary>Clipboard (copy/cut/paste). </summary>
    public IClipboard Clipboard { get; set; } = new InMemoryClipboard();

    /// <summary>
    /// Destination for playing sound effects (a bridge to game audio; null = silence). This
    /// <b>only provides the trigger to play a sound</b>: call <see cref="PlaySound"/> from widget-declarative
    /// sounds (<see cref="InteractionSounds"/>) or from user state callbacks.
    /// </summary>
    public ISoundPlayer? Sound { get; set; }

    /// <summary>Plays <paramref name="sound"/> (a <see cref="SoundId"/> value type, so no boxing). No-op if <see cref="Sound"/> is not injected.</summary>
    public void PlaySound(SoundId? sound, float volume = 1f)
    {
        if (sound is SoundId id)
        {
            Sound?.Play(id, volume);
        }
    }

    /// <summary>
    /// Exception-catching boundary for build/layout/render (active only when set). Notifies this handler
    /// instead of crashing, safely aborting the current frame (retried on the next frame).
    /// </summary>
    public Action<Exception>? OnError { get; set; }

    /// <summary>
    /// Height of the soft keyboard covering the bottom of the screen (px; 0 = hidden).
    /// The application/layout can raise its content by this amount so the focused field isn't hidden (used by <see cref="SafeArea"/>).
    /// </summary>
    public float SoftKeyboardHeight { get; set; }

    /// <summary>
    /// Safe area insets (edge margins in px hidden by notch/status bar/home indicator, etc.).
    /// Configured by the mobile backend; <see cref="SafeArea"/> consumes this as inner margin.
    /// </summary>
    public EdgeInsets SafeAreaInsets { get; set; }

    /// <summary><see cref="IHamonHost.BeginTextInput"/>: tells the IME to begin input for the focused field.</summary>
    public void BeginTextInput() => TextInput?.Start();

    /// <summary><see cref="IHamonHost.EndTextInput"/>: Informs the IME that input is complete.</summary>
    public void EndTextInput() => TextInput?.Stop();

    /// <summary><see cref="IHamonHost.SetTextInputCaret"/>: Send the caret rectangle to the IME (candidate window position).</summary>
    public void SetTextInputCaret(Rect caret) => TextInput?.SetCaretRect(caret);

    /// <summary>Deliver the text being converted (preedit) of the IME to the focused field (called by the backend).</summary>
    public void DispatchComposition(string text, int caret)
    {
        // 変換中（および確定/取り消し直後の数フレーム）はアプリのキー処理を抑止するためのガード。
        // IME の上下キー（候補選択）や確定 Enter をアプリの MoveFocus/Submit が横取りしないようにする。
        // 変換中テキストの有無で composing を更新＝空配送（確定/取り消し）で composing は解除される。
        _imeComposing = !string.IsNullOrEmpty(text);
        _imeGuardFrames = 3;
        Focus.Focused?.OnComposition?.Invoke(text, caret);
    }

    /// <summary>
    /// Whether the IME is currently active (during conversion, or for a few frames immediately after
    /// confirm/cancel). While true, <b>app key processing should be suppressed</b> (direction keys select IME
    /// candidates, Enter confirms via the IME; do not move focus or submit). Because of frame ordering — e.g.
    /// around <see cref="DispatchText"/> — the guard lasts for a few extra frames.
    /// </summary>
    public bool IsImeActive => _imeComposing || _imeGuardFrames > 0;

    /// <summary>Current focus cursor rectangle (after animation interpolation, including margins). </summary>
    public Rect? FocusCursorRect =>
        Cursor.Enabled && _cursorNode is not null ? Pad(CurrentCursorRect(), Cursor.Padding) : null;

    public State<T> CreateState<T>(T initial) => new(this, initial);

    /// <summary>
    /// Creates an animation driver linked to this root (advanced by dt via <see cref="Update(Size, float)"/>).
    /// Read it from <see cref="Opacity"/>/<see cref="Transform"/> via <c>() =&gt; ctrl.Curved</c> at paint
    /// time, without needing to rebuild.
    /// </summary>
    public AnimationController CreateAnimation(float durationSeconds, Curve? curve = null) => new(this, durationSeconds, curve);

    // --- 既定バインドで物理入力を処理する便宜入口（全自前なら下の直接APIを使う） ---

    /// <summary>
    /// Processes a physical button press using the default binding: directional buttons move focus; other
    /// buttons are delivered to the focused node plus OK/Cancel. Also starts auto-repeat/long-press timing
    /// here (advanced by dt via <see cref="Update(Size, float)"/>).
    /// </summary>
    public void HandleButtonDown(GamepadButton button)
    {
        StartHold(button);
        DispatchButtonDownDefault(button);
    }

    /// <summary>Delivers a single press using the default binding (shared by both the initial press and repeats).</summary>
    private void DispatchButtonDownDefault(GamepadButton button)
    {
        if (Bindings.Directional.TryGetValue(button, out FocusDirection direction))
        {
            Focus.MoveFocus(direction);
            return;
        }

        Focus.DispatchButtonDown(button);
        if (button == Bindings.ActivateButton)
        {
            Focus.Activate();
        }
        else if (button == Bindings.DismissButton)
        {
            Focus.Dismiss();
        }
    }

    public void HandleButtonUp(GamepadButton button)
    {
        EndHold(button);
        Focus.DispatchButtonUp(button);
    }

    /// <summary>Buttons that are held down, their holding time, long-pressed flag, and repeat accumulation.</summary>
    private struct HeldButton
    {
        public HeldButton(GamepadButton button)
        {
            Button = button;
            Held = 0f;
            SinceRepeat = 0f;
            LongPressFired = false;
        }

        public GamepadButton Button;
        public float Held;
        public float SinceRepeat;
        public bool LongPressFired;
    }

    private void StartHold(GamepadButton button)
    {
        for (int i = 0; i < _heldButtons.Count; i++)
        {
            if (_heldButtons[i].Button == button)
            {
                return; // 既に保持中（重複押下イベント）
            }
        }

        _heldButtons.Add(new HeldButton(button));
    }

    private void EndHold(GamepadButton button)
    {
        for (int i = _heldButtons.Count - 1; i >= 0; i--)
        {
            if (_heldButtons[i].Button == button)
            {
                _heldButtons.RemoveAt(i);
            }
        }
    }

    /// <summary>Advances the timer while holding the button, and fires a long press (once) and automatic repeat.</summary>
    private void AdvanceHeldButtons(float dt)
    {
        for (int i = 0; i < _heldButtons.Count; i++)
        {
            HeldButton held = _heldButtons[i];
            held.Held += dt;

            // 長押し（1回）。
            if (!held.LongPressFired && Hold.LongPressDuration > 0f && held.Held >= Hold.LongPressDuration)
            {
                held.LongPressFired = true;
                Focus.DispatchButtonLongPress(held.Button);
            }

            // 自動リピート（初回遅延後に一定間隔）。
            if (Hold.RepeatEnabled && Hold.RepeatInterval > 0f && held.Held >= Hold.RepeatDelay)
            {
                held.SinceRepeat += dt;
                while (held.SinceRepeat >= Hold.RepeatInterval)
                {
                    held.SinceRepeat -= Hold.RepeatInterval;
                    EmitRepeat(held.Button);
                }
            }

            _heldButtons[i] = held;
        }
    }

    private void EmitRepeat(GamepadButton button)
    {
        if (Bindings.Directional.TryGetValue(button, out FocusDirection direction))
        {
            Focus.MoveFocus(direction); // 方向ボタンのリピート＝オートナビ
        }
        else if (!Hold.RepeatDirectionalOnly)
        {
            Focus.DispatchButtonRepeat(button);
        }
    }

    /// <summary>Deliver confirmed characters to the focused field (e.g. <c>TextField</c>).</summary>
    public void DispatchText(char character)
    {
        _imeComposing = false; // 確定（commit）または通常打鍵＝変換中ではない
        Focus.Focused?.OnTextInput?.Invoke(character);
    }

    /// <summary>Deliver edit keys (backspace/cursor movement, etc.) to the focused area.</summary>
    public void DispatchEditKey(TextEditKey key) => Focus.Focused?.OnEditKey?.Invoke(key);

    // --- 直接API（全自前ハンドリング用：スティック駆動ナビ等は利用側で MoveFocus を呼ぶ） ---

    public bool MoveFocus(FocusDirection direction) => Focus.MoveFocus(direction);

    /// <summary>Move to the next focus target using linear traversal (Tab).</summary>
    public bool MoveNext() => Focus.MoveNext();

    /// <summary>Move to the previous focus target using linear traversal (Shift+Tab).</summary>
    public bool MovePrevious() => Focus.MovePrevious();
    public void DispatchButtonDown(GamepadButton button) => Focus.DispatchButtonDown(button);
    public void DispatchButtonUp(GamepadButton button) => Focus.DispatchButtonUp(button);
    public void DispatchTrigger(GamepadSide side, float value) => Focus.DispatchTrigger(side, value);
    public void DispatchStick(GamepadSide side, Vec2 value) => Focus.DispatchStick(side, value);
    public void Activate() => Focus.Activate();
    public void Dismiss() => Focus.Dismiss();

    /// <summary>
    /// Delivers a pointer event (touch/mouse), routing subsequent events for the same pointer to the same
    /// element. Also shifts focus to the element's <see cref="FocusNode"/> (tap = focus).
    /// Gesture arbitration: even while a tappable child has captured the pointer, if movement exceeds
    /// <see cref="DragSlop"/> along the main axis, delivery is handed off to the nearest ancestor scroll
    /// element, and the child's operation is aborted with <see cref="PointerPhase.Cancel"/> (scroll vs. tap).
    /// </summary>
    public void DispatchPointer(PointerEvent pointer)
    {
        if (_root is null)
        {
            return;
        }

        if (pointer.Timestamp <= 0f)
        {
            pointer = new PointerEvent(pointer.Position, pointer.Phase, _time, pointer.PointerId); // 未指定なら内部時計で補完
        }

        if (pointer.Phase == PointerPhase.Down)
        {
            // 各ポインタは独立にヒットテスト＋キャプチャ＝別の指が別ウィジェットを同時操作できる（VPad＋スキルボタン等）。
            // 同一要素に2本落ちればその要素が両方を受ける＝ピンチ（要素側が ID で判別）。
            int slot = AcquireSlot(pointer.PointerId);
            Element? hit = HitTest(_root, pointer.Position, Transform2D.Identity, out Transform2D local);
            _captures[slot].Captured = hit;
            _captures[slot].Down = pointer.Position; // 調停 slop は常にルート座標で測る
            _captures[slot].DownTime = pointer.Timestamp;
            _captures[slot].ArenaResolved = false;
            _captures[slot].Local = local;
            if (hit is not null)
            {
                FocusAncestor(hit);
                hit.HandlePointer(ToLocal(pointer, local)); // 変換下なら子座標へ写して配送（恒等なら不変）
            }

            return;
        }

        int idx = FindSlot(pointer.PointerId);
        if (idx < 0)
        {
            return;
        }

        Element? captured = _captures[idx].Captured;
        if (captured is null)
        {
            if (pointer.Phase is PointerPhase.Up or PointerPhase.Cancel)
            {
                ReleaseSlot(idx);
            }

            return;
        }

        // 調停（ポインタ別）：キャプチャ先が「手動スクロール有効な scrollable」でないとき、主軸 slop 超なら最寄りの有効な
        // 祖先スクロールへ移譲（手動無効のスクロールは飛ばして親へバブルする）。
        bool capturedIsActiveScroll = captured is IScrollable cs && cs.ManualScrollEnabled;
        if (pointer.Phase == PointerPhase.Move && !_captures[idx].ArenaResolved && !capturedIsActiveScroll)
        {
            Element? scroll = NearestEnabledScrollable(captured, includeSelf: false);
            if (scroll is IScrollable scrollable && ExceedsSlop(_captures[idx].Down, pointer.Position, scrollable.ScrollAxis))
            {
                Transform2D local = _captures[idx].Local;
                captured.HandlePointer(new PointerEvent(local.Apply(pointer.Position), PointerPhase.Cancel, pointer.Timestamp, pointer.PointerId));
                _captures[idx].Captured = scroll;
                _captures[idx].ArenaResolved = true;
                _captures[idx].Local = Transform2D.Identity; // スクロール要素はルート座標で扱う（変換境界の外側を想定）
                scroll.HandlePointer(new PointerEvent(_captures[idx].Down, PointerPhase.Down, _captures[idx].DownTime, pointer.PointerId)); // ドラッグ基点
                scroll.HandlePointer(pointer);
                return;
            }
        }

        captured.HandlePointer(ToLocal(pointer, _captures[idx].Local));
        if (pointer.Phase is PointerPhase.Up or PointerPhase.Cancel)
        {
            ReleaseSlot(idx);
        }
    }

    // ルート座標のポインタイベントをキャプチャ要素のローカル座標へ写す（変換が恒等なら同値＝従来挙動）。
    private static PointerEvent ToLocal(in PointerEvent pointer, in Transform2D local) =>
        new(local.Apply(pointer.Position), pointer.Phase, pointer.Timestamp, pointer.PointerId);

    /// <summary>Capture status by pointer ID (element hit with Down, base point, arbitration completed, local conversion).</summary>
    private struct PointerCapture
    {
        public bool Active;
        public int Id;
        public Element? Captured;
        public Vec2 Down;
        public float DownTime;
        public bool ArenaResolved;
        public Transform2D Local; // ルート座標→キャプチャ要素のローカル座標（変換下のヒット時。既定＝恒等）
    }

    // 既存スロット（同一 ID の重複 Down 防御）を優先し、無ければ空きスロットを確保する。溢れたらスロット0を再利用（>16 同時タッチは非現実的）。
    private int AcquireSlot(int id)
    {
        for (int i = 0; i < _captures.Length; i++)
        {
            if (_captures[i].Active && _captures[i].Id == id)
            {
                return i;
            }
        }

        for (int i = 0; i < _captures.Length; i++)
        {
            if (!_captures[i].Active)
            {
                _captures[i].Active = true;
                _captures[i].Id = id;
                return i;
            }
        }

        _captures[0].Active = true;
        _captures[0].Id = id;
        return 0;
    }

    private int FindSlot(int id)
    {
        for (int i = 0; i < _captures.Length; i++)
        {
            if (_captures[i].Active && _captures[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private void ReleaseSlot(int idx)
    {
        _captures[idx].Active = false;
        _captures[idx].Captured = null;
    }

    /// <summary>
    /// Delivers wheel/trackpad scroll: applies <paramref name="delta"/> (px, positive = wheel up) to the
    /// nearest scroll element directly below <paramref name="position"/>, offsetting it by that amount.
    /// </summary>
    public void DispatchScroll(Vec2 position, float delta)
    {
        if (_root is null)
        {
            return;
        }

        Element? hit = HitTest(_root, position);
        if (hit is null)
        {
            return;
        }

        // 手動スクロール有効な最寄りのスクロール要素へ（無効なものは飛ばして親へ）。
        (NearestEnabledScrollable(hit, includeSelf: true) as IScrollable)?.ScrollByAnimated(-delta);
    }

    /// <summary>
    /// The cursor shape presented by the frontmost hovered <see cref="MouseRegion"/> (or
    /// <see cref="MouseCursor.Basic"/> if none). The backend reads this each frame and applies it to the OS
    /// cursor.
    /// </summary>
    public MouseCursor CurrentCursor { get; private set; } = MouseCursor.Basic;

    /// <summary>
    /// Delivers the mouse hover position (movement without pressing; called on backend mouse-move events).
    /// Recalculates the set of <see cref="MouseRegion"/> elements directly under <paramref name="position"/>
    /// and uses the difference from the previous set to fire enter/exit, plus hover for regions that remain
    /// active. <b>Not called for touch (<see cref="DispatchPointer"/>) — hover is purely mouse-driven</b>.
    /// </summary>
    public void DispatchHover(Vec2 position)
    {
        _hoverPosition = position;
        _hasHover = true;
        RecomputeHover(position);
    }

    /// <summary>Release all hovers when the mouse goes outside the window (deliver exit to the remaining area).</summary>
    public void ClearHover()
    {
        _hasHover = false;
        for (int i = 0; i < _hovered.Count; i++)
        {
            _hovered[i].HoverExit(_hoverPosition);
        }

        _hovered.Clear();
        CurrentCursor = MouseCursor.Basic;
    }

    /// <summary>If an <see cref="IHoverTarget"/> is unmounted while hovering, this guarantees an exit call and removes it from tracking.</summary>
    internal void NotifyHoverTargetUnmounted(IHoverTarget target)
    {
        int index = _hovered.IndexOf(target);
        if (index >= 0)
        {
            target.HoverExit(_hoverPosition);
            _hovered.RemoveAt(index);
            RefreshCursor();
        }
    }

    /// <summary>Recalculates the current hover set and delivers enter/exit/hover (can also be called after layout changes = ZeroAlloc).</summary>
    private void RecomputeHover(Vec2 position)
    {
        if (_root is null || !_hasHover)
        {
            return;
        }

        _hoverScratch.Clear();
        CollectHover(_root, position, _hoverScratch);

        // exit：前回 hover 中で今回集合に無いもの。
        for (int i = 0; i < _hovered.Count; i++)
        {
            IHoverTarget prev = _hovered[i];
            if (!_hoverScratch.Contains(prev))
            {
                prev.HoverExit(position);
            }
        }

        // enter / hover：今回集合の各要素。前回も居れば hover、新規なら enter。
        for (int i = 0; i < _hoverScratch.Count; i++)
        {
            IHoverTarget now = _hoverScratch[i];
            if (_hovered.Contains(now))
            {
                now.HoverMove(position);
            }
            else
            {
                now.HoverEnter(position);
            }
        }

        // 集合を入れ替え（作業バッファの内容を確定集合へコピー）。
        _hovered.Clear();
        _hovered.AddRange(_hoverScratch);
        RefreshCursor();
    }

    /// <summary>
    /// Collects the <see cref="MouseRegionElement"/> hover targets directly under <paramref name="position"/>,
    /// front-to-back. A return value of true means this subtree occluded the hover behind it (an opaque
    /// region or a pointer-receiving element). Results are appended to <paramref name="acc"/> so that the
    /// frontmost element ends up last.
    /// </summary>
    private static bool CollectHover(Element element, Vec2 position, List<IHoverTarget> acc)
    {
        if (!element.LayoutNode.Bounds.Contains(position.X, position.Y))
        {
            return false;
        }

        bool occluded = false;
        IReadOnlyList<Element> children = element.Children;
        for (int i = children.Count - 1; i >= 0; i--) // 後勝ち（最前面）から
        {
            if (CollectHover(children[i], position, acc))
            {
                occluded = true; // 前面の子が遮った＝それより背後の兄弟は hover しない
                break;
            }
        }

        if (element is IHoverTarget region)
        {
            acc.Add(region);
            if (region.HoverOpaque)
            {
                occluded = true;
            }
        }

        // ポインタを受ける要素（Button/GestureDetector 等）は背後への hover も遮る（不透明扱い）。
        return occluded || element.WantsPointer;
    }

    /// <summary>Adopts the cursor of the topmost (frontmost) region in the hover set.</summary>
    private void RefreshCursor()
    {
        // _hovered は背後→最前面の順に積まれている＝末尾が最前面。
        CurrentCursor = _hovered.Count > 0 ? _hovered[_hovered.Count - 1].HoverCursor : MouseCursor.Basic;
    }

    private static bool ExceedsSlop(Vec2 down, Vec2 position, Axis axis)
    {
        float along = axis == Axis.Vertical ? position.Y - down.Y : position.X - down.X;
        return MathF.Abs(along) > DragSlop;
    }

    // 最寄りの「手動スクロール有効な」スクロール要素を返す（includeSelf=false なら親から探す）。手動無効なスクロールは飛ばす。
    private static Element? NearestEnabledScrollable(Element? from, bool includeSelf)
    {
        for (Element? current = includeSelf ? from : from?.Parent; current is not null; current = current.Parent)
        {
            if (current is IScrollable s && s.ManualScrollEnabled)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Register animations such as fling every frame (duplicate registrations are ignored).</summary>
    public void RegisterTicker(ITicker ticker)
    {
        if (!_tickers.Contains(ticker))
        {
            _tickers.Add(ticker);
        }
    }

    public void UnregisterTicker(ITicker ticker) => _tickers.Remove(ticker);

    /// <summary>Number of registered tickers (for leak detection/inspection/testing of animation/inertia, etc.).</summary>
    public int ActiveTickerCount => _tickers.Count;

    /// <summary>Internal clock elapsed seconds (accumulation of dt). </summary>
    public float ElapsedSeconds => _time;

    /// <summary>Total number of mounted Elements (for debug display, only for debug as it walks the tree).</summary>
    public int ElementCount => _root is null ? 0 : CountElements(_root);

    private static int CountElements(Element e)
    {
        int n = 1;
        IReadOnlyList<Element> children = e.Children;
        for (int i = 0; i < children.Count; i++)
        {
            n += CountElements(children[i]);
        }

        return n;
    }

    private static Element? HitTest(Element element, Vec2 position) =>
        HitTest(element, position, Transform2D.Identity, out _);

    /// <summary>
    /// Given <paramref name="position"/> (in root = device/UI coordinates), returns the frontmost element
    /// that receives the pointer directly below it. <paramref name="acc"/> is the cumulative transformation
    /// from "root to current element's coordinate space"; when descending, it composes the element's
    /// <see cref="Element.ChildHitTestTransform"/> (so hit detection matches the display even under
    /// scaling/translation, such as in <see cref="InteractiveViewer"/>). <paramref name="hitLocal"/> returns
    /// the cumulative transformation up to the hit element (i.e. the transform used to map the pointer
    /// position into local coordinates when delivering the event).
    /// </summary>
    private static Element? HitTest(Element element, Vec2 position, Transform2D acc, out Transform2D hitLocal)
    {
        hitLocal = acc;
        if (!element.LayoutNode.Bounds.Contains(position.X, position.Y))
        {
            return null;
        }

        Vec2 childPosition = position;
        Transform2D childAcc = acc;
        if (element.ChildHitTestTransform is Transform2D t)
        {
            childPosition = t.Apply(position);
            childAcc = Transform2D.Compose(t, acc); // ルート→子＝子変換 ∘ ルート→現在
        }

        IReadOnlyList<Element> children = element.Children;
        for (int i = children.Count - 1; i >= 0; i--) // 最前面（後勝ち）から
        {
            Element? hit = HitTest(children[i], childPosition, childAcc, out hitLocal);
            if (hit is not null)
            {
                return hit;
            }
        }

        hitLocal = acc;
        return element.WantsPointer ? element : null;
    }

    private void FocusAncestor(Element element)
    {
        for (Element? current = element; current is not null; current = current.Parent)
        {
            if (current.FocusNodeOrNull is FocusNode node)
            {
                Focus.RequestFocus(node);
                return;
            }
        }
    }

    /// <summary>Set root build (immediately dirty).</summary>
    public void SetRoot(Func<Widget> build)
    {
        _build = build;
        _dirty = true;
    }

    /// <summary>
    /// Pushes a foreground overlay (modal/bottom sheet/pop-up, etc.). <paramref name="build"/> is placed at
    /// full-screen size, so build the scrim plus <c>Center</c>/<c>Positioned</c>/<c>FocusScope</c>, etc.,
    /// internally. Close it via <see cref="RemoveOverlay"/> using the returned <see cref="OverlayEntry"/>.
    /// </summary>
    public OverlayEntry PushOverlay(Func<Widget> build) => PushOverlay(build, 0f);

    /// <summary>
    /// Stacks an overlay with entry and exit animations. When <paramref name="transitionDuration"/> > 0,
    /// opening uses <paramref name="transition"/> (default <see cref="RouteTransitions.FadeScale"/>) to
    /// animate in; when <see cref="RemoveOverlay"/> is called, <b>the subtree is kept alive until the exit
    /// animation completes</b>.
    /// </summary>
    public OverlayEntry PushOverlay(Func<Widget> build, float transitionDuration, RouteTransition? transition = null, Curve? curve = null)
    {
        AnimationController? anim = null;
        if (transitionDuration > 0f)
        {
            anim = new AnimationController(this, transitionDuration, curve ?? Curves.EaseOut);
            anim.Forward(); // 0→1 で入場
        }

        var entry = new OverlayEntry(build, anim, transition);
        _overlays.Add(entry);
        _dirty = true;
        return entry;
    }

    /// <summary>
    /// Builds an overlay with a builder that receives entry/exit progress (0 to 1), for modals that want
    /// <b>different progress mappings for different parts</b> (e.g. <c>ShowDialog</c>/<c>ShowBottomSheet</c>).
    /// <see cref="RemoveOverlay"/> is responsible for keeping the overlay alive until the exit animation
    /// completes. If <paramref name="transitionDuration"/> is 0, progress is always 1.
    /// </summary>
    public OverlayEntry PushOverlay(Func<Func<float>, Widget> animatedBuild, float transitionDuration, Curve? curve = null)
    {
        AnimationController? anim = null;
        Func<float> progress = StaticOne;
        if (transitionDuration > 0f)
        {
            anim = new AnimationController(this, transitionDuration, curve ?? Curves.EaseOut);
            anim.Forward();
            AnimationController captured = anim;
            progress = () => captured.Curved;
        }

        var entry = new OverlayEntry(() => animatedBuild(progress), anim, RouteTransitions.None);
        _overlays.Add(entry);
        _dirty = true;
        return entry;
    }

    /// <summary>Closes the overlay (the pushed <see cref="OverlayEntry"/>).</summary>
    public void RemoveOverlay(OverlayEntry entry)
    {
        if (entry.Exiting)
        {
            return;
        }

        if (entry.Anim is AnimationController anim)
        {
            entry.Exiting = true;
            anim.OnCompleted = () =>
            {
                _overlays.Remove(entry);
                _dirty = true;
            };
            anim.Reverse(); // 1→0 で退場
            _dirty = true;
        }
        else if (_overlays.Remove(entry))
        {
            _dirty = true;
        }
    }

    /// <summary>Number of stacked overlays (not including during the exit animation = logical number).</summary>
    public int OverlayCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _overlays.Count; i++)
            {
                if (!_overlays[i].Exiting)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Number of overlays during drawing (including during exit animation, for inspection/testing purposes).</summary>
    internal int OverlayRenderedCount => _overlays.Count;

    /// <summary>Rebuild the whole thing on the next frame (called after the app makes imperative changes to the model).</summary>
    public void Invalidate() => _dirty = true;

    /// <summary>
    /// Transfers work from another thread (Task continuation, etc.) onto the UI thread, executed on the main
    /// thread at the start of <see cref="Update(Size, float)"/> (used by async atoms/<c>UseAsync</c> to
    /// reflect completion).
    /// </summary>
    public void Post(Action action)
    {
        lock (_postLock)
        {
            _posted.Add(action);
            _hasPosted = true;
        }
    }

    private void DrainPosted()
    {
        if (!_hasPosted)
        {
            return; // 投稿が無ければロックも取らない（async 未使用時はここで即 return＝毎フレームのロック無し）
        }

        Action[] batch;
        lock (_postLock)
        {
            batch = _posted.ToArray();
            _posted.Clear();
            _hasPosted = false;
        }

        for (int i = 0; i < batch.Length; i++)
        {
            batch[i](); // メインスレッドで実行（dirty を立てる→同フレームで反映）
        }
    }

    private readonly AtomStore _atoms = new();

    /// <summary>Global atom store (the default used outside any <see cref="StoreProvider"/> scope).</summary>
    public AtomStore GlobalStore => _atoms;

    /// <summary>Searches the scope chain for a value override registered via <see cref="Provider{T}"/> (null if none).</summary>
    internal static AtomCell? FindOverride(AtomScope? scope, object atom)
    {
        for (AtomScope? s = scope; s is not null; s = s.Parent)
        {
            if (ReferenceEquals(s.Atom, atom))
            {
                return s.Cell;
            }
        }

        return null;
    }

    /// <summary>Read global value of atom (derivation is computation; imperative access outside hooks).</summary>
    public T ReadAtom<T>(Atom<T> atom) => (T)_atoms.Read(atom)!;

    /// <summary>Updates the global value of an atom (for bidirectional/write atoms, executes the write expression); rebuilds only subscribers.</summary>
    public void WriteAtom<T>(Atom<T> atom, T value) => _atoms.Set(atom, value);

    /// <summary>Resets the global value of an atom to its initial value (equivalent to jotai's RESET).</summary>
    public void ResetAtom<T>(Atom<T> atom) => _atoms.Reset(atom);

    public void MarkDirty() => _dirty = true;

    /// <summary>Rebuilds only specific elements on the next frame, avoiding a full-tree reconcile (used by <see cref="Bind{T}"/>, etc.).</summary>
    public void MarkElementDirty(Element element)
    {
        if (!_dirtyElements.Contains(element))
        {
            _dirtyElements.Add(element);
        }
    }

    /// <summary>
    /// Combines the app body and overlays into a full-screen Stack (z-order: later wins for both painting
    /// and hit testing). Always wraps, even with no overlays, so the root type stays stable; opening/closing
    /// a modal does not recreate the application subtree, so its state (focus/scroll, etc.) is preserved
    /// without needing to be rebuilt.
    /// </summary>
    private Widget BuildEffectiveRoot()
    {
        var children = new List<Widget>(_overlays.Count + 1) { _build!() };
        for (int i = 0; i < _overlays.Count; i++)
        {
            OverlayEntry entry = _overlays[i];
            Widget content = entry.Build();
            if (entry.Anim is AnimationController anim)
            {
                content = (entry.Transition ?? RouteTransitions.FadeScale)(content, () => anim.Curved); // 入場/退場とも進捗を描画時に読む
            }

            children.Add(new OverlayHost(entry) { Key = entry, Child = content });
        }

        return new Stack { Fit = StackFit.Expand, Children = children };
    }

    /// <summary>
    /// Rebuilds if dirty and re-lays-out if the size changed. If <paramref name="dtSeconds"/> is passed,
    /// advances the internal clock and steps inertia flings, etc., via <see cref="ITicker"/> (motion marks
    /// the affected elements dirty and triggers re-layout).
    /// </summary>
    public void Update(Size available, float dtSeconds = 0f)
    {
        if (_build is null)
        {
            return;
        }

        DrainPosted(); // 別スレッドからの完了をメインスレッドで反映（dirty を立てる→このフレームで処理）

        if (_imeGuardFrames > 0)
        {
            _imeGuardFrames--; // IME 入力ガードを1フレーム消化
        }

        if (dtSeconds > 0f)
        {
            _time += dtSeconds;
            AdvanceTickers(dtSeconds);
            if (_heldButtons.Count > 0)
            {
                AdvanceHeldButtons(dtSeconds);
            }
        }

        if (available.Width != _lastSize.Width || available.Height != _lastSize.Height)
        {
            _lastSize = available;
            _needsLayout = true; // サイズ変化＝全レイアウト（部分木で済ませない）
        }

        try
        {
            if (_dirty || _root is null)
            {
                _root = Reconciler.Reconcile(_root, BuildEffectiveRoot(), new BuildContext(_text, Focus, this, EffectiveTheme));
                _dirty = false;
                _dirtyElements.Clear(); // 全再構築が吸収（個別 dirty は無効化）
                _needsLayout = true;
            }
            else if (_dirtyElements.Count > 0)
            {
                // 局所再構築＋**部分木だけ再レイアウト**（全ツリーを舐めない＝高頻度 Bind でも 60fps を保つ）。
                // サイズ変化が祖先に波及するケースは稀だが、その時は対象が安定パススルー node（Stack-expand）で
                // 親の制約が一定のため、自身の再測定で吸収される。
                for (int i = 0; i < _dirtyElements.Count; i++)
                {
                    Element element = _dirtyElements[i];
                    if (!element.IsMounted)
                    {
                        continue;
                    }

                    element.RebuildInPlace();
                    if (!_needsLayout) // 全レイアウト予定が無ければ部分木だけ
                    {
                        FlexLayoutEngine.RelayoutSubtree(element.LayoutNode);
                    }
                }

                _dirtyElements.Clear();
            }

            if (_needsLayout && _root is not null)
            {
                FlexLayoutEngine.Layout(_root.LayoutNode, BoxConstraints.Tight(available));
                _needsLayout = false;

                // レイアウトが動いたら、静止マウス下の hover を再計算（出現/消滅/移動した領域の enter/exit を拾う）。
                if (_hasHover)
                {
                    RecomputeHover(_hoverPosition);
                }
            }
        }
        catch (Exception ex) when (OnError is not null)
        {
            // build/layout の捕捉境界：このフレームを打ち切り、状態をリセットして次フレームで再試行。
            OnError(ex);
            _dirty = false;
            _dirtyElements.Clear();
            _needsLayout = false;
        }

        if (Cursor.Enabled)
        {
            UpdateFocusCursor();
        }

        // フォーカスが変わったら、その要素を所属スクロールの可視範囲へ入れる（scroll-to-focus）。
        // 変化時のみ＝手動スクロールでフォーカス先を外しても勝手に戻さない。
        if (!ReferenceEquals(Focus.Focused, _lastRevealedFocus))
        {
            _lastRevealedFocus = Focus.Focused;
            Focus.RevealFocused();
        }
    }

    /// <summary>Detects a focus change and slides the cursor from its current position to the new frame (if the focused node is unchanged, it simply follows the raw frame).</summary>
    private void UpdateFocusCursor()
    {
        _cursorAnim ??= new AnimationController(this, Cursor.GlideDuration, Cursor.Curve);
        _cursorAnim.Duration = Cursor.GlideDuration;
        _cursorAnim.Curve = Cursor.Curve;

        FocusNode? focused = Focus.Focused;
        Rect? target = Focus.FocusedNodeBounds();

        if (!ReferenceEquals(focused, _cursorNode))
        {
            if (_cursorNode is null || focused is null || target is null)
            {
                _cursorFrom = target ?? default;
                _cursorTo = target ?? default;
                _cursorAnim.JumpTo(1f); // 初回フォーカス/喪失は瞬時
            }
            else
            {
                _cursorFrom = CurrentCursorRect(); // 今いる場所から
                _cursorTo = target.Value;
                _cursorAnim.JumpTo(0f);
                _cursorAnim.Forward();
            }

            _cursorNode = focused;
        }
        else if (target is Rect live)
        {
            _cursorTo = live; // 同一ノードのレイアウト変化（スクロール等）に追従
            if (!_cursorAnim.IsAnimating)
            {
                _cursorFrom = live;
            }
        }
    }

    private Rect CurrentCursorRect()
    {
        float t = _cursorAnim?.Curved ?? 1f;
        return new Rect(
            _cursorFrom.X + ((_cursorTo.X - _cursorFrom.X) * t),
            _cursorFrom.Y + ((_cursorTo.Y - _cursorFrom.Y) * t),
            _cursorFrom.Width + ((_cursorTo.Width - _cursorFrom.Width) * t),
            _cursorFrom.Height + ((_cursorTo.Height - _cursorFrom.Height) * t));
    }

    private static Rect Pad(Rect r, float p) => new(r.X - p, r.Y - p, r.Width + (2f * p), r.Height + (2f * p));

    private static Rect ScaleRectAboutCenter(Rect r, float scale)
    {
        if (MathF.Abs(scale - 1f) < 0.0005f)
        {
            return r;
        }

        float dw = r.Width * (scale - 1f);
        float dh = r.Height * (scale - 1f);
        return new Rect(r.X - (dw / 2f), r.Y - (dh / 2f), r.Width + dw, r.Height + dh);
    }

    private static Color WithPulse(Color color, float pulse) =>
        new(color.R, color.G, color.B, (int)Math.Clamp(color.A * pulse, 0f, 255f));

    private float CursorPulseAlpha()
    {
        if (Cursor.PulseAmplitude <= 0f || Cursor.PulsePeriod <= 0f)
        {
            return 1f;
        }

        float phase = (_time / Cursor.PulsePeriod) * MathF.Tau;
        return 1f - (Cursor.PulseAmplitude * (0.5f - (0.5f * MathF.Cos(phase)))); // [1-amp, 1]
    }

    private void AdvanceTickers(float dt)
    {
        for (int i = _tickers.Count - 1; i >= 0; i--) // 後ろから＝Tick 中の RemoveAt が安全
        {
            if (!_tickers[i].Tick(dt))
            {
                _tickers.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Draws the tree to the rendering backend, between <paramref name="painter"/>'s BeginFrame/EndFrame, and
    /// layers the focus cursor on top. The backend implementation is responsible for <see cref="IPainter"/>
    /// (the core stays engine-independent).
    /// </summary>
    public void Render(IPainter painter)
    {
        if (_root is null)
        {
            return;
        }

        painter.BeginFrame();
        var context = new PaintContext(painter, _text);
        if (_devicePixelRatio != 1f) // 論理pt レイアウトを物理px へスケール（HiDPI/Retina）。テキストもこの比率でラスタライズされる。
        {
            context = context.WithTransform(new Transform2D(new Vec2(_devicePixelRatio, _devicePixelRatio), Vec2.Zero));
        }

        try
        {
            _root.Paint(context);
            if (FocusCursorRect is Rect cursor) // 最前面にフォーカスカーソルを重ねる
            {
                float pulse = CursorPulseAlpha();
                Rect scaled = ScaleRectAboutCenter(cursor, Cursor.Scale);
                if (Cursor.Renderer is { } draw)
                {
                    draw(context, scaled, pulse); // 完全カスタム描画
                }
                else
                {
                    if (Cursor.Background is Color bg)
                    {
                        context.FillRoundedRect(scaled, WithPulse(bg, pulse), Cursor.Radius);
                    }

                    context.DrawOutline(scaled, WithPulse(Cursor.Color, pulse), Cursor.Thickness);
                }
            }
        }
        catch (Exception ex) when (OnError is not null)
        {
            OnError(ex); // 描画の捕捉境界：このフレームの残り描画を諦め、フレームは正常に閉じる
        }

        painter.EndFrame();
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Identifier for a stacked overlay (the return value of <see cref="HamonRoot.PushOverlay"/>). Also serves as
/// a stable identity (Key) for reconcile. Close it via <see cref="HamonRoot.RemoveOverlay"/>.
/// </summary>
public sealed class OverlayEntry
{
    private readonly Func<Widget> _build;

    internal OverlayEntry(Func<Widget> build, AnimationController? anim, RouteTransition? transition)
    {
        _build = build;
        Anim = anim;
        Transition = transition;
    }

    /// <summary>Driver of entry/exit progress (only present when a transition is specified via <see cref="HamonRoot.PushOverlay(Func{Widget}, float, RouteTransition?, Curve?)"/>).</summary>
    internal AnimationController? Anim { get; }

    internal RouteTransition? Transition { get; }

    /// <summary>During the exit animation (continue drawing until completion).</summary>
    internal bool Exiting { get; set; }

    internal Widget Build() => _build();
}

/// <summary>Transparent wrapper that holds one overlay layer (reconcile is stabilized via Key; adds no layout box).</summary>
internal sealed class OverlayHost : StatelessWidget
{
    public OverlayHost(OverlayEntry entry) => Entry = entry;

    public OverlayEntry Entry { get; }

    public Widget? Child { get; init; }

    public override Widget Build(BuildContext context) => Child ?? new Stack();
}

/// <summary>
/// Reactive state; setting its value marks the owning <see cref="HamonRoot"/> dirty so it rebuilds on the
/// next frame.
/// </summary>
public sealed class State<T>
{
    private readonly IHamonHost _owner;
    private T _value;

    internal State(IHamonHost owner, T value)
    {
        _owner = owner;
        _value = value;
    }

    public T Value
    {
        get => _value;
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                _value = value;
                _owner.MarkDirty();
            }
        }
    }
}
