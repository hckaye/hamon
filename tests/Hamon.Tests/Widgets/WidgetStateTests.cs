using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary><see cref="WidgetState"/>bit set and  <see cref="WidgetStateProperty{T}"/>Determinism test of solution. </summary>
public class WidgetStateTests
{
    [Fact]
    public void Has_DetectsContainedState()
    {
        WidgetState states = WidgetState.Hovered | WidgetState.Focused;
        Assert.True(states.Has(WidgetState.Hovered));
        Assert.True(states.Has(WidgetState.Focused));
        Assert.False(states.Has(WidgetState.Pressed));
        Assert.False(states.Has(WidgetState.Disabled));
    }

    [Fact]
    public void None_ContainsNothing()
    {
        Assert.False(WidgetState.None.Has(WidgetState.Hovered));
        Assert.False(WidgetState.None.Has(WidgetState.Disabled));
    }

    [Fact]
    public void All_ReturnsSameValueForEveryState()
    {
        WidgetStateProperty<int> prop = WidgetStateProperty<int>.All(42);
        Assert.Equal(42, prop.Resolve(WidgetState.None));
        Assert.Equal(42, prop.Resolve(WidgetState.Pressed));
        Assert.Equal(42, prop.Resolve(WidgetState.Hovered | WidgetState.Focused));
    }

    [Fact]
    public void ResolveWith_PicksByPriority()
    {
        // disabled > pressed > hovered > 既定 の優先順で解決する典型パターン。
        WidgetStateProperty<Color> prop = WidgetStateProperty<Color>.ResolveWith(static s =>
            s.Has(WidgetState.Disabled) ? new Color(10, 10, 10)
            : s.Has(WidgetState.Pressed) ? new Color(20, 20, 20)
            : s.Has(WidgetState.Hovered) ? new Color(30, 30, 30)
            : new Color(40, 40, 40));

        Assert.Equal(new Color(40, 40, 40), prop.Resolve(WidgetState.None));
        Assert.Equal(new Color(30, 30, 30), prop.Resolve(WidgetState.Hovered));
        Assert.Equal(new Color(20, 20, 20), prop.Resolve(WidgetState.Pressed | WidgetState.Hovered));
        Assert.Equal(new Color(10, 10, 10), prop.Resolve(WidgetState.Disabled | WidgetState.Pressed));
    }

    [Fact]
    public void ImplicitConversion_BehavesAsAll()
    {
        WidgetStateProperty<int> prop = 7; // implicit operator
        Assert.Equal(7, prop.Resolve(WidgetState.Hovered));
    }

    [Fact]
    public void NullableValue_ResolvesNullForFallback()
    {
        // null を返せる＝利用側で `prop.Resolve(s) ?? themeDefault` の fallback ができる。
        WidgetStateProperty<Color?> prop = WidgetStateProperty<Color?>.ResolveWith(static s =>
            s.Has(WidgetState.Pressed) ? new Color(1, 2, 3) : (Color?)null);

        Assert.Null(prop.Resolve(WidgetState.None));
        Assert.Equal(new Color(1, 2, 3), prop.Resolve(WidgetState.Pressed));
    }
}
