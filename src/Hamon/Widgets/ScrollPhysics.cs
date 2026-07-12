namespace Hamon.Widgets;

/// <summary>
/// A set of constants that determine scroll movement (Flutter<c>ScrollPhysics</c>a considerable set of adjustments).
/// Sensitivity, target tracking speed, hardness and upper limit of the rubber band at the end (overscroll), judgment of tensile hold, inertia fling
/// friction etc.<b>all adjustable</b>Make it.<see cref="HamonTheme.ScrollPhysics"/>Defaults the whole thing to each scroll
/// Widget (<see cref="ScrollView"/>/<see cref="ListView"/>/<see cref="GridView"/>)of<c>Physics</c>in
/// Can be overwritten individually.
/// </summary>
public sealed class ScrollPhysics
{
    /// <summary>
    /// Sensitivity multiplied by wheel/trackpad input delta (px) =<b>This is the main knob for scrolling effect</b>。<b>The smaller the duller</b>
    /// (The amount of movement with the same physical scroll is reduced).
    /// </summary>
    public float WheelSensitivity { get; init; } = 1f;

    /// <summary>Follow-up speed (1/sec) at which continuous scroll approaches the target position. </summary>
    public float GlideRate { get; init; } = 13f;

    /// <summary>Display limit (px) for overscroll (rubber band). </summary>
    public float MaxOverscroll { get; init; } = 140f;

    /// <summary>The grace period (in seconds) before continuous input is considered interrupted. </summary>
    public float HoldWindow { get; init; } = 0.12f;

    /// <summary>Spring speed (1/s) at which continuous overscroll returns to the boundary after input stops. </summary>
    public float SpringRate { get; init; } = 13f;

    /// <summary>Stiffness of the overscroll return spring when releasing the drag. </summary>
    public float DragSpringStiffness { get; init; } = 220f;

    /// <summary>Attenuation of the return spring when releasing the drag (≒ critical and no vibration). </summary>
    public float DragSpringDamping { get; init; } = 26f;

    /// <summary>Friction damping (velocity *= e^(-Friction*dt)) of inertia fling (drag release). </summary>
    public float Friction { get; init; } = 6f;

    /// <summary>Upper limit of fling initial velocity (px/sec). </summary>
    public float MaxFlingSpeed { get; init; } = 5500f;

    /// <summary>Upper limit of inertial velocity (px/sec). </summary>
    public float MaxSpeed { get; init; } = 9000f;

    /// <summary>Stop below this speed (px/sec). </summary>
    public float StopSpeed { get; init; } = 8f;

    /// <summary>Default (slightly slow = does not move too much relative to physical scrolling).</summary>
    public static ScrollPhysics Default { get; } = new();
}
