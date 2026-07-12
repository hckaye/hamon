namespace Hamon.Layout;

/// <summary>
/// Dimensional constraints passed from parent to child in layout (equivalent to BoxConstraints in Flutter).
/// Max is<see cref="float.PositiveInfinity"/>represents "no upper limit (shrinks to fit the content)".
/// </summary>
public readonly struct BoxConstraints
{
    public float MinWidth { get; }
    public float MaxWidth { get; }
    public float MinHeight { get; }
    public float MaxHeight { get; }

    public BoxConstraints(float minWidth, float maxWidth, float minHeight, float maxHeight)
    {
        MinWidth = minWidth;
        MaxWidth = maxWidth;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
    }

    /// <summary>Relaxed constraints from 0 to the specified size.</summary>
    public static BoxConstraints Loose(Size size) => new(0, size.Width, 0, size.Height);

    /// <summary>A constraint that fixes to a specified size.</summary>
    public static BoxConstraints Tight(Size size) => new(size.Width, size.Width, size.Height, size.Height);

    /// <summary>There is no upper limit for both width and height.</summary>
    public static BoxConstraints Unbounded => new(0, float.PositiveInfinity, 0, float.PositiveInfinity);

    public bool HasBoundedWidth => float.IsFinite(MaxWidth);
    public bool HasBoundedHeight => float.IsFinite(MaxHeight);

    /// <summary>Clamp the size to a constraint range.</summary>
    public Size Constrain(Size size) => new(
        Math.Clamp(size.Width, MinWidth, MaxWidth),
        Math.Clamp(size.Height, MinHeight, MaxHeight));
}
