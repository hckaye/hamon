using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Animated elements that advance every frame (such as inertial flings). <see cref="HamonRoot.Update(Size, float)"/>
/// calls <see cref="Tick"/> with dt in seconds.
/// </summary>
public interface ITicker
{
    bool Tick(float dtSeconds);
}

/// <summary>
/// Estimates speed (px/sec) from the time series of the pointer's main-axis position.
/// Computes the average speed from the difference between the oldest and latest samples (allocation only in
/// the constructor = zero allocation per frame).
/// </summary>
internal sealed class VelocityTracker
{
    private const int Capacity = 6;
    private readonly float[] _time = new float[Capacity];
    private readonly float[] _pos = new float[Capacity];
    private int _count;
    private int _head; // 次に書く位置

    public void Reset()
    {
        _count = 0;
        _head = 0;
    }

    public void Add(float time, float position)
    {
        _time[_head] = time;
        _pos[_head] = position;
        _head = (_head + 1) % Capacity;
        if (_count < Capacity)
        {
            _count++;
        }
    }

    /// <summary>Estimated speed (px/sec). </summary>
    public float Velocity()
    {
        if (_count < 2)
        {
            return 0f;
        }

        int oldest = ((_head - _count) % Capacity + Capacity) % Capacity;
        int newest = (_head - 1 + Capacity) % Capacity;
        float dt = _time[newest] - _time[oldest];
        if (dt <= 1e-4f)
        {
            return 0f;
        }

        return (_pos[newest] - _pos[oldest]) / dt;
    }
}

/// <summary>
/// Common drag-to-scroll logic plus inertial fling at the release speed, held by <see cref="ScrollView"/>/
/// <see cref="ListView"/>/<see cref="GridView"/>. Speed is estimated via <see cref="VelocityTracker"/>; on
/// pointer Up, this registers with the owner as an <see cref="ITicker"/> and advances the offset with
/// exponential decay, ending once the speed falls below the threshold. <see cref="Owner"/> is set when the
/// element is mounted.
/// </summary>
internal sealed class DragScroller : ITicker
{
    // すべての動き定数は対象の <see cref="ScrollPhysics"/>（ウィジェット指定→テーマ既定）から取る＝調整可能。
    private ScrollPhysics P => _target.Physics;

    private enum Mode : byte
    {
        None,
        Momentum,   // 速度＋摩擦（ドラッグ離しの慣性フリング）
        Glide,      // 目標 offset へ指数補間（ゲームパッド/scroll-to-focus＝グリッド単位移動）
        Continuous, // ホイール/トラックパッド＝連続スクロール（慣性なし・端でゴムバンド＋pull ホールド）
    }

    private readonly IScrollable _target;
    private readonly VelocityTracker _tracker = new();

    private Mode _mode;
    private bool _dragging;
    private float _lastMain;
    private float _dragEff;      // ドラッグ中の未clamp実効offset（オーバースクロール込み）
    private float _velocity;     // offset 速度（px/秒）
    private float _targetOffset; // グライドの目標 offset
    private float _overscroll;   // 実効offset − clamped（符号付き。<0=先端越え,>0=終端越え。描画で content を平行移動）
    private float _contTarget;   // 連続入力の実効目標（オーバースクロール込み・未clamp）
    private float _contPos;      // 連続入力の平滑現在位置（_contTarget へ指数追従）
    private float _inputAge;     // 最後の連続入力からの経過秒（ホールド→ゴムバンド復帰の判定）
    private bool _ticking;

    public DragScroller(IScrollable target) => _target = target;

    /// <summary>Ticker registration destination, set to <c>Context.Owner</c> when the element is mounted.</summary>
    public IHamonHost? Owner { get; set; }

    /// <summary>Current inertial velocity (offset px/sec. for inspection/testing).</summary>
    public float Velocity => _velocity;

    /// <summary>Overscroll amount (translate content when drawing; 0=within bounds).</summary>
    public float Overscroll => _overscroll;

    private bool Bounce => _target.BounceEnabled;

    private float Max => _target.MaxScroll;

