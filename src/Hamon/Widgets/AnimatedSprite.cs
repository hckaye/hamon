using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Sprite sheet frame-by-frame animation (flipbook).<see cref="Texture"/>of<see cref="FrameWidth"/>×<see cref="FrameHeight"/>of
/// Divide into grids,<see cref="Fps"/>in<see cref="FrameCount"/>Display the frames in order (<see cref="Loop"/>(repeat).
/// Because I read<b>No rebuild</b>.
/// </summary>
public sealed class AnimatedSprite : Widget
{
    public required ITexture Texture { get; init; }

    /// <summary>Texture width for one frame (px).</summary>
    public required int FrameWidth { get; init; }

    /// <summary>Texture height of one frame (px).</summary>
    public required int FrameHeight { get; init; }

    /// <summary>Total number of frames.</summary>
    public required int FrameCount { get; init; }

    /// <summary>Number of columns in the sheet (0 = texture width ÷<see cref="FrameWidth"/>(calculated from).</summary>
    public int Columns { get; init; }

    /// <summary>Frames per second (default 12).</summary>
    public float Fps { get; init; } = 12f;

    /// <summary>Whether to repeat (default true; false to stop at the last frame).</summary>
    public bool Loop { get; init; } = true;

    public Color Tint { get; init; } = Color.White;

    /// <summary>Display width (unspecified<see cref="FrameWidth"/>）。</summary>
    public Dimension Width { get; init; }

    /// <summary>Display height (unspecified)<see cref="FrameHeight"/>）。</summary>
    public Dimension Height { get; init; }

    public override Element CreateElement() => new AnimatedSpriteElement(this);
}

internal sealed class AnimatedSpriteElement : Element, ITicker
{
    private readonly LayoutNode _node;
    private float _time;

    public AnimatedSpriteElement(AnimatedSprite widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private AnimatedSprite W => (AnimatedSprite)Widget;

    public override LayoutNode LayoutNode => _node;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        Context.Owner?.RegisterTicker(this);
    }

    public override void Unmount()
    {
        Context.Owner?.UnregisterTicker(this);
        base.Unmount();
    }

    public bool Tick(float dtSeconds)
    {
        _time += dtSeconds;
        return W.Loop || CurrentFrame() < W.FrameCount - 1; // 非ループは最後のコマで停止
    }

    private int CurrentFrame()
    {
        if (W.FrameCount <= 0)
        {
            return 0;
        }

        int f = (int)(_time * W.Fps);
        return W.Loop ? ((f % W.FrameCount) + W.FrameCount) % W.FrameCount : Math.Min(f, W.FrameCount - 1);
    }

    public override void Paint(in PaintContext context)
    {
        int cols = W.Columns > 0 ? W.Columns : Math.Max(1, W.Texture.Width / Math.Max(1, W.FrameWidth));
        int frame = CurrentFrame();
        int col = frame % cols;
        int row = frame / cols;
        var source = new RectInt(col * W.FrameWidth, row * W.FrameHeight, W.FrameWidth, W.FrameHeight);
        context.DrawTexture(W.Texture, _node.Bounds, source, W.Tint);
    }

    private Size MeasureSelf(BoxConstraints constraints)
    {
        float w = W.Width.Resolve(constraints.MaxWidth) ?? W.FrameWidth;
        float h = W.Height.Resolve(constraints.MaxHeight) ?? W.FrameHeight;
        return new Size(w, h);
    }
}
