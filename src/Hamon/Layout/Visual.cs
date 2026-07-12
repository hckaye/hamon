namespace Hamon.Layout;

/// <summary>2D vector (px/float). </summary>
public readonly struct Vec2 : IEquatable<Vec2>
{
    public Vec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; }

    public float Y { get; }

    public static Vec2 Zero => default;

    public static Vec2 One => new(1f, 1f);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vec2 operator *(Vec2 a, Vec2 b) => new(a.X * b.X, a.Y * b.Y); // 成分ごと

    public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);

    public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"Vec2({X}, {Y})";
}

/// <summary>Integer rectangle (a partial area of a texture, e.g. a cell in a sprite sheet).</summary>
public readonly struct RectInt : IEquatable<RectInt>
{
    public RectInt(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public bool Equals(RectInt other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is RectInt r && Equals(r);

    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
}

/// <summary>RGBA color (8 bit each). </summary>
public readonly struct Color : IEquatable<Color>
{
    public Color(int r, int g, int b, int a)
    {
        R = Clamp(r);
        G = Clamp(g);
        B = Clamp(b);
        A = Clamp(a);
    }

    public Color(int r, int g, int b)
        : this(r, g, b, 255)
    {
    }

    public byte R { get; }

    public byte G { get; }

    public byte B { get; }

    public byte A { get; }

    public static Color White => new(255, 255, 255);

    public static Color Black => new(0, 0, 0);

    public static Color Red => new(255, 0, 0);

    public static Color SkyBlue => new(135, 206, 235);

    public static Color SteelBlue => new(70, 130, 180);

    public static Color Goldenrod => new(218, 165, 32);

    public static Color IndianRed => new(205, 92, 92);

    public static Color Khaki => new(240, 230, 140);

    public static Color LightGray => new(211, 211, 211);

    public static Color LightGreen => new(144, 238, 144);

    public static Color MediumSeaGreen => new(60, 179, 113);

    /// <summary>Linear interpolation (per component).</summary>
    public static Color Lerp(Color a, Color b, float t) => new(
        (int)(a.R + ((b.R - a.R) * t)),
        (int)(a.G + ((b.G - a.G) * t)),
        (int)(a.B + ((b.B - a.B) * t)),
        (int)(a.A + ((b.A - a.A) * t)));

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;

    public override bool Equals(object? obj) => obj is Color c && Equals(c);

    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    private static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
}
