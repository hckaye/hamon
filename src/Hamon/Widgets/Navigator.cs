using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A transition builder that wraps a route's entry and exit animation with a
/// progress value in the range 0..1 (1 = fully visible). Implementations typically
/// drive an <see cref="Opacity"/> or <see cref="Transform"/> using a getter that reads
/// <paramref name="progress"/> on every paint, so the animation runs without
/// rebuilding the widget tree.
/// </summary>
public delegate Widget RouteTransition(Widget child, Func<float> progress);

/// <summary>Standard route transitions.</summary>
public static class RouteTransitions
{
    /// <summary>No transition: returns the content unchanged and ignores the progress value.</summary>
    public static readonly RouteTransition None = static (child, _) => child;

    /// <summary>Fade only, without scaling. Useful for full-screen scrims and similar cases where scaling would reveal gaps at the edges.</summary>
    public static readonly RouteTransition Fade = static (child, progress) => new Opacity { ValueGetter = progress, Child = child };

    /// <summary>Fade + slight expansion (modal style).</summary>
    public static readonly RouteTransition FadeScale = static (child, progress) => new Opacity
    {
        ValueGetter = progress,
        Child = new Transform
        {
            ScaleGetter = () => 0.96f + (0.04f * progress()),
            Origin = Alignment.Center,
            Child = child,
        },
    };
}

/// <summary>
/// A single route (screen) in the navigation stack. Returned by
/// <see cref="NavigatorController.Push"/>, and also used as the stable identity (Key)
/// for reconciliation. The result passed to <see cref="NavigatorController.Pop"/> is
/// delivered back through <see cref="OnResult"/>.
/// </summary>
public sealed class NavRoute
{
    private readonly Func<Widget> _build;

    internal NavRoute(Func<Widget> build, Action<object?>? onResult)
    {
        _build = build;
        OnResult = onResult;
    }

    internal Action<object?>? OnResult { get; }

    internal Widget Build() => _build();
}

/// <summary>A route currently being rendered. Holds the state for its transition animation and keeps drawing until the exit animation completes.</summary>
internal sealed class RouteEntry
{
    public RouteEntry(NavRoute route, AnimationController? anim)
    {
        Route = route;
        Anim = anim;
    }

    public NavRoute Route { get; }

    public AnimationController? Anim { get; }

    public bool Exiting { get; set; }
}

/// <summary>
/// Imperative navigation controller that maintains a route stack and handles
/// push/pop (a thin version of Flutter's <c>Navigator</c>). The app creates one and
/// passes it to a <see cref="Navigator"/> overlay. Each route is wrapped in a
/// <see cref="FocusScope"/>, so focus can only land on the frontmost route (popping
/// returns focus to the previous route). When <see cref="TransitionDuration"/> is
/// greater than 0, push/pop are animated using <see cref="Transition"/>.
/// <b>Even after a route is popped, its subtree is kept alive until the exit
/// animation finishes.</b> (Default is 0, meaning immediate with no animation.)
/// </summary>
public sealed class NavigatorController
{
    private readonly HamonRoot _host;
    private readonly List<NavRoute> _routes = new();
    private readonly List<RouteEntry> _entries = new();

    /// <param name="host">The host to notify of changes (marked dirty to trigger a rebuild).</param>
    /// <param name="home">The initial route (the base of the stack, which cannot be popped).</param>
    public NavigatorController(HamonRoot host, Func<Widget> home)
    {
        _host = host;
        var route = new NavRoute(home, null);
        _routes.Add(route);
        _entries.Add(new RouteEntry(route, null)); // home は入場アニメなし
    }

    /// <summary>The current route stack, ordered bottom to top.</summary>
    public IReadOnlyList<NavRoute> Routes => _routes;

    /// <summary>Number of routes on the stack (including home, excluding routes that are currently exiting).</summary>
    public int Count => _routes.Count;

    /// <summary>Whether there is anything above home on the stack (i.e., whether <see cref="Pop"/> is possible).</summary>
    public bool CanPop => _routes.Count > 1;

    /// <summary>Push/pop transition animation seconds (default 0 = immediate, no animation).</summary>
    public float TransitionDuration { get; set; }

    /// <summary>The route transition to use (default <see cref="RouteTransitions.FadeScale"/>).</summary>
    public RouteTransition Transition { get; set; } = RouteTransitions.FadeScale;

    public Curve Curve { get; set; } = Curves.EaseOut;

    /// <summary>The routes currently being drawn (including ones that are exiting), read by <see cref="Navigator"/>.</summary>
    internal IReadOnlyList<RouteEntry> Entries => _entries;

