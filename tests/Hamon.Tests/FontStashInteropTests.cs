using FontStashSharp;
using Hamon.Fonts;
using Microsoft.Xna.Framework;
using Xunit;

namespace Hamon.Tests;

/// <summary>
/// Deterministic test for mutual conversion between FontStashSharp and MonoGame types (GPU independent).
/// Actual drawing (TTF reading → screen display) requires a GraphicsDevice/window, so verify it with Sandbox/Game.Core integration.
/// </summary>
public class FontStashInteropTests
{
    [Fact]
    public void ToXna_Vector2_PreservesComponents()
    {
        Vector2 v = FontStashInterop.ToXna(new System.Numerics.Vector2(1.5f, -2.25f));
        Assert.Equal(1.5f, v.X);
        Assert.Equal(-2.25f, v.Y);
    }

    [Fact]
    public void ToXna_Rectangle_PreservesBounds()
    {
        Rectangle r = FontStashInterop.ToXna(new System.Drawing.Rectangle(3, 4, 5, 6));
        Assert.Equal(new Rectangle(3, 4, 5, 6), r);
    }

    [Fact]
    public void ToFs_Color_PreservesChannels()
    {
        FSColor c = FontStashInterop.ToFs(new Hamon.Layout.Color(10, 20, 30, 40));
        Assert.Equal((byte)10, c.R);
        Assert.Equal((byte)20, c.G);
        Assert.Equal((byte)30, c.B);
        Assert.Equal((byte)40, c.A);
    }
}