    public void HandlePointer(in PointerEvent pointer, float main)
    {
        switch (pointer.Phase)
        {
            case PointerPhase.Down:
                Stop();
                _velocity = 0f;
                _dragging = true;
                _lastMain = main;
                _dragEff = _target.ScrollOffset + _overscroll;
                _tracker.Reset();
                _tracker.Add(pointer.Timestamp, main);
                break;
            case PointerPhase.Move when _dragging:
                float delta = main - _lastMain;
                _lastMain = main;
                _tracker.Add(pointer.Timestamp, main);
                _dragEff -= delta; // 指を上へ＝offset 増＝下へスクロール
                float clamped = Math.Clamp(_dragEff, 0f, Max);
                _target.SetScroll(clamped);
                if (Bounce)
                {
                    _overscroll = RubberBand(_dragEff - clamped); // 端を越えたら抵抗付きで引っ張れる
                }
                else
                {
                    _overscroll = 0f;
                    _dragEff = clamped; // 端で蓄積しない
                }

                break;
            case PointerPhase.Up when _dragging:
                _dragging = false;
                _tracker.Add(pointer.Timestamp, main);
                _velocity = Math.Clamp(-_tracker.Velocity(), -P.MaxFlingSpeed, P.MaxFlingSpeed);
                StartMomentum(); // 慣性＋（越えていれば）復帰
                break;
            case PointerPhase.Cancel:
                _dragging = false;
                StartMomentum();
                break;
        }
    }

    /// <summary>
    /// Wheel/trackpad two-finger scrolling = <b>continuous scroll</b> (no inertia), smoothly converging to the
    /// target.
    /// <para>
    /// When input goes past the edge and <see cref="BounceEnabled"/> is set, it pulls with a rubber band (with
    /// resistance); <b>as long as you keep inputting while pulling, it holds in place</b> (equivalent to
    /// pull-and-hold, as in pull-to-refresh UIs). Once input stops, it springs back to the boundary. If
    /// <see cref="BounceEnabled"/> is false, it stops at the edge without overscrolling (opt-out).
    /// </para>
    /// Arrow keys / D-pad up-down for focus movement use <c>scroll-to-focus</c> (see <see cref="GlideTo"/>)
    /// instead, which operates in grid units.
    /// </summary>
    public void ScrollBy(float offsetDelta)
    {
        if (Owner is null || _dragging)
        {
            return;
        }

        // 連続入力でなければ現在の実効位置（offset＋オーバースクロール）から開始。継続中なら目標へ積み増し。
        if (_mode != Mode.Continuous || !_ticking)
        {
            _contPos = _target.ScrollOffset + _overscroll;
            _contTarget = _contPos;
        }

        // 感度（小さいほど鈍く）を掛ける。バウンス可なら端を越えられる（ゴムバンドが描画で抵抗を掛ける）。不可なら境界で止める。
        float scaled = offsetDelta * P.WheelSensitivity;
        float lo = Bounce ? -2f * P.MaxOverscroll : 0f;
        float hi = Bounce ? Max + (2f * P.MaxOverscroll) : Max;
        _contTarget = Math.Clamp(_contTarget + scaled, lo, hi);
        _inputAge = 0f; // 入力が来た＝ホールド継続（離すまでゴムバンドを戻さない）
        _mode = Mode.Continuous;
        _velocity = 0f;
        StartTicker();
    }

    /// <summary>Gamepad / scroll-to-focus: glides smoothly to the specified offset (moves by the target amount).</summary>
    public void GlideTo(float targetOffset)
    {
        if (Owner is null || _dragging)
        {
            return;
        }

        _targetOffset = Math.Clamp(targetOffset, 0f, Max);
        if (MathF.Abs(_targetOffset - _target.ScrollOffset) < 0.5f)
        {
            return;
        }

        _mode = Mode.Glide;
        _velocity = 0f;
        StartTicker();
    }

    public bool Tick(float dt)
    {
        if (!_ticking || dt <= 0f)
        {
            return _ticking;
        }

        return _mode switch
        {
            Mode.Glide => GlideTick(dt),
            Mode.Continuous => ContinuousTick(dt),
            _ => MomentumTick(dt),
        };
    }

