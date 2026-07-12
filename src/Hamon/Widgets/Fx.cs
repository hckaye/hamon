using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Management of lightweight FX (floating text = damage numbers/simple burst of particles).<b>outside the declaration tree</b>handle with
/// Ultra-high-frequency, short-lived effects are rotated in a fixed capacity pool (not allocated by spawn), advanced every frame, and erased at the end of their lifespan.
/// <see cref="ITicker"/>Register with the host as<see cref="FxLayer"/>Draw with it in the forefront. <see cref="FxLayer"/>call from.
/// </summary>
public sealed class FxController : ITicker
{
    private struct Floater
    {
        public Vec2 Pos;
        public string Text;
        public Color Color;
        public float Size;
        public float Age;
        public float Life;
        public float Rise;
        public bool Active;
    }

    private struct Particle
    {
        public Vec2 Pos;
        public Vec2 Vel;
        public Color Color;
        public float Size;
        public float Age;
        public float Life;
        public float Gravity;
        public bool Additive; // 加算合成（グロー）
        public bool Active;
    }

    private readonly Floater[] _floaters;
    private readonly Particle[] _particles;
    private int _floaterCursor;
    private int _particleCursor;
    private uint _seed = 2463534242u; // xorshift（ゼロアロケの疑似乱数）

    public FxController(int maxFloaters = 64, int maxParticles = 256)
    {
        _floaters = new Floater[maxFloaters];
        _particles = new Particle[maxParticles];
    }

    /// <summary>Number of valid floating texts (for inspection/testing).</summary>
    public int ActiveFloaterCount => CountActive(_floaters);

    /// <summary>Valid particle count (for inspection/testing).</summary>
    public int ActiveParticleCount => CountActiveP(_particles);

    /// <summary>Displays damage numbers, etc. as a rise + fade.<paramref name="pos"/>are screen coordinates (draw centered).</summary>
    public void SpawnText(Vec2 pos, string text, Color color, float size = 22f, float life = 0.9f, float rise = 60f)
    {
        int i = NextSlot(_floaters, ref _floaterCursor);
        _floaters[i] = new Floater { Pos = pos, Text = text, Color = color, Size = size, Age = 0f, Life = life, Rise = rise, Active = true };
    }

    /// <summary>particles radially<paramref name="count"/>Distribute (hit/acquisition effects, etc.).<paramref name="additive"/>Additive synthesis (glow).</summary>
    public void SpawnBurst(Vec2 pos, int count, Color color, float speed = 120f, float life = 0.6f, float size = 4f, float gravity = 220f, bool additive = false)
    {
        for (int k = 0; k < count; k++)
        {
            float ang = NextFloat() * MathF.PI * 2f;
            float spd = speed * (0.5f + (0.5f * NextFloat()));
            int i = NextSlot(_particles, ref _particleCursor);
            _particles[i] = new Particle
            {
                Pos = pos,
                Vel = new Vec2(MathF.Cos(ang) * spd, MathF.Sin(ang) * spd),
                Color = color,
                Size = size,
                Age = 0f,
                Life = life,
                Gravity = gravity,
                Additive = additive,
                Active = true,
            };
        }
    }

    public bool Tick(float dtSeconds)
    {
        for (int i = 0; i < _floaters.Length; i++)
        {
            if (!_floaters[i].Active)
            {
                continue;
            }

            _floaters[i].Age += dtSeconds;
            _floaters[i].Pos = new Vec2(_floaters[i].Pos.X, _floaters[i].Pos.Y - (_floaters[i].Rise * dtSeconds));
            if (_floaters[i].Age >= _floaters[i].Life)
            {
                _floaters[i].Active = false;
            }
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            if (!_particles[i].Active)
            {
                continue;
            }

            _particles[i].Age += dtSeconds;
            _particles[i].Vel = new Vec2(_particles[i].Vel.X, _particles[i].Vel.Y + (_particles[i].Gravity * dtSeconds));
            _particles[i].Pos += _particles[i].Vel * dtSeconds;
            if (_particles[i].Age >= _particles[i].Life)
            {
                _particles[i].Active = false;
            }
        }

        return true;
    }

