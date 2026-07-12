using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Pointer (touch/mouse) phase.</summary>
public enum PointerPhase : byte
{
    Down,
    Move,
    Up,

    /// <summary>The gesture was taken over by another recognizer (e.g. a scroll) during arbitration, so this pointer series was discontinued (tap, etc. did not fire).</summary>
    Cancel,
}

/// <summary>A pointer event (position is in UI coordinates, px; time is in seconds). <see cref="Timestamp"/> is used for velocity estimation and for detecting long presses and repeated taps.</summary>
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

    /// <summary>Event time in seconds, monotonically increasing. Filled in by <see cref="HamonRoot"/>'s internal clock at delivery time.</summary>
    public float Timestamp { get; }

    /// <summary>
    /// Pointer (finger) identifier. <see cref="HamonRoot"/> captures and delivers
    /// events independently for each ID, enabling multi-touch. Defaults to 0, the
    /// primary pointer (mouse or single touch).
    /// </summary>
    public int PointerId { get; }
}

/// <summary>Pinch (two-finger scale) start information (the equivalent of Flutter's <c>ScaleStartDetails</c>).</summary>
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

/// <summary>Pinch update information (the equivalent of Flutter's <c>ScaleUpdateDetails</c>).</summary>
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

/// <summary>Pinch end information (the equivalent of Flutter's <c>ScaleEndDetails</c>).</summary>
public readonly struct ScaleEndDetails
{
}

/// <summary>Drag (one finger pan) start information (the equivalent of Flutter's <c>DragStartDetails</c>).</summary>
public readonly struct DragStartDetails
{
    public DragStartDetails(Vec2 position) => Position = position;

    /// <summary>Pan start position = pressed position (UI coordinates px).</summary>
    public Vec2 Position { get; }
}

/// <summary>Drag (one finger pan) update information (the equivalent of Flutter's <c>DragUpdateDetails</c>).</summary>
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

/// <summary>Drag (one finger pan) end information (the equivalent of Flutter's <c>DragEndDetails</c>).</summary>
public readonly struct DragEndDetails
{
}

/// <summary>
/// Detects pointer gestures on its child (a minimal version of Flutter's
/// <c>GestureDetector</c>). The sequence starting from Down is captured by
/// <see cref="HamonRoot"/> and delivered to the same element throughout.
/// </summary>
public sealed class GestureDetector : Widget, IRenderConfig
{
    public Widget? Child { get; init; }

    public Action? OnTap { get; init; }

    public Action? OnTapDown { get; init; }

    public Action? OnTapUp { get; init; }

    /// <summary>
    /// Fires when a tap is determined not to have succeeded (the equivalent of
    /// Flutter's <c>onTapCancel</c>). This happens when the gesture is taken over
    /// during arbitration (e.g. by a scroll, via pointer cancel) or when it turns
    /// into a long press instead.
    /// </summary>
    public Action? OnTapCancel { get; init; }

    /// <summary>Fires when the pointer is held down without moving for <see cref="LongPressDuration"/> seconds (a long press).</summary>
    public Action? OnLongPress { get; init; }

    /// <summary>Fires on two consecutive taps within <see cref="DoubleTapWindow"/> seconds of each other (a double tap).</summary>
    public Action? OnDoubleTap { get; init; }

    /// <summary>
    /// Fires when a two-finger pinch (scale) gesture starts (the equivalent of
    /// Flutter's <c>onScaleStart</c>). Aborts any tap or long press in progress
    /// (<see cref="OnTapCancel"/>). <b>Requires that the child not claim the pointer
    /// itself</b> (i.e., this element must be the frontmost hit in the hit test) —
    /// for example, a purely visual child such as <c>Container</c>, <c>Image</c>, or
    /// <c>SceneView</c> with no pointer handling of its own.
    /// </summary>
    public Action<ScaleStartDetails>? OnScaleStart { get; init; }

    /// <summary>Fires on pinch updates (the equivalent of Flutter's <c>onScaleUpdate</c>), carrying <see cref="ScaleUpdateDetails.Scale"/>, rotation, and the focal midpoint.</summary>
    public Action<ScaleUpdateDetails>? OnScaleUpdate { get; init; }

    /// <summary>Fires when the pinch gesture ends (the equivalent of Flutter's <c>onScaleEnd</c>).</summary>
    public Action<ScaleEndDetails>? OnScaleEnd { get; init; }

    /// <summary>
    /// Fires when a one-finger pan starts (the equivalent of Flutter's
    /// <c>onPanStart</c>). Aborts any tap or long press in progress
    /// (<see cref="OnTapCancel"/>) and starts delivering deltas via
    /// <see cref="OnPanUpdate"/>. If a second finger comes down, the pan is promoted
    /// to <see cref="OnScaleStart"/> and ends via <see cref="OnPanEnd"/>.
    /// </summary>
    public Action<DragStartDetails>? OnPanStart { get; init; }

    /// <summary>Fires on one-finger pan updates (the equivalent of Flutter's <c>onPanUpdate</c>). Accumulate <see cref="DragUpdateDetails.Delta"/> to get the total translation.</summary>
    public Action<DragUpdateDetails>? OnPanUpdate { get; init; }

    /// <summary>Fires when a one-finger pan ends (the equivalent of Flutter's <c>onPanEnd</c>).</summary>
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
/// The <see cref="Element"/> that backs a <see cref="GestureDetector"/> widget.
/// Implements <see cref="ITicker"/> to detect long presses (fires once the hold
/// duration is exceeded without the pointer moving beyond the slop threshold).
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
