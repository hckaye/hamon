using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Pointer (touch/mouse) phase.</summary>
public enum PointerPhase : byte
{
    Down,
    Move,
    Up,

    /// <summary>It was taken over by another recognizer (scroll, etc.) in arbitration, and this series was discontinued (tap, etc. did not fire).</summary>
    Cancel,
}

/// <summary>Pointer event (position is UI coordinates px, time is seconds).<see cref="Timestamp"/>is used for speed estimation and long press/repeat hit determination.</summary>
public readonly struct PointerEvent
{
    public PointerEvent(Vec2 position, PointerPhase phase, float timestamp = 0f, int pointerId = 0)
    {
        Position = position;
        Phase = phase;
        Timestamp = timestamp;
        PointerId = pointerId;
    }

    public Vec2 Position { get; }

    public PointerPhase Phase { get; }

    /// <summary>Event time (seconds, monotonically increasing). <see cref="HamonRoot"/>will be supplemented with an internal clock upon delivery.</summary>
    public float Timestamp { get; }

    /// <summary>
    /// Pointer (finger) identifier.<see cref="HamonRoot"/>is captured and delivered independently for each ID (multi-touch).
    /// Default 0 = primary pointer (mouse/single touch).
    /// </summary>
    public int PointerId { get; }
}

/// <summary>Pinch (two-finger scale) start information (Flutter<c>ScaleStartDetails</c>equivalent).</summary>
public readonly struct ScaleStartDetails
{
    public ScaleStartDetails(Vec2 focalPoint, int pointerCount)
    {
        FocalPoint = focalPoint;
        PointerCount = pointerCount;
    }

    /// <summary>Midpoint of two fingers (UI coordinates px) = pinch reference point.</summary>
    public Vec2 FocalPoint { get; }

    /// <summary>Number of pointers involved (currently 2).</summary>
    public int PointerCount { get; }
}

/// <summary>Pinch update information (Flutter<c>ScaleUpdateDetails</c>equivalent). </summary>
public readonly struct ScaleUpdateDetails
{
    public ScaleUpdateDetails(Vec2 focalPoint, float scale, float rotation, int pointerCount)
    {
        FocalPoint = focalPoint;
        Scale = scale;
        Rotation = rotation;
        PointerCount = pointerCount;
    }

    /// <summary>Midpoint of two fingers (UI coordinates px).</summary>
    public Vec2 FocalPoint { get; }

    /// <summary>Magnification of the starting finger distance (1 = no change, >1 = widened = enlarged, <1 = narrowed = reduced).</summary>
    public float Scale { get; }

    /// <summary>Relative rotation from start (in radians).</summary>
    public float Rotation { get; }

    /// <summary>Number of pointers involved (currently 2).</summary>
    public int PointerCount { get; }
}

/// <summary>Pinch end information (Flutter<c>ScaleEndDetails</c>equivalent). </summary>
public readonly struct ScaleEndDetails
{
}

/// <summary>Drag (one finger pan) start information (Flutter<c>DragStartDetails</c>equivalent).</summary>
public readonly struct DragStartDetails
{
    public DragStartDetails(Vec2 position) => Position = position;

    /// <summary>Pan start position = pressed position (UI coordinates px).</summary>
    public Vec2 Position { get; }
}

/// <summary>Drag (one finger pan) update information (Flutter<c>DragUpdateDetails</c>equivalent).</summary>
public readonly struct DragUpdateDetails
{
    public DragUpdateDetails(Vec2 position, Vec2 delta)
    {
        Position = position;
        Delta = delta;
    }

    /// <summary>Current position (UI coordinates px).</summary>
    public Vec2 Position { get; }

    /// <summary>Amount of movement (px) since the last update. </summary>
    public Vec2 Delta { get; }
}

/// <summary>Drag (one finger pan) end information (Flutter<c>DragEndDetails</c>equivalent). </summary>
public readonly struct DragEndDetails
{
}

