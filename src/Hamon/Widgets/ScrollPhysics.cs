namespace Hamon.Widgets;

/// <summary>
/// A set of constants that determine scroll movement (roughly equivalent to Flutter's <c>ScrollPhysics</c>).
/// Wheel sensitivity, glide (target-tracking) speed, the stiffness and limit of edge rubber-banding (overscroll),
/// hold/interruption detection, and inertial fling friction are <b>all adjustable</b>. <see cref="HamonTheme.ScrollPhysics"/>
/// supplies a default for the whole app, and each scroll widget (<see cref="ScrollView"/>/<see cref="ListView"/>/
/// <see cref="GridView"/>) can override it individually via its <c>Physics</c> property.
/// </summary>
public sealed class ScrollPhysics
{
    /// <summary>
    /// Sensitivity multiplier applied to wheel/trackpad input delta (px). <b>This is the main knob for tuning how
    /// scrolling feels.</b> <b>Smaller values feel duller</b> — the same physical scroll input produces less movement.
    /// </summary>
    public float WheelSensitivity { get; init; } = 1f;

    /// <summary>Follow speed (1/sec) at which continuous scrolling glides toward its target position.</summary>
    public float GlideRate { get; init; } = 13f;

    /// <summary>Maximum displayed overscroll distance (rubber band), in px.</summary>
    public float MaxOverscroll { get; init; } = 140f;

    /// <summary>Grace period (in seconds) before continuous input is considered interrupted.</summary>
    public float HoldWindow { get; init; } = 0.12f;

    /// <summary>Spring rate (1/sec) at which overscroll returns to the boundary after continuous input stops.</summary>
    public float SpringRate { get; init; } = 13f;

    /// <summary>Stiffness of the overscroll return spring when releasing a drag.</summary>
    public float DragSpringStiffness { get; init; } = 220f;

    /// <summary>Damping of the return spring when releasing a drag (approximately critical damping = no oscillation).</summary>
    public float DragSpringDamping { get; init; } = 26f;

    /// <summary>Friction damping applied to inertial fling after a drag release (velocity *= e^(-Friction * dt)).</summary>
    public float Friction { get; init; } = 6f;

    /// <summary>Upper limit on initial fling velocity (px/sec).</summary>
    public float MaxFlingSpeed { get; init; } = 5500f;

    /// <summary>Upper limit on inertial velocity (px/sec).</summary>
    public float MaxSpeed { get; init; } = 9000f;

    /// <summary>Inertial scrolling stops once speed falls below this value (px/sec).</summary>
    public float StopSpeed { get; init; } = 8f;

    /// <summary>The default physics (slightly conservative = does not overshoot relative to the physical scroll input).</summary>
    public static ScrollPhysics Default { get; } = new();
}
