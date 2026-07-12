namespace Hamon.Widgets;

/// <summary>
/// Interactive widget state (equivalent to Flutter's <c>WidgetState</c>), used with
/// <see cref="WidgetStateProperty{T}"/>.
/// <para>
/// <b>Intentional difference from Flutter</b>: Flutter represents state as an individual <c>WidgetState</c>
/// enum value plus a <c>Set&lt;WidgetState&gt;</c>. Hamon instead represents a set of states as bit flags via
/// <c>[Flags]</c>, so that resolving state on every frame/input does not allocate on the heap (a ZeroAlloc
/// exception). Use <see cref="WidgetStateExtensions.Has"/> instead of <c>Contains</c>.
/// </para>
/// </summary>
[Flags]
public enum WidgetState : byte
{
    /// <summary>No state.</summary>
    None = 0,

    /// <summary>Disabled (does not accept input, appears dimmed).</summary>
    Disabled = 1 << 0,

    /// <summary>The pointer is over the widget (mouse hover).</summary>
    Hovered = 1 << 1,

    /// <summary>In focus (keyboard/gamepad).</summary>
    Focused = 1 << 2,

    /// <summary>Being pressed.</summary>
    Pressed = 1 << 3,

    /// <summary>Selected (e.g., a toggle, tab, or checkbox in the on state).</summary>
    Selected = 1 << 4,
}

/// <summary>ZeroAlloc bit-set operations for <see cref="WidgetState"/> (equivalent to <c>Set.Contains</c>).</summary>
public static class WidgetStateExtensions
{
    /// <summary>Returns whether <paramref name="states"/> includes <paramref name="state"/> (equivalent to Flutter's <c>states.contains(...)</c>).</summary>
    public static bool Has(this WidgetState states, WidgetState state) => (states & state) != 0;
}

/// <summary>
/// A property whose value depends on state (equivalent to Flutter's <c>WidgetStateProperty&lt;T&gt;</c>).
/// Used to switch background color, foreground color, border, margin, and similar values based on
/// <see cref="WidgetState"/>.
/// <para>
/// <b>Intentional difference from Flutter</b>: instead of a resolver function taking
/// <c>Func&lt;Set&lt;WidgetState&gt;, T&gt;</c>, Hamon's resolver takes <c>Func&lt;WidgetState, T&gt;</c>
/// (a bit set), so resolving a value does not allocate on the heap (a ZeroAlloc exception).
/// </para>
/// </summary>
public sealed class WidgetStateProperty<T>
{
    private readonly T _constant;
    private readonly Func<WidgetState, T>? _resolver;

    private WidgetStateProperty(T constant, Func<WidgetState, T>? resolver)
    {
        _constant = constant;
        _resolver = resolver;
    }

    /// <summary>The same value in every state (equivalent to Flutter's <c>WidgetStatePropertyAll</c> / <c>WidgetStateProperty.all</c>).</summary>
    public static WidgetStateProperty<T> All(T value) => new(value, null);

    /// <summary>Computes a value from a state set (equivalent to Flutter's <c>WidgetStateProperty.resolveWith</c>).</summary>
    public static WidgetStateProperty<T> ResolveWith(Func<WidgetState, T> resolver) => new(default!, resolver);

    /// <summary>Resolves the value for the given state set <paramref name="states"/>.</summary>
    public T Resolve(WidgetState states) => _resolver is null ? _constant : _resolver(states);

    /// <summary>Implicit conversion to a constant value via <see cref="All"/> (e.g., <c>BackgroundColor = color</c>).</summary>
    public static implicit operator WidgetStateProperty<T>(T value) => All(value);
}