/// <summary>
/// Detect child pointer operations (Flutter<c>GestureDetector</c>(minimum version).
/// The sequence from Down is<see cref="HamonRoot"/>captures and delivers to the same element.
/// </summary>
public sealed class GestureDetector : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public Action? OnTap { get; init; }

    public Action? OnTapDown { get; init; }

    public Action? OnTapUp { get; init; }

    /// <summary>
    /// When it is determined that the tap is not successful (Flutter<c>onTapCancel</c>).
    /// Fires when arbitration is taken by a scroll, etc. (pointer cancel)/disguised as a long press.
    /// </summary>
    public Action? OnTapCancel { get; init; }

    /// <summary>Long press (keep pressed and do not move)<see cref="LongPressDuration"/>seconds elapsed). </summary>
    public Action? OnLongPress { get; init; }

    /// <summary>for a short time (<see cref="DoubleTapWindow"/>2 consecutive taps (within seconds). </summary>
    public Action? OnDoubleTap { get; init; }

    /// <summary>
    /// Pinch (two-finger scale) start (Flutter<c>onScaleStart</c>equivalent).
    /// Any tap/long press in progress will be aborted (<see cref="OnTapCancel"/>）。<b>The child does not take the pointer by itself (= in the hit test
    /// This element is the front)</b>is condition = visual only child (<c>Container</c>/<c>Image</c>/<c>SceneView</c>(OnPointer not set)).
    /// </summary>
    public Action<ScaleStartDetails>? OnScaleStart { get; init; }

    /// <summary>Pinch update (Flutter<c>onScaleUpdate</c>equivalent). <see cref="ScaleUpdateDetails.Scale"/>/rotation/midpoint.</summary>
    public Action<ScaleUpdateDetails>? OnScaleUpdate { get; init; }

    /// <summary>Pinch end (Flutter<c>onScaleEnd</c>equivalent). </summary>
    public Action<ScaleEndDetails>? OnScaleEnd { get; init; }

    /// <summary>
    /// Start one finger pan (Flutter<c>onPanStart</c>equivalent).
    /// Any tap/long press in progress will be aborted (<see cref="OnTapCancel"/>). <see cref="OnPanUpdate"/>and distribute delta.
    /// When the second finger falls<see cref="OnScaleStart"/>Pan was promoted to<see cref="OnPanEnd"/>It ends with
    /// </summary>
    public Action<DragStartDetails>? OnPanStart { get; init; }

    /// <summary>One finger pan update (Flutter<c>onPanUpdate</c>equivalent).<see cref="DragUpdateDetails.Delta"/>Add to the parallel translation.</summary>
    public Action<DragUpdateDetails>? OnPanUpdate { get; init; }

    /// <summary>One finger pan finished (Flutter<c>onPanEnd</c>equivalent). </summary>
    public Action<DragEndDetails>? OnPanEnd { get; init; }

    /// <summary>Number of seconds to hold to consider a long press (default 0.5 seconds).</summary>
    public float LongPressDuration { get; init; } = 0.5f;

    /// <summary>Number of seconds between two taps to be considered a double tap (default 0.3 seconds).</summary>
    public float DoubleTapWindow { get; init; } = 0.3f;

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Box };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new GestureDetectorElement(this);
}

/// <summary>
/// <see cref="GestureDetector"/>holding entity.
/// <see cref="ITicker"/>(fires when the threshold is exceeded without moving the button while it is pressed).
/// </summary>
internal sealed class GestureDetectorElement : RenderElement, ITicker
{
    private const float MoveSlop = 10f; // この距離を超えて動いたら長押しを取り消す
    private const int NoPointer = int.MinValue;

    // 主ポインタ（タップ/長押し/ダブルタップ）の状態。
    private int _primaryId = NoPointer;
    private Vec2 _primaryPos;
    private bool _pressed;
    private bool _longPressed;
    private bool _ticking;
    private float _held;
    private Vec2 _downPosition;
    private float _lastTapTime = float.NegativeInfinity;

    // 2本目のポインタ（ピンチ＝スケール/回転）。
    private int _secondaryId = NoPointer;
    private Vec2 _secondaryPos;
    private bool _scaling;
    private float _startDistance;
    private float _startAngle;

