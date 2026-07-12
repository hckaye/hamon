using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>An easing function that maps a progress value in the range 0..1 to a transformed value in 0..1.</summary>
public delegate float Curve(float t);

/// <summary>Standard easing functions (static instances, so no allocation per use). Named after Flutter's <c>Curves</c> class.</summary>
public static class Curves
{
    public static readonly Curve Linear = static t => t;
    public static readonly Curve EaseIn = static t => t * t;
    public static readonly Curve EaseOut = static t => 1f - ((1f - t) * (1f - t));
    public static readonly Curve EaseInOut = static t => t < 0.5f ? 2f * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 2f) / 2f);

    /// <summary>Cubic ease-in (starts slow, then accelerates rapidly).</summary>
    public static readonly Curve EaseInCubic = static t => t * t * t;

    /// <summary>Cubic ease-out (starts fast, then decelerates smoothly to a stop).</summary>
    public static readonly Curve EaseOutCubic = static t => 1f - MathF.Pow(1f - t, 3f);

    /// <summary>Cubic ease-in-out (the most versatile, general-purpose smoothing).</summary>
    public static readonly Curve EaseInOutCubic = static t => t < 0.5f ? 4f * t * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 3f) / 2f);

    /// <summary>The Material Design standard curve (fastOutSlowIn = cubic-bezier(0.4, 0, 0.2, 1)).</summary>
    public static readonly Curve FastOutSlowIn = CubicBezier(0.4f, 0f, 0.2f, 1f);

    /// <summary>Deceleration curve (starts fast and eases to a stop = cubic-bezier(0, 0, 0.2, 1)).</summary>
    public static readonly Curve Decelerate = CubicBezier(0f, 0f, 0.2f, 1f);

    /// <summary>Overshoots slightly past the end before settling back (overshoot easing).</summary>
    public static readonly Curve EaseOutBack = static t =>
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + (c3 * u * u * u) + (c1 * u * u);
    };

    /// <summary>Elastic deceleration.</summary>
    public static readonly Curve ElasticOut = static t =>
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        const float p = 0.3f;
        return (MathF.Pow(2f, -10f * t) * MathF.Sin((t - (p / 4f)) * (2f * MathF.PI) / p)) + 1f;
    };

    /// <summary>Bounce easing (bounces like hitting the ground before settling).</summary>
    public static readonly Curve BounceOut = static t =>
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1)
        {
            return n1 * t * t;
        }

        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return (n1 * t * t) + 0.75f;
        }

        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return (n1 * t * t) + 0.9375f;
        }

        t -= 2.625f / d1;
        return (n1 * t * t) + 0.984375f;
    };

    /// <summary>
    /// Generates a CSS-like cubic-bezier easing curve (endpoints fixed at (0,0) and (1,1)). <b>Generate it once and
    /// reuse it</b> — generating a new one every frame causes closure allocations.
    /// </summary>
    public static Curve CubicBezier(float x1, float y1, float x2, float y2) => t =>
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        float u = t;
        for (int i = 0; i < 6; i++)
        {
            float x = BezierAxis(u, x1, x2) - t;
            float dx = BezierSlope(u, x1, x2);
            if (MathF.Abs(dx) < 1e-6f)
            {
                break;
            }

            u = Math.Clamp(u - (x / dx), 0f, 1f);
        }

        return BezierAxis(u, y1, y2);
    };

    private static float BezierAxis(float u, float a, float b)
    {
        float v = 1f - u;
        return (3f * v * v * u * a) + (3f * v * u * u * b) + (u * u * u);
    }

    private static float BezierSlope(float u, float a, float b)
    {
        float v = 1f - u;
        return (3f * v * v * a) + (6f * v * u * (b - a)) + (3f * u * u * (1f - b));
    }
}

/// <summary>
/// Keyframe interpolation (a fairly lightweight version of Flutter's <c>TweenSequence</c>).
/// <see cref="Evaluate"/> maps a progress value in 0..1 to the interpolated value. Each stop's <c>Curve</c>
/// applies to the segment leading up to it. The stops array is allocated once in the constructor, and evaluation
/// is zero-allocation (a linear search over the intervals).
/// </summary>
public sealed class TweenSequence
{
    private readonly (float Time, float Value, Curve Curve)[] _stops;

    /// <param name="stops">An ascending sequence of (time in 0..1, value, curve for that interval) stops.</param>
    public TweenSequence(params (float Time, float Value, Curve Curve)[] stops)
    {
        if (stops is null || stops.Length == 0)
        {
            throw new ArgumentException("At least one keyframe is required.", nameof(stops));
        }

        _stops = stops;
    }

