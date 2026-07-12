using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Easing function (transfer progress of 0..1 to 0..1). <c>Curve</c>Quite a bit.</summary>
public delegate float Curve(float t);

/// <summary>Standard easing (static instance = no generation allocation). <c>Curves</c>Submit your name to.</summary>
public static class Curves
{
    public static readonly Curve Linear = static t => t;
    public static readonly Curve EaseIn = static t => t * t;
    public static readonly Curve EaseOut = static t => 1f - ((1f - t) * (1f - t));
    public static readonly Curve EaseInOut = static t => t < 0.5f ? 2f * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 2f) / 2f);

    /// <summary>Third-order acceleration (slow → sudden).</summary>
    public static readonly Curve EaseInCubic = static t => t * t * t;

    /// <summary>3rd order deceleration (sudden → slow; emphasis on stopping).</summary>
    public static readonly Curve EaseOutCubic = static t => 1f - MathF.Pow(1f - t, 3f);

    /// <summary>3rd order acceleration/deceleration (the most versatile smoothness).</summary>
    public static readonly Curve EaseInOutCubic = static t => t < 0.5f ? 4f * t * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 3f) / 2f);

    /// <summary>Material standard (fastOutSlowIn=cubic-bezier(0.4,0,0.2,1)). </summary>
    public static readonly Curve FastOutSlowIn = CubicBezier(0.4f, 0f, 0.2f, 1f);

    /// <summary>Deceleration (coming in and stopping = cubic-bezier(0,0,0.2,1)).</summary>
    public static readonly Curve Decelerate = CubicBezier(0f, 0f, 0.2f, 1f);

    /// <summary>Go a little too far at the end and go back (overshoot). </summary>
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

    /// <summary>Bounce (bounce to the ground and settle down).</summary>
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
    /// Generate CSS-like cubic-bezier easing (endpoints fixed at (0,0)/(1,1)). <b>Only once</b>generate and reuse
    /// (When generated every frame, closure allocation will occur).
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
/// Keyframe interpolation (Flutter<c>TweenSequence</c>A fairly lightweight version).
/// <see cref="Evaluate"/>0..1 Copy the progress to the value. <c>Curve</c>is that<b>Section in front</b>Applies to.
/// The array is allocated once in the constructor, and the evaluation is zero allocation (linear search of the interval).
/// </summary>
public sealed class TweenSequence
{
    private readonly (float Time, float Value, Curve Curve)[] _stops;

    /// <param name="stops">Ascending sequence of (time 0..1, value, Curve in that interval). </param>
    public TweenSequence(params (float Time, float Value, Curve Curve)[] stops)
    {
        if (stops is null || stops.Length == 0)
        {
            throw new ArgumentException("キーフレームが必要。", nameof(stops));
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

/// <summary>Interpolation of float intervals (Flutter<c>Tween</c>equivalent).<see cref="Lerp"/>0..1 Copy the progress to the value.</summary>
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

/// <summary>Interpolation of Color intervals.</summary>
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
/// Axis-parallel affine transformation (scaling + translation, no rotation). <see cref="PaintContext"/>) into a partial tree.
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

    /// <summary>A transformation that scales around pivot and then translate.</summary>
    public static Transform2D ScaleAbout(Vec2 scale, Vec2 pivot, Vec2 translate) =>
        new(scale, new Vec2(pivot.X * (1f - scale.X), pivot.Y * (1f - scale.Y)) + translate);

    /// <summary>outer transformation<paramref name="outer"/>inside after<paramref name="inner"/>Synthesis applying (<c>outer.Apply(inner.Apply(p))</c>）。</summary>
    public static Transform2D Compose(in Transform2D outer, in Transform2D inner) =>
        new(inner.Scale * outer.Scale, (inner.Translate * outer.Scale) + outer.Translate);

    /// <summary>Inverse transformation (<c>q = p·Scale + Translate</c>The opposite of =<c>p = (q − Translate)/Scale</c>). </summary>
    public Transform2D Inverse()
    {
        float ix = Scale.X == 0f ? 0f : 1f / Scale.X;
        float iy = Scale.Y == 0f ? 0f : 1f / Scale.Y;
        return new Transform2D(new Vec2(ix, iy), new Vec2(-Translate.X * ix, -Translate.Y * iy));
    }
}

/// <summary>
/// Animation driver that advances 0→1 in time (Flutter<c>AnimationController</c>equivalent).<see cref="ITicker"/>as
/// <see cref="HamonRoot.Update(Size, float)"/>Move forward with dt.<see cref="Value"/>is linear 0..1,<see cref="Curved"/>teeth
/// <see cref="Curve"/>After application.<see cref="Opacity"/>/<see cref="Transform"/>from<c>() =&gt; ctrl.Curved</c>If you read it in
/// <b>Animation when drawing without reconstruction</b>(Reflects the Draw of each frame). <see cref="OnChanged"/>to encourage rebuilding.
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

    /// <summary>Number of seconds (>0) to multiply from 0 to 1. </summary>
    public float Duration { get; set; }

    public Curve Curve { get; set; }

    /// <summary>Linear progress 0..1.</summary>
    public float Value => _value;

    /// <summary><see cref="Curve"/>Progress after application 0..1.</summary>
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
/// Make the value follow the target using spring physics (Flutter<c>SpringSimulation</c>A fairly simple version).<see cref="AnimationController"/>but
/// In contrast to advancing from 0 to 1 in a fixed time, this<b>Converges to target with spring + damping</b>(excessive = overshoot possible).
/// <see cref="Value"/>Animation without reconstruction if read with getter when drawing.<see cref="ITicker"/>as<see cref="HamonRoot.Update(Size, float)"/>Proceed with
/// Integration is performed in fixed substeps to ensure stability even when dt is large.
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

    /// <summary>The stiffness of the spring (the larger the spring, the faster it returns).</summary>
    public float Stiffness { get; set; }

    /// <summary>Attenuation (the larger the value, the faster the shaking will subside. Critical attenuation ≒ 2*sqrt(Stiffness)).</summary>
    public float Damping { get; set; }

    /// <summary>Current value.</summary>
    public float Value => _value;

    /// <summary>Current speed.</summary>
    public float Velocity => _velocity;

    /// <summary>Target value (when set, it will start moving towards that target).</summary>
    public float Target
    {
        get => _target;
        set => SetTarget(value);
    }

    public bool IsAnimating => _ticking;

    /// <summary>Called every time the value changes (prompts rebuilding in animations that affect the layout, etc. Not necessary if it is a getter when drawing).</summary>
    public Action? OnChanged { get; set; }

    /// <summary>Set goals and move towards them.</summary>
    public void SetTarget(float target)
    {
        _target = target;
        if (!_ticking)
        {
            _ticking = true;
            _owner.RegisterTicker(this);
        }
    }

    /// <summary>Jump to the value immediately (speed to 0).</summary>
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
