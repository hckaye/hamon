using FontStashSharp;
using Microsoft.Xna.Framework;
using DrawRect = System.Drawing.Rectangle;
using NumVector2 = System.Numerics.Vector2;

namespace Hamon.Fonts;

/// <summary>
/// Types used by FontStashSharp (System.Numerics.Vector2 / System.Drawing.Rectangle / FSColor)
/// MonoGame type interconversion.
/// </summary>
internal static class FontStashInterop
{
    public static Vector2 ToXna(NumVector2 v) => new(v.X, v.Y);

    public static Rectangle ToXna(DrawRect r) => new(r.X, r.Y, r.Width, r.Height);

    public static FSColor ToFs(Hamon.Layout.Color c) => new(c.R, c.G, c.B, c.A);
}