    // 1本指パン（主ポインタが slop を超えて動いたら開始）。
    private bool _panning;
    private Vec2 _lastPanPos;

    public GestureDetectorElement(GestureDetector widget)
        : base(widget)
    {
    }

    public override bool WantsPointer => true;

    public override void Unmount()
    {
        StopLongPressTimer();
        base.Unmount();
    }

    public override void HandlePointer(in PointerEvent pointer)
    {
        var widget = (GestureDetector)Widget;
        switch (pointer.Phase)
        {
            case PointerPhase.Down:
                HandleDown(widget, pointer);
                break;
            case PointerPhase.Move:
                HandleMove(widget, pointer);
                break;
            case PointerPhase.Up:
                HandleUp(widget, pointer);
                break;
            case PointerPhase.Cancel:
                HandleCancel(widget, pointer);
                break;
        }
    }

    private void HandleDown(GestureDetector widget, in PointerEvent pointer)
    {
        if (_primaryId == NoPointer)
        {
            // 1本目＝タップ/長押し経路。
            _primaryId = pointer.PointerId;
            _primaryPos = pointer.Position;
            _pressed = true;
            _longPressed = false;
            _held = 0f;
            _downPosition = pointer.Position;
            widget.OnTapDown?.Invoke();
            if (widget.OnLongPress is not null)
            {
                StartLongPressTimer();
            }
        }
        else if (_secondaryId == NoPointer && pointer.PointerId != _primaryId && WantsScale(widget))
        {
            // 2本目＝ピンチへ昇格。進行中のタップ/長押し/パンは打ち切る。
            _secondaryId = pointer.PointerId;
            _secondaryPos = pointer.Position;
            StopLongPressTimer();
            if (_panning)
            {
                _panning = false;
                widget.OnPanEnd?.Invoke(new DragEndDetails());
            }
            else if (_pressed && !_longPressed)
            {
                widget.OnTapCancel?.Invoke();
            }

            _pressed = false;
            _scaling = true;
            _startDistance = MathF.Max(Distance(_primaryPos, _secondaryPos), 0.0001f);
            _startAngle = Angle(_primaryPos, _secondaryPos);
            widget.OnScaleStart?.Invoke(new ScaleStartDetails(Focal(), 2));
        }
    }

    private void HandleMove(GestureDetector widget, in PointerEvent pointer)
    {
        if (pointer.PointerId == _primaryId)
        {
            _primaryPos = pointer.Position;
            if (!_scaling)
            {
                if (_panning)
                {
                    widget.OnPanUpdate?.Invoke(new DragUpdateDetails(pointer.Position, pointer.Position - _lastPanPos));
                    _lastPanPos = pointer.Position;
                }
                else if (_pressed && Distance(pointer.Position, _downPosition) > MoveSlop)
                {
                    StopLongPressTimer(); // 動いたら長押しは成立しない
                    if (WantsPan(widget))
                    {
                        // slop 超え＝パン開始（タップ/長押しに優先）。進行中タップは打ち切る。
                        _panning = true;
                        if (!_longPressed)
                        {
                            widget.OnTapCancel?.Invoke();
                        }

                        _pressed = false;
                        widget.OnPanStart?.Invoke(new DragStartDetails(_downPosition));
                        widget.OnPanUpdate?.Invoke(new DragUpdateDetails(pointer.Position, pointer.Position - _downPosition));
                        _lastPanPos = pointer.Position;
                    }
                }
            }
        }
        else if (pointer.PointerId == _secondaryId)
        {
            _secondaryPos = pointer.Position;
        }
        else
        {
            return;
        }

        if (_scaling && _primaryId != NoPointer && _secondaryId != NoPointer)
        {
            float scale = Distance(_primaryPos, _secondaryPos) / _startDistance;
            float rotation = Angle(_primaryPos, _secondaryPos) - _startAngle;
            widget.OnScaleUpdate?.Invoke(new ScaleUpdateDetails(Focal(), scale, rotation, 2));
        }
    }

