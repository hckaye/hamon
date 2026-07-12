namespace Hamon.Layout;

/// <summary>Dimensions for layout (px/float). </summary>
public readonly struct Size : IEquatable<Size>
{
    public float Width { get; }
    public float Height { get; }

    public Size(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public static Size Zero => default;

    public bool Equals(Size other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Size s && Equals(s);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public override string ToString() => $"Size({Width}, {Height})";
}

/// <summary>Rectangle (absolute coordinates/px) after layout is confirmed.</summary>
public readonly struct Rect : IEquatable<Rect>
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public Rect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool Contains(float px, float py) => px >= X && px < Right && py >= Y && py < Bottom;

    public bool Equals(Rect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Rect r && Equals(r);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public override string ToString() => $"Rect({X}, {Y}, {Width}, {Height})";
}

/// <summary>Margins on all sides (shared margin/padding).</summary>
public readonly struct EdgeInsets : IEquatable<EdgeInsets>
{
    public float Left { get; }
    public float Top { get; }
    public float Right { get; }
    public float Bottom { get; }

    public EdgeInsets(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static EdgeInsets Zero => default;
    public static EdgeInsets All(float v) => new(v, v, v, v);
    public static EdgeInsets Symmetric(float horizontal, float vertical) => new(horizontal, vertical, horizontal, vertical);

    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;

    public bool Equals(EdgeInsets other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
    public override bool Equals(object? obj) => obj is EdgeInsets e && Equals(e);
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
}

/// <summary>Unit of measurement.</summary>
public enum DimensionUnit : byte
{
    /// <summary>Determined by content/Flex (default).</summary>
    Auto,

    /// <summary>Fixed pixel.</summary>
    Pixels,

    /// <summary>Percentage of parent's corresponding range (0–100).</summary>
    Percent,
}

/// <summary>Declarative specification of dimensions such as width and height. <see cref="DimensionUnit.Auto"/>。</summary>
public readonly struct Dimension : IEquatable<Dimension>
{
    public DimensionUnit Unit { get; }
    public float Value { get; }

    private Dimension(DimensionUnit unit, float value)
    {
        Unit = unit;
        Value = value;
    }

    public static Dimension Auto => default;
    public static Dimension Px(float pixels) => new(DimensionUnit.Pixels, pixels);
    public static Dimension Percent(float percent) => new(DimensionUnit.Percent, percent);

    public bool IsAuto => Unit == DimensionUnit.Auto;

    /// <summary>
    /// parent range<paramref name="parentExtent"/>Resolves for (px).
    /// If Auto or Percent and the parent range is non-finite, returns null as unresolvable.
    /// </summary>
    public float? Resolve(float parentExtent) => Unit switch
    {
        DimensionUnit.Pixels => Value,
        DimensionUnit.Percent => float.IsFinite(parentExtent) ? Value / 100f * parentExtent : null,
        _ => null,
    };

    public bool Equals(Dimension other) => Unit == other.Unit && Value == other.Value;
    public override bool Equals(object? obj) => obj is Dimension d && Equals(d);
    public override int GetHashCode() => HashCode.Combine((byte)Unit, Value);
}