    public float Evaluate(float t)
    {
        if (t <= _stops[0].Time)
        {
            return _stops[0].Value;
        }

        (float Time, float Value, Curve Curve) last = _stops[_stops.Length - 1];
        if (t >= last.Time)
        {
            return last.Value;
        }

        for (int i = 1; i < _stops.Length; i++)
        {
            (float Time, float Value, Curve Curve) b = _stops[i];
            if (t <= b.Time)
            {
                (float Time, float Value, Curve Curve) a = _stops[i - 1];
                float span = b.Time - a.Time;
                float local = span <= 0f ? 1f : (t - a.Time) / span;
                float curved = (b.Curve ?? Curves.Linear)(local);
                return a.Value + ((b.Value - a.Value) * curved);
            }
        }

        return last.Value;
    }
}

/// <summary>Interpolation over a float range (equivalent to Flutter's <c>Tween</c>). <see cref="Lerp"/> maps a progress value in 0..1 to the interpolated value.</summary>
public readonly struct Tween
{
    public Tween(float begin, float end)
    {
        Begin = begin;
        End = end;
    }

    public float Begin { get; }

    public float End { get; }

    public float Lerp(float t) => Begin + ((End - Begin) * t);
}

/// <summary>Interpolation over a Color range.</summary>
public readonly struct ColorTween
{
    public ColorTween(Color begin, Color end)
    {
        Begin = begin;
        End = end;
    }

    public Color Begin { get; }

    public Color End { get; }

    public Color Lerp(float t) => Color.Lerp(Begin, End, t);
}

/// <summary>
/// An axis-aligned affine transformation (scaling + translation, no rotation), applied to a subtree during
/// painting via <see cref="PaintContext"/>.
/// <c>map(p) = p * Scale + Translate</c>.
/// </summary>
internal readonly struct Transform2D
{
    public static readonly Transform2D Identity = new(Vec2.One, Vec2.Zero);

    public Transform2D(Vec2 scale, Vec2 translate)
    {
        Scale = scale;
        Translate = translate;
    }

    public Vec2 Scale { get; }

    public Vec2 Translate { get; }

    public Vec2 Apply(Vec2 p) => new((p.X * Scale.X) + Translate.X, (p.Y * Scale.Y) + Translate.Y);

    public Rect Apply(Rect r)
    {
        Vec2 a = Apply(new Vec2(r.X, r.Y));
        Vec2 b = Apply(new Vec2(r.X + r.Width, r.Y + r.Height));
        return new Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
    }

    /// <summary>Translation-only transformation.</summary>
    public static Transform2D Translation(Vec2 offset) => new(Vec2.One, offset);

    /// <summary>A transformation that scales around a pivot point and then translates.</summary>
    public static Transform2D ScaleAbout(Vec2 scale, Vec2 pivot, Vec2 translate) =>
        new(scale, new Vec2(pivot.X * (1f - scale.X), pivot.Y * (1f - scale.Y)) + translate);

    /// <summary>Composes an <paramref name="outer"/> transformation with an <paramref name="inner"/> one, applying the inner transformation first and then the outer (equivalent to <c>outer.Apply(inner.Apply(p))</c>).</summary>
    public static Transform2D Compose(in Transform2D outer, in Transform2D inner) =>
        new(inner.Scale * outer.Scale, (inner.Translate * outer.Scale) + outer.Translate);

    /// <summary>The inverse transformation: given <c>q = p·Scale + Translate</c>, returns the transform such that <c>p = (q − Translate) / Scale</c>.</summary>
    public Transform2D Inverse()
    {
        float ix = Scale.X == 0f ? 0f : 1f / Scale.X;
        float iy = Scale.Y == 0f ? 0f : 1f / Scale.Y;
        return new Transform2D(new Vec2(ix, iy), new Vec2(-Translate.X * ix, -Translate.Y * iy));
    }
}

/// <summary>
/// An animation driver that advances a value from 0 to 1 over time (equivalent to Flutter's <c>AnimationController</c>).
/// As an <see cref="ITicker"/>, it is advanced by dt in <see cref="HamonRoot.Update(Size, float)"/>. <see cref="Value"/>
/// is the linear progress in 0..1, while <see cref="Curved"/> is that value after applying <see cref="Curve"/>.
/// Widgets such as <see cref="Opacity"/>/<see cref="Transform"/> can read it via a getter like <c>() =&gt; ctrl.Curved</c>
/// to get <b>animation at draw time without rebuilding</b> (reflected on every frame). Use <see cref="OnChanged"/>
/// to trigger a rebuild instead.
/// </summary>
public sealed class AnimationController : ITicker
{
    private readonly IHamonHost _owner;
    private float _value;
    private int _direction; // 0=停止, +1=前進, -1=後退

    internal AnimationController(IHamonHost owner, float durationSeconds, Curve? curve = null)
    {
        _owner = owner;
        Duration = durationSeconds;
        Curve = curve ?? Curves.Linear;
    }

    /// <summary>Number of seconds (>0) to go from 0 to 1.</summary>
    public float Duration { get; set; }

    public Curve Curve { get; set; }

    /// <summary>Linear progress 0..1.</summary>
    public float Value => _value;

    /// <summary>Progress in 0..1 after applying <see cref="Curve"/>.</summary>
    public float Curved => Curve(_value);

    public bool IsAnimating => _direction != 0;

