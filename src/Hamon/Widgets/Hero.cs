using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>A shared element that can be the target of a flight. Registered by tag, and provides a drawing of its current rectangle and contents.</summary>
internal interface IHeroSource
{
    Rect Bounds { get; }

    void PaintFlight(in PaintContext context);
}

/// <summary>
/// A registry of <see cref="Hero"/> instances by tag (one is held by <see cref="HamonRoot"/>).
/// During a transition (<see cref="TransitionActive"/>), interpolates the flight from the Hero on the lower route
/// (starting point) to the Hero on the upper route (end point).
/// </summary>
internal sealed class HeroRegistry
{
    private readonly Dictionary<object, List<IHeroSource>> _byTag = new();

    /// <summary>Is a transition animation in progress? Set according to <see cref="Navigator"/> push/pop progress.</summary>
    public bool TransitionActive { get; set; }

    public IReadOnlyDictionary<object, List<IHeroSource>> ByTag => _byTag;

    public void Register(object tag, IHeroSource source)
    {
        if (!_byTag.TryGetValue(tag, out List<IHeroSource>? list))
        {
            list = new List<IHeroSource>(2);
            _byTag[tag] = list;
        }

        list.Add(source);
    }

    public void Unregister(object tag, IHeroSource source)
    {
        if (_byTag.TryGetValue(tag, out List<IHeroSource>? list))
        {
            list.Remove(source);
            if (list.Count == 0)
            {
                _byTag.Remove(tag);
            }
        }
    }

    /// <summary>Is this tag currently in flight (a transition is active and there are two or more Heroes with the same tag)?</summary>
    public bool IsFlying(object tag) => TransitionActive && _byTag.TryGetValue(tag, out List<IHeroSource>? list) && list.Count >= 2;
}

/// <summary>
/// A shared-element transition (a minimal, practical equivalent of Flutter's <c>Hero</c>). If a Hero with a matching
/// <see cref="Tag"/> exists in both the route before and after the transition, its contents fly from the starting
/// rectangle to the ending rectangle as the push/pop progresses (position and size are interpolated).
/// Normal-position drawing is suppressed, and only the flying foreground copy is visible.
/// </summary>
public sealed class Hero : Widget, IRenderConfig
{
    public required object Tag { get; init; }

    public Widget? Child { get; init; }

    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };

    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };

    Color? IRenderConfig.Background => null;

    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new HeroElement(this);
}

/// <summary>The holding entity for <see cref="Hero"/>.</summary>
internal sealed class HeroElement : RenderElement, IHeroSource
{
    private object? _registeredTag;

    public HeroElement(Hero widget)
        : base(widget)
    {
    }

    public Rect Bounds => LayoutNode.Bounds;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        Register();
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        if (!Equals(_registeredTag, ((Hero)Widget).Tag))
        {
            Unregister();
            Register();
        }
    }

    public override void Unmount()
    {
        Unregister();
        base.Unmount();
    }

    public override void Paint(in PaintContext context)
    {
        if (_registeredTag is object tag && (Context.Owner as HamonRoot)?.Heroes.IsFlying(tag) == true)
        {
            return; // flight 中は HeroLayer が代わりに飛ばして描く
        }

        base.Paint(context);
    }

    /// <summary>Proxy drawing invoked by the flight (contents only, since Background is null).</summary>
    public void PaintFlight(in PaintContext context) => base.Paint(context);

    private void Register()
    {
        if (Context.Owner is HamonRoot host)
        {
            _registeredTag = ((Hero)Widget).Tag;
            host.Heroes.Register(_registeredTag, this);
        }
    }

    private void Unregister()
    {
        if (_registeredTag is object tag && Context.Owner is HamonRoot host)
        {
            host.Heroes.Unregister(tag, this);
        }

        _registeredTag = null;
    }
}

/// <summary>
/// The flight-drawing layer stacked on top of the <see cref="Navigator"/>.
/// Transforms and draws the contents of the end-point Hero.
/// </summary>
internal sealed class HeroLayer : Widget
{
    public required NavigatorController Controller { get; init; }

    public override Element CreateElement() => new HeroLayerElement(this);
}

internal sealed class HeroLayerElement : Element
{
    private readonly LayoutNode _node;

    public HeroLayerElement(HeroLayer widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: static _ => Size.Zero);
    }

    public override LayoutNode LayoutNode => _node;

    public override void Paint(in PaintContext context)
    {
        if (Context.Owner is not HamonRoot host)
        {
            return;
        }

        Func<float>? transition = ((HeroLayer)Widget).Controller.ActiveTransition;
        if (transition is null)
        {
            return;
        }

        float p = Math.Clamp(transition(), 0f, 1f);
        foreach (KeyValuePair<object, List<IHeroSource>> pair in host.Heroes.ByTag)
        {
            List<IHeroSource> list = pair.Value;
            if (list.Count < 2)
            {
                continue;
            }

            IHeroSource source = list[0];
            IHeroSource dest = list[list.Count - 1];
            Rect from = source.Bounds;
            Rect to = dest.Bounds;
            Rect current = LerpRect(from, to, p);

            // 終点 Hero の中身を current へ写す変換（topleft 合わせ＋サイズ比）。
            float sx = to.Width <= 0f ? 1f : current.Width / to.Width;
            float sy = to.Height <= 0f ? 1f : current.Height / to.Height;
            var map = new Transform2D(new Vec2(sx, sy), new Vec2(current.X - (to.X * sx), current.Y - (to.Y * sy)));
            dest.PaintFlight(context.WithTransform(map));
        }
    }

    private static Rect LerpRect(Rect a, Rect b, float t) => new(
        a.X + ((b.X - a.X) * t),
        a.Y + ((b.Y - a.Y) * t),
        a.Width + ((b.Width - a.Width) * t),
        a.Height + ((b.Height - a.Height) * t));
}