    internal void Paint(in PaintContext context)
    {
        // 通常合成のパーティクル。
        for (int i = 0; i < _particles.Length; i++)
        {
            ref readonly Particle p = ref _particles[i];
            if (p.Active && !p.Additive)
            {
                context.FillCircle(p.Pos, p.Size, Fade(p.Color, 1f - (p.Age / p.Life)));
            }
        }

        // 加算合成のパーティクル（グロー）。最初の1個で合成モードを積む（無ければ flush しない）。
        object? blend = null;
        for (int i = 0; i < _particles.Length; i++)
        {
            ref readonly Particle p = ref _particles[i];
            if (p.Active && p.Additive)
            {
                blend ??= context.PushBlend(BlendMode.Additive);
                context.FillCircle(p.Pos, p.Size, Fade(p.Color, 1f - (p.Age / p.Life)));
            }
        }

        if (blend is not null)
        {
            context.PopBlend(blend);
        }

        if (context.Text is ITextRenderer text)
        {
            for (int i = 0; i < _floaters.Length; i++)
            {
                ref readonly Floater f = ref _floaters[i];
                if (!f.Active)
                {
                    continue;
                }

                float a = 1f - (f.Age / f.Life);
                Vec2 size = text.Measure(f.Text, f.Size);
                text.Draw(f.Text, new Vec2(f.Pos.X - (size.X / 2f), f.Pos.Y), f.Size, Fade(f.Color, a));
            }
        }
    }

    private static Color Fade(Color c, float a) => new(c.R, c.G, c.B, (int)Math.Clamp(c.A * Math.Clamp(a, 0f, 1f), 0f, 255f));

    private float NextFloat()
    {
        _seed ^= _seed << 13;
        _seed ^= _seed >> 17;
        _seed ^= _seed << 5;
        return (_seed & 0xFFFFFFu) / (float)0x1000000;
    }

    private static int NextSlot(Floater[] pool, ref int cursor)
    {
        for (int n = 0; n < pool.Length; n++)
        {
            int i = (cursor + n) % pool.Length;
            if (!pool[i].Active)
            {
                cursor = (i + 1) % pool.Length;
                return i;
            }
        }

        int over = cursor; // 満杯なら最古を上書き
        cursor = (cursor + 1) % pool.Length;
        return over;
    }

    private static int NextSlot(Particle[] pool, ref int cursor)
    {
        for (int n = 0; n < pool.Length; n++)
        {
            int i = (cursor + n) % pool.Length;
            if (!pool[i].Active)
            {
                cursor = (i + 1) % pool.Length;
                return i;
            }
        }

        int over = cursor;
        cursor = (cursor + 1) % pool.Length;
        return over;
    }

    private static int CountActive(Floater[] pool)
    {
        int n = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i].Active)
            {
                n++;
            }
        }

        return n;
    }

    private static int CountActiveP(Particle[] pool)
    {
        int n = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i].Active)
            {
                n++;
            }
        }

        return n;
    }
}

/// <summary>
/// A layer that draws FX on top (<see cref="FxController"/>Read the contents when drawing = no reconstruction/pointer transparency).
/// (such as the end of a Stack). <see cref="FxController.SpawnText"/>/<see cref="FxController.SpawnBurst"/>Load from
/// </summary>
public sealed class FxLayer : Widget
{
    public required FxController Controller { get; init; }

    public override Element CreateElement() => new FxLayerElement(this);
}

internal sealed class FxLayerElement : Element
{
    private readonly LayoutNode _node;

    public FxLayerElement(FxLayer widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: static _ => Size.Zero);
    }

    public override LayoutNode LayoutNode => _node;

    public override void Paint(in PaintContext context) => ((FxLayer)Widget).Controller.Paint(context);
}
