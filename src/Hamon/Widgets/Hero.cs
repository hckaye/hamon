using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>A shared element that can be the target of a flight (registered by tag and provides a drawing of the current rectangle and contents).</summary>
internal interface IHeroSource
{
    Rect Bounds { get; }

    void PaintFlight(in PaintContext context);
}

/// <summary>
/// <see cref="Hero"/>Registry by tag (<see cref="HamonRoot"/>has one).
/// In transition (<see cref="TransitionActive"/>), interpolate flight from "Hero of the lower route (starting point) → Hero of the upper route (end point)".
/// </summary>
internal sealed class HeroRegistry
{
    private readonly Dictionary<object, List<IHeroSource>> _byTag = new();

    /// <summary>Is it during the transition animation? (<see cref="Navigator"/>(set by push/pop progress status).</summary>
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

    /// <summary>Is this tag in flight (in transition and there are two or more of the same tags)? </summary>
    public bool IsFlying(object tag) => TransitionActive && _byTag.TryGetValue(tag, out List<IHeroSource>? list) && list.Count >= 2;
}

/// <summary>
/// Shared element transition (Flutter<c>Hero</c>equivalent minimum practical version). <see cref="Tag"/>If a Hero with is in the route before and after the transition,
/// The contents jump from the starting point rectangle to the ending rectangle according to the progress of push/pop (position and size are interpolated).
/// The drawing of the normal position is omitted, and only the foreground flight is visible.
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

/// <summary><see cref="Hero"/>holding entity. </summary>
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

    /// <summary>Proxy drawing from flight (contents only = Background is null).</summary>
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
/// <see cref="Navigator"/>The flight drawing layer that is stacked on top.
/// Convert and draw the contents of the end point Hero.
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