    /// <summary>Progress of the current transition animation (1 = the upper route is fully visible), read by <see cref="HeroLayer"/> for flight interpolation.</summary>
    internal Func<float>? ActiveTransition { get; private set; }

    /// <summary>Number of routes being drawn (including during exit animation, for inspection/testing purposes).</summary>
    internal int RenderedCount => _entries.Count;

    /// <summary>Pushes a new route onto the stack.</summary>
    public NavRoute Push(Func<Widget> build, Action<object?>? onResult = null)
    {
        var route = new NavRoute(build, onResult);
        _routes.Add(route);

        AnimationController? anim = null;
        if (TransitionDuration > 0f)
        {
            AnimationController controller = _host.CreateAnimation(TransitionDuration, Curve);
            controller.Forward(); // 0→1 で入場
            ActiveTransition = () => controller.Curved; // Hero flight：始点→終点（0→1）
            controller.OnCompleted = () =>
            {
                ActiveTransition = null;
                _host.MarkDirty();
            };
            anim = controller;
        }

        _entries.Add(new RouteEntry(route, anim));
        _host.MarkDirty();
        return route;
    }

    /// <summary>Pops the frontmost route and delivers <paramref name="result"/> to its <see cref="NavRoute.OnResult"/> callback. Returns <see langword="false"/> if only the home route remains.</summary>
    public bool Pop(object? result = null)
    {
        if (_routes.Count <= 1)
        {
            return false;
        }

        NavRoute top = _routes[_routes.Count - 1];
        _routes.RemoveAt(_routes.Count - 1); // 論理スタックからは即時に外す
        top.OnResult?.Invoke(result);

        RouteEntry entry = FindEntry(top);
        if (entry.Anim is not null)
        {
            // 退場アニメ：完了まで描画を続け、完了時に実体を外す。
            entry.Exiting = true;
            ActiveTransition = () => entry.Anim!.Curved; // Hero flight：終点→始点（1→0）
            entry.Anim.OnCompleted = () =>
            {
                ActiveTransition = null;
                _entries.Remove(entry);
                _host.MarkDirty();
            };
            entry.Anim.Reverse(); // 1→0 で退場
        }
        else
        {
            _entries.Remove(entry);
        }

        _host.MarkDirty();
        return true;
    }

    private RouteEntry FindEntry(NavRoute route)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_entries[i].Route, route))
            {
                return _entries[i];
            }
        }

        throw new InvalidOperationException("No render entry found for the route being popped.");
    }
}

/// <summary>
/// Draws the route stack of a <see cref="NavigatorController"/> (the equivalent of
/// Flutter's <c>Navigator</c>). Each route is wrapped full-screen in a
/// <see cref="FocusScope"/> and stacked using <see cref="Stack"/> (later entries are
/// on top in z-order), so focus is automatically locked to the frontmost route.
/// Wiring up <see cref="NavigatorController.Pop"/> is a one-liner on the app side
/// (e.g. <c>if (b == Dismiss &amp;&amp; nav.Pop()) return;</c>).
/// </summary>
public sealed class Navigator : StatelessWidget
{
    public NavigatorController? Controller { get; init; }

    public override Widget Build(BuildContext context)
    {
        NavigatorController controller = Controller
            ?? throw new InvalidOperationException("Navigator requires a Controller.");

        // Hero の本体描画を flight 中に省くためのフラグ（push/pop の進捗有無で決まる＝再構築は push/pop/完了時のみ）。
        if (context.Owner is HamonRoot host)
        {
            host.Heroes.TransitionActive = controller.ActiveTransition is not null;
        }

        IReadOnlyList<RouteEntry> entries = controller.Entries;
        var children = new Widget[entries.Count + 1];
        for (int i = 0; i < entries.Count; i++)
        {
            RouteEntry entry = entries[i];
            Widget content = entry.Route.Build();
            if (entry.Anim is AnimationController anim)
            {
                content = controller.Transition(content, () => anim.Curved); // 入場/退場とも進捗を描画時に読む
            }

            // ルートは不透明（Flutter の opaque route）：全画面の GestureDetector で素抜けを吸収し、背面ルートへ
            // ポインタを貫通させない（中の Button 等は深さ優先で先にヒットするので操作可能）。
            children[i] = new FocusScope { Key = entry.Route, Child = new GestureDetector { Child = content } };
        }

        // 最前面に flight 層（遷移中だけ Hero を飛ばして描く。レイアウト 0 サイズ）。
        children[entries.Count] = new HeroLayer { Controller = controller };

        return new Stack { Fit = StackFit.Expand, Children = children };
    }
}