    // ホイール/トラックパッド連続スクロール：目標へ平滑追従。端では入力中はゴムバンドを保持、入力が止まればバネで戻す。
    private bool ContinuousTick(float dt)
    {
        _inputAge += dt;

        // 入力が途切れたら、オーバースクロール分の目標を境界へバネで戻す（pull して離す＝ゴムバンド復帰）。
        // 入力継続中（_inputAge<=P.HoldWindow）は戻さない＝引っ張ったまま静止保持。
        if (_inputAge > P.HoldWindow)
        {
            float edge = Math.Clamp(_contTarget, 0f, Max);
            if (_contTarget != edge)
            {
                _contTarget += (edge - _contTarget) * (1f - MathF.Exp(-P.SpringRate * dt));
                if (MathF.Abs(_contTarget - edge) < 0.5f)
                {
                    _contTarget = edge;
                }
            }
        }

        // 現在位置を目標へ滑らかに寄せる。
        _contPos += (_contTarget - _contPos) * (1f - MathF.Exp(-P.GlideRate * dt));

        float clamped = Math.Clamp(_contPos, 0f, Max);
        _target.SetScroll(clamped);
        if (Bounce)
        {
            _overscroll = RubberBand(_contPos - clamped); // 端越えは抵抗付きで引っ張れる
        }
        else
        {
            _overscroll = 0f;
            _contPos = clamped; // 端で蓄積しない（opt-out）
            _contTarget = Math.Clamp(_contTarget, 0f, Max);
        }

        // 整定：入力が止まり、現在位置が目標に到達し、オーバースクロールが解消したら終了。
        bool atTarget = MathF.Abs(_contPos - _contTarget) < 0.5f;
        if (_inputAge > P.HoldWindow && atTarget && MathF.Abs(_overscroll) < 0.5f)
        {
            _overscroll = 0f;
            _target.SetScroll(clamped);
            ResetState();
            return false;
        }

        return true;
    }

    private bool MomentumTick(float dt)
    {
        // オーバースクロール中：バネ・ダンパで連続的に境界へ戻す（速度がそのまま流れ込む＝1回の滑らかな弾み）。
        if (_overscroll != 0f)
        {
            if (!Bounce)
            {
                _overscroll = 0f; // バウンス無効ならそのまま境界内処理へ
            }
            else
            {
                _velocity += -P.DragSpringStiffness * _overscroll * dt; // 境界へ引き戻す
                _velocity *= MathF.Exp(-P.DragSpringDamping * dt);        // 減衰（振動を抑える）
                _overscroll = Math.Clamp(_overscroll + (_velocity * dt), -P.MaxOverscroll, P.MaxOverscroll);

                if (MathF.Abs(_overscroll) < 0.5f)
                {
                    _overscroll = 0f; // 境界へ戻った：残速度があれば下の境界内処理へ流す
                    if (MathF.Abs(_velocity) < P.StopSpeed)
                    {
                        _velocity = 0f;
                        ResetState();
                        return false;
                    }
                }
                else
                {
                    return true; // まだ越えている＝バネ継続
                }
            }
        }

        float offset = _target.ScrollOffset;
        float next = offset + (_velocity * dt);
        _velocity *= MathF.Exp(-P.Friction * dt);

        if (next < 0f || next > Max)
        {
            float bound = next < 0f ? 0f : Max;
            _target.SetScroll(bound);
            if (Bounce)
            {
                _overscroll += next - bound; // 越えた分をオーバースクロールへ（速度は維持＝連続的に弾む）
            }
            else
            {
                _velocity = 0f;
            }
        }
        else
        {
            _target.SetScroll(next);
        }

        if (MathF.Abs(_velocity) < P.StopSpeed && _overscroll == 0f)
        {
            ResetState();
            return false;
        }

        return true;
    }

    private bool GlideTick(float dt)
    {
        float cur = _target.ScrollOffset;
        float diff = _targetOffset - cur;
        if (MathF.Abs(diff) < 0.5f)
        {
            _target.SetScroll(_targetOffset);
            ResetState();
            return false;
        }

        float before = cur;
        _target.SetScroll(cur + (diff * (1f - MathF.Exp(-P.GlideRate * dt)))); // 指数で目標へ寄せる
        if (_target.ScrollOffset == before) // 端でクランプ＝停止
        {
            ResetState();
            return false;
        }

        return true;
    }

    private float RubberBand(float beyond)
    {
        float sign = MathF.Sign(beyond);
        float x = MathF.Abs(beyond);
        return sign * P.MaxOverscroll * (1f - MathF.Exp(-x / P.MaxOverscroll)); // 漸近的に P.MaxOverscroll へ＝引くほど重い
    }

    private void StartMomentum()
    {
        _mode = Mode.Momentum;
        StartTicker();
    }

    private void StartTicker()
    {
        if (_ticking || Owner is null)
        {
            return;
        }

        _ticking = true;
        Owner.RegisterTicker(this);
    }

    // Tick 内からの停止：状態だけ落とし、登録解除は ticker ループの false 検知に任せる（多重 RemoveAt 回避）。
    private void ResetState()
    {
        _ticking = false;
        _mode = Mode.None;
        _velocity = 0f;
    }

    // 外部（ポインタ Down 等）からの停止：登録も解除する。
    private void Stop()
    {
        if (!_ticking)
        {
            return;
        }

        ResetState();
        Owner?.UnregisterTicker(this);
    }
}