    private void HandleUp(GestureDetector widget, in PointerEvent pointer)
    {
        if (_scaling && (pointer.PointerId == _primaryId || pointer.PointerId == _secondaryId))
        {
            EndScale(widget); // ピンチ中はどちらの指が離れても終了（残った指で新規タップは始めない）
            return;
        }

        if (pointer.PointerId != _primaryId)
        {
            return; // 主ポインタ以外の解放（ピンチ外）は無視
        }

        if (_panning)
        {
            _panning = false;
            widget.OnPanEnd?.Invoke(new DragEndDetails());
            _pressed = false;
            _primaryId = NoPointer;
            return; // パンはタップを発火しない
        }

        StopLongPressTimer();
        if (_pressed && !_longPressed)
        {
            if (LayoutNode.Bounds.Contains(pointer.Position.X, pointer.Position.Y))
            {
                if (widget.OnDoubleTap is not null && pointer.Timestamp - _lastTapTime <= widget.DoubleTapWindow)
                {
                    widget.OnDoubleTap();
                    _lastTapTime = float.NegativeInfinity; // 3連打目は新規扱い
                }
                else
                {
                    widget.OnTap?.Invoke();
                    _lastTapTime = pointer.Timestamp;
                }
            }
            else
            {
                widget.OnTapCancel?.Invoke(); // 範囲外で離した＝タップ不成立
            }
        }

        widget.OnTapUp?.Invoke();
        _pressed = false;
        _primaryId = NoPointer;
    }

    private void HandleCancel(GestureDetector widget, in PointerEvent pointer)
    {
        if (_scaling && (pointer.PointerId == _primaryId || pointer.PointerId == _secondaryId))
        {
            EndScale(widget);
            return;
        }

        if (pointer.PointerId != _primaryId)
        {
            return;
        }

        // 調停でスクロール等に取られた＝タップ/長押し/パンは発火させない（パンは終了通知）。
        StopLongPressTimer();
        if (_panning)
        {
            _panning = false;
            widget.OnPanEnd?.Invoke(new DragEndDetails());
        }
        else if (_pressed && !_longPressed)
        {
            widget.OnTapCancel?.Invoke();
        }

        _pressed = false;
        _primaryId = NoPointer;
    }

    private void EndScale(GestureDetector widget)
    {
        _scaling = false;
        _panning = false;
        _primaryId = NoPointer;
        _secondaryId = NoPointer;
        _pressed = false;
        widget.OnScaleEnd?.Invoke(new ScaleEndDetails());
    }

    private static bool WantsScale(GestureDetector w) =>
        w.OnScaleStart is not null || w.OnScaleUpdate is not null || w.OnScaleEnd is not null;

    private static bool WantsPan(GestureDetector w) =>
        w.OnPanStart is not null || w.OnPanUpdate is not null || w.OnPanEnd is not null;

    private Vec2 Focal() => new((_primaryPos.X + _secondaryPos.X) / 2f, (_primaryPos.Y + _secondaryPos.Y) / 2f);

    private static float Angle(Vec2 a, Vec2 b) => MathF.Atan2(b.Y - a.Y, b.X - a.X);

    bool ITicker.Tick(float dt)
    {
        if (!_pressed)
        {
            _ticking = false;
            return false;
        }

        _held += dt;
        if (_held >= ((GestureDetector)Widget).LongPressDuration)
        {
            _longPressed = true;
            var widget = (GestureDetector)Widget;
            widget.OnTapCancel?.Invoke();  // タップ認識器は長押しに敗退＝tap cancel（Flutter と同じ順）
            widget.OnLongPress?.Invoke();
            _ticking = false;
            return false; // 長押し確定＝計時終了（Up でタップは抑止される）
        }

        return true;
    }

    private void StartLongPressTimer()
    {
        if (_ticking)
        {
            return;
        }

        _ticking = true;
        Context.Owner?.RegisterTicker(this);
    }

    private void StopLongPressTimer()
    {
        if (!_ticking)
        {
            return;
        }

        _ticking = false;
        Context.Owner?.UnregisterTicker(this);
    }

    private static float Distance(Vec2 a, Vec2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
