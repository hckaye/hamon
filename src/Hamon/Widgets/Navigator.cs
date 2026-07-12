using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A transition builder that wraps route entry and exit with progress 0..1 (1 = fully visible).<see cref="Opacity"/>/<see cref="Transform"/>of
/// <paramref name="progress"/>If you run it with (a getter that is read every time it is drawn), it will be an animation without reconstruction.
/// </summary>
public delegate Widget RouteTransition(Widget child, Func<float> progress);

/// <summary>Standard route transition.</summary>
public static class RouteTransitions
{
    /// <summary>No transition (only the substance is used = the builder side uses progress to create an appearance).</summary>
    public static readonly RouteTransition None = static (child, _) => child;

    /// <summary>Fade only (no enlargement = when you want to avoid missing edges in full-screen scrims, etc.).</summary>
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
/// 1 root (screen) of the navigation stack.<see cref="NavigatorController.Push"/>With the return value of
/// Also serves as the stable identity (Key) of reconcile.<see cref="NavigatorController.Pop"/>The result is
/// <see cref="OnResult"/>Return to
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

/// <summary>Route being rendered (has state for transition animation. Continues drawing until completion even while exiting).</summary>
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
/// Imperative navigation that maintains root stack and pushes/pops (Flutter<c>Navigator</c>thin version).
/// The app generates<see cref="Navigator"/>give it to
/// overlay/<see cref="FocusScope"/>(each root is<see cref="FocusScope"/>in
/// Wrapped, focus can only be placed on the frontmost route (Pop returns to previous route).
/// <see cref="TransitionDuration"/>When >0, push/pop is<see cref="Transition"/>Animated with
/// <b>Even if you pop, keep the subtree alive until the exit animation is completed.</b>(Default 0 = Immediate).
/// </summary>
public sealed class NavigatorController
{
    private readonly HamonRoot _host;
    private readonly List<NavRoute> _routes = new();
    private readonly List<RouteEntry> _entries = new();

    /// <param name="host">Change notification destination (make dirty for rebuild).</param>
    /// <param name="home">The first root (the foundation that does not disappear with pop).</param>
    public NavigatorController(HamonRoot host, Func<Widget> home)
    {
        _host = host;
        var route = new NavRoute(home, null);
        _routes.Add(route);
        _entries.Add(new RouteEntry(route, null)); // home は入場アニメなし
    }

    /// <summary>Current root stack (bottom → top). </summary>
    public IReadOnlyList<NavRoute> Routes => _routes;

    /// <summary>Number of routes stacked (including home, excluding routes while leaving).</summary>
    public int Count => _routes.Count;

    /// <summary>Is there anything higher than home (can you go back)?</summary>
    public bool CanPop => _routes.Count > 1;

    /// <summary>Push/pop transition animation seconds (default 0 = immediate, no animation).</summary>
    public float TransitionDuration { get; set; }

    /// <summary>Route transition appearance (default<see cref="RouteTransitions.FadeScale"/>）。</summary>
    public RouteTransition Transition { get; set; } = RouteTransitions.FadeScale;

    public Curve Curve { get; set; } = Curves.EaseOut;

    /// <summary>The route being drawn (including while leaving).<see cref="Navigator"/>read).</summary>
    internal IReadOnlyList<RouteEntry> Entries => _entries;

    /// <summary>Progress during transition animation (1=upper route fully visible). <see cref="HeroLayer"/>read into flight interpolation.</summary>
    internal Func<float>? ActiveTransition { get; private set; }

    /// <summary>Number of routes being drawn (including during exit animation, for inspection/testing purposes).</summary>
    internal int RenderedCount => _entries.Count;

    /// <summary>Lay out new routes.</summary>
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

    /// <summary>Drops the frontmost route and returns the result. </summary>
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

        throw new InvalidOperationException("pop 対象のルートに対応する描画エントリが無い。");
    }
}

/// <summary>
/// <see cref="NavigatorController"/>Draw the root stack of (Flutter<c>Navigator</c>equivalent).
/// each route<see cref="FocusScope"/>Full screen wrapped in<see cref="Stack"/>Stack on = front in z order,
/// The focus is automatically locked to the frontmost route. <see cref="NavigatorController.Pop"/>
/// Wiring is done in one line on the app side (<c>if (b==Dismiss &amp;&amp; nav.Pop()) return;</c>）。
/// </summary>
public sealed class Navigator : StatelessWidget
{
    public NavigatorController? Controller { get; init; }

    public override Widget Build(BuildContext context)
    {
        NavigatorController controller = Controller
            ?? throw new InvalidOperationException("Navigator には Controller が必要。");

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
