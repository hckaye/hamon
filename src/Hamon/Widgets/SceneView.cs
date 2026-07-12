using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// The context passed to <see cref="SceneView"/>'s drawing delegate. It carries <see cref="Painter"/> and the
/// layout-resolved drawing rectangle <see cref="Bounds"/>. Cast <see cref="Painter"/> to a backend-specific type
/// (e.g. MonoGamePainter) to perform raw drawing (e.g. calling <c>SpriteBatch.Begin</c> directly), keeping Core
/// engine-independent.
/// </summary>
public readonly struct SceneDrawContext
{
    public SceneDrawContext(IPainter painter, Rect bounds, float scale, ITextRenderer? text = null)
    {
        Painter = painter;
        Bounds = bounds;
        Scale = scale;
        Text = text;
    }

    /// <summary>Drawing backend (raw drawing is also possible by casting to a backend-specific type).</summary>
    public IPainter Painter { get; }

    /// <summary>
    /// Absolute rectangle of the drawing destination in device space (physical pixels), already multiplied by
    /// <see cref="Scale"/>. Since raw drawing (SpriteBatch, etc.) draws to the back buffer in physical pixels, using
    /// this rectangle as-is gives the correct position and size.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Device pixel ratio (physical px ÷ logical pt, e.g. 2.0 for HiDPI/Retina, default 1.0).
    /// Multiply values specified in logical pt (line thickness, cell size, etc.) by this when doing raw drawing
    /// (e.g. <c>lineWidth * Scale</c>).
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Text measurement/drawing backend (injected; text cannot be drawn if this is null). <see cref="Painter"/> has
    /// no text-drawing capability of its own, so use this to draw text in the scene (e.g. floating damage numbers,
    /// entity names). <see cref="DrawText"/> and <see cref="MeasureText"/> are convenience wrappers around it:
    /// position is in device space, and size is specified in logical pt and multiplied internally by
    /// <see cref="Scale"/>.
    /// </summary>
    public ITextRenderer? Text { get; }

    /// <summary>Draws text at <paramref name="position"/> in device space (top-left) with a logical <paramref name="pixelSize"/> (multiplied by <see cref="Scale"/> internally).</summary>
    public void DrawText(string text, Vec2 position, float pixelSize, Color color) =>
        Text?.Draw(text, position, pixelSize * Scale, color);

    /// <summary>Measures the size of the text for a logical <paramref name="pixelSize"/>, returned in device space (multiplied by <see cref="Scale"/>).</summary>
    public Vec2 MeasureText(string text, float pixelSize) => Text is { } t ? t.Measure(text, pixelSize * Scale) : default;
}

/// <summary>A user-supplied implementation responsible for drawing the game world (cache it and pass the same instance instead of allocating a new one every frame).</summary>
public interface ISceneRenderer
{
    void Draw(in SceneDrawContext context);
}

/// <summary>
/// Embeds the game world (MonoGame drawing) as a single widget in the UI tree (equivalent to Flutter's <c>Texture</c>).
/// Drawing is delegated to <see cref="Renderer"/> / <see cref="OnDraw"/> for the rectangle obtained from layout.
/// <see cref="Clip"/> clips drawing to the viewport rectangle (content does not extend outside it). Setting
/// <see cref="Focusable"/> lets you make this the gamepad's default focus target on the play screen, and
/// <see cref="OnPointer"/> lets you forward pointer input inside the rectangle to the game. Hamon has no knowledge
/// that the content is a game — it remains general-purpose and engine-independent.
/// </summary>
public sealed class SceneView : Widget
{
    /// <summary>Drawing delegate as an interface. Can be used together with <see cref="OnDraw"/> (both are invoked).</summary>
    public ISceneRenderer? Renderer { get; init; }

    /// <summary>Drawing delegate.</summary>
    public Action<SceneDrawContext>? OnDraw { get; init; }

    /// <summary>Forwards pointer (touch/mouse) input inside the rectangle to the game.</summary>
    public Action<PointerEvent>? OnPointer { get; init; }

    /// <summary>Width (default Auto = fill available width).</summary>
    public Dimension Width { get; init; }

    /// <summary>Height (default Auto = fill available height).</summary>
    public Dimension Height { get; init; }

    /// <summary>Clips drawing to the viewport rectangle (default true).</summary>
    public bool Clip { get; init; } = true;

    /// <summary>Makes this widget a gamepad focus target (e.g. to set the default focus on the play screen).</summary>
    public bool Focusable { get; init; }

    /// <summary>The focus node. Used only when <see cref="Focusable"/> is true (recommended to be retained by the caller to preserve state across rebuilds).</summary>
    public FocusNode Node { get; init; } = new();

    /// <summary>Whether to receive initial focus when <see cref="Focusable"/> is true.</summary>
    public bool Autofocus { get; init; }

    public override Element CreateElement() => new SceneViewElement(this);
}

/// <summary>The element that backs <see cref="SceneView"/>.</summary>
internal sealed class SceneViewElement : Element
{
    private readonly LayoutNode _node;

    public SceneViewElement(SceneView widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    public override LayoutNode LayoutNode => _node;

    private SceneView View => (SceneView)Widget;

    public override bool WantsPointer => View.OnPointer is not null;

    internal override FocusNode? FocusNodeOrNull => View.Focusable ? View.Node : null;

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        if (View.Focusable && context.Focusable)
        {
            context.Focus?.Register(View.Node, () => _node.Bounds, EnclosingScope(), EnclosingScrollable(), context.Traversable);
            if (View.Autofocus)
            {
                context.Focus?.AutofocusIfNone(View.Node);
            }
        }
    }

    public override void Unmount()
    {
        if (View.Focusable)
        {
            Context.Focus?.Unregister(View.Node);
        }

        base.Unmount();
    }

    public override void HandlePointer(in PointerEvent pointer) => View.OnPointer?.Invoke(pointer);

    public override void Paint(in PaintContext context)
    {
        SceneView view = View;
        Rect bounds = _node.Bounds;
        if (bounds.Width <= 0f || bounds.Height <= 0f || (view.Renderer is null && view.OnDraw is null))
        {
            return;
        }

        // 生描画はバックバッファ＝物理px に対して描くため、デバイス空間の矩形とスケールを渡す（HiDPI/Retina）。
        var scene = new SceneDrawContext(context.Painter, context.ApplyTransform(bounds), context.ScaleY, context.Text);

        if (view.Clip)
        {
            object? previous = context.PushClip(bounds);
            Invoke(view, scene);
            context.PopClip(previous);
        }
        else
        {
            Invoke(view, scene);
        }
    }

    private static void Invoke(SceneView view, in SceneDrawContext scene)
    {
        view.Renderer?.Draw(scene);
        view.OnDraw?.Invoke(scene);
    }

    private Size MeasureSelf(BoxConstraints constraints)
    {
        SceneView view = View;
        float width = view.Width.Resolve(constraints.MaxWidth)
            ?? (constraints.HasBoundedWidth ? constraints.MaxWidth : constraints.MinWidth);
        float height = view.Height.Resolve(constraints.MaxHeight)
            ?? (constraints.HasBoundedHeight ? constraints.MaxHeight : constraints.MinHeight);
        return constraints.Constrain(new Size(width, height));
    }
}
