namespace Hamon.Widgets;

/// <summary>
/// Interactive widget state (Flutter<c>WidgetState</c>equivalent).
/// （<see cref="WidgetStateProperty{T}"/>) used for
/// <para>
/// <b>Differences with Flutter (intentional)</b>:Flutter is<c>WidgetState</c>Individual enum +<c>Set&lt;WidgetState&gt;</c>in
/// Hamon represents a set of states.<b>Do not allocate heap by resolving every frame/input</b>Because<c>[Flags]</c>in
/// Represents a set in bits (ZeroAlloc exception). <c>Contains</c>teeth<see cref="WidgetStateExtensions.Has"/>。
/// </para>
/// </summary>
[Flags]
public enum WidgetState : byte
{
    /// <summary>No condition.</summary>
    None = 0,

    /// <summary>Disabled (does not accept input, appears dimmed).</summary>
    Disabled = 1 << 0,

    /// <summary>The pointer is on top (mouse hover).</summary>
    Hovered = 1 << 1,

    /// <summary>In focus (keyboard/gamepad).</summary>
    Focused = 1 << 2,

    /// <summary>Pressing down.</summary>
    Pressed = 1 << 3,

    /// <summary>Selected (toggle/tab/check, etc. ON).</summary>
    Selected = 1 << 4,
}

/// <summary><see cref="WidgetState"/>bit set operations (such as ZeroAlloc)<c>Set.Contains</c>equivalent).</summary>
public static class WidgetStateExtensions
{
    /// <summary><paramref name="states"/>to<paramref name="state"/>(Flutter's<c>states.contains(...)</c>equivalent).</summary>
    public static bool Has(this WidgetState states, WidgetState state) => (states & state) != 0;
}

/// <summary>
/// Properties that resolve values ​​depending on state (Flutter<c>WidgetStateProperty&lt;T&gt;</c>equivalent).
/// Background color, foreground color, frame, margin, etc.<see cref="WidgetState"/>Used to switch between.
/// <para>
/// <b>Differences with Flutter (intentional)</b>:The resolution function is<c>Func&lt;Set&lt;WidgetState&gt;, T&gt;</c>not
/// <c>Func&lt;WidgetState, T&gt;</c>Receive (bit set) = do not allocate heap during resolution (ZeroAlloc exception).
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

    /// <summary>Same value in all states (Flutter<c>WidgetStatePropertyAll</c> / <c>WidgetStateProperty.all</c>equivalent).</summary>
    public static WidgetStateProperty<T> All(T value) => new(value, null);

    /// <summary>Compute values ​​from a set of states (Flutter<c>WidgetStateProperty.resolveWith</c>equivalent).</summary>
    public static WidgetStateProperty<T> ResolveWith(Func<WidgetState, T> resolver) => new(default!, resolver);

    /// <summary>state set<paramref name="states"/>Resolve the value for .</summary>
    public T Resolve(WidgetState states) => _resolver is null ? _constant : _resolver(states);

    /// <summary>constant value<see cref="All"/>Implicit conversion as (<c>BackgroundColor = color</c>).</summary>
    public static implicit operator WidgetStateProperty<T>(T value) => All(value);
}