    /// <summary>Called every time a value changes (for example, to prompt a rebuild in an animation that affects the layout). </summary>
    public Action? OnChanged { get; set; }

    /// <summary>Call once when reaching the end (1 for forward/0 for backward).</summary>
    public Action? OnCompleted { get; set; }

    public void Forward() => Drive(1);

    public void Reverse() => Drive(-1);

    public void Stop()
    {
        _direction = 0;
        _owner.UnregisterTicker(this);
    }

    /// <summary>Set the value immediately (stop the animation).</summary>
    public void JumpTo(float value)
    {
        Stop();
        float clamped = Math.Clamp(value, 0f, 1f);
        if (clamped != _value)
        {
            _value = clamped;
            OnChanged?.Invoke();
        }
    }

    public bool Tick(float dt)
    {
        if (_direction == 0 || dt <= 0f)
        {
            return _direction != 0;
        }

        float step = (Duration <= 0f ? 1f : dt / Duration) * _direction;
        _value = Math.Clamp(_value + step, 0f, 1f);
        OnChanged?.Invoke();

        if ((_direction > 0 && _value >= 1f) || (_direction < 0 && _value <= 0f))
        {
            _direction = 0;
            OnCompleted?.Invoke(); // ここで Forward()/Reverse() を呼ぶと _direction が再設定される（ping-pong＝点滅）
            // OnCompleted が再駆動したら登録解除しない（しないと往復アニメが1半周期で固まる）。
            return _direction != 0;
        }

        return true;
    }

    private void Drive(int direction)
    {
        if (Duration <= 0f)
        {
            JumpTo(direction > 0 ? 1f : 0f);
            OnCompleted?.Invoke();
            return;
        }

        _direction = direction;
        _owner.RegisterTicker(this);
    }
}

/// <summary>
/// Makes a value follow a target using spring physics (a fairly simple version of Flutter's <c>SpringSimulation</c>).
/// Unlike <see cref="AnimationController"/>, which advances from 0 to 1 over a fixed duration, this
/// <b>converges toward the target via spring + damping</b> (it may overshoot if underdamped). If <see cref="Value"/>
/// is read via a getter at draw time, the animation updates without needing a rebuild. As an <see cref="ITicker"/>,
/// it is advanced in <see cref="HamonRoot.Update(Size, float)"/>. Integration is performed in fixed substeps to
/// keep it stable even when dt is large.
/// </summary>
public sealed class SpringController : ITicker
{
    private const float SubStep = 1f / 120f;

    private readonly IHamonHost _owner;
    private float _value;
    private float _velocity;
    private float _target;
    private bool _ticking;

    public SpringController(IHamonHost owner, float stiffness = 200f, float damping = 20f, float initial = 0f)
    {
        _owner = owner;
        Stiffness = stiffness;
        Damping = damping;
        _value = initial;
        _target = initial;
    }

    /// <summary>The stiffness of the spring (the larger the value, the faster it returns to the target).</summary>
    public float Stiffness { get; set; }

    /// <summary>Damping (the larger the value, the faster the oscillation subsides; critical damping ≈ 2*sqrt(Stiffness)).</summary>
    public float Damping { get; set; }

    /// <summary>Current value.</summary>
    public float Value => _value;

    /// <summary>Current velocity.</summary>
    public float Velocity => _velocity;

    /// <summary>Target value (when set, it will start moving towards that target).</summary>
    public float Target
    {
        get => _target;
        set => SetTarget(value);
    }

    public bool IsAnimating => _ticking;

    /// <summary>Called every time the value changes (use it to prompt a rebuild for animations that affect layout; unnecessary if you read the value via a getter at draw time).</summary>
    public Action? OnChanged { get; set; }

    /// <summary>Sets the target and starts moving towards it.</summary>
    public void SetTarget(float target)
    {
        _target = target;
        if (!_ticking)
        {
            _ticking = true;
            _owner.RegisterTicker(this);
        }
    }

    /// <summary>Jumps to the value immediately (resets velocity to 0).</summary>
    public void JumpTo(float value)
    {
        _value = value;
        _velocity = 0f;
        OnChanged?.Invoke();
    }

    public void Stop()
    {
        _ticking = false;
        _velocity = 0f;
        _owner.UnregisterTicker(this);
    }

    public bool Tick(float dtSeconds)
    {
        if (dtSeconds <= 0f)
        {
            return _ticking;
        }

        float remaining = dtSeconds;
        while (remaining > 0f)
        {
            float step = MathF.Min(remaining, SubStep);
            float accel = (-Stiffness * (_value - _target)) - (Damping * _velocity);
            _velocity += accel * step;
            _value += _velocity * step;
            remaining -= step;
        }

        OnChanged?.Invoke();

        if (MathF.Abs(_value - _target) < 0.001f && MathF.Abs(_velocity) < 0.01f)
        {
            _value = _target;
            _velocity = 0f;
            _ticking = false;
            return false; // 収束＝ティッカー解除
        }

        return true;
    }
}
