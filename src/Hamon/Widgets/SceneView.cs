using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// <see cref="SceneView"/>The context to pass to the drawing delegate. <see cref="Painter"/>And the layout was confirmed.
/// drawing rectangle<see cref="Bounds"/>have. <see cref="Painter"/>to a backend-specific type (e.g. MonoGamePainter)
/// Cast and live drawing (original<c>SpriteBatch.Begin</c>etc.) = Core remains engine independent.
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
    /// Absolute rectangle of drawing destination = **device space (physical px)**. <see cref="Scale"/>The value multiplied by
    /// Raw drawing (SpriteBatch, etc.) draws to the back buffer = physical px, so if you use this rectangle as is, the position and dimensions will be correct.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Device pixel ratio (physical px ÷ logical pt, e.g. 2.0 for HiDPI/Retina, default 1.0).
    /// Multiply when you want to use "logical pt specified thickness/cell size" in raw drawing (e.g.<c>Line width × Scale</c>）。
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Text measurement/drawing (backend injection/text cannot be drawn if it is null).<see cref="Painter"/>has no characters, so
    /// Use this to draw text (floating damage, entity names, etc.) in the scene.<see cref="DrawText"/>/
    /// <see cref="MeasureText"/>Easy to transit (location is<b>device space</b>・Size is passed in logical pt internally<see cref="Scale"/>).
    /// </summary>
    public ITextRenderer? Text { get; }

    /// <summary>device space<paramref name="position"/>(top left) to logic<paramref name="pixelSize"/>Draw the text of (<see cref="Scale"/>included).</summary>
    public void DrawText(string text, Vec2 position, float pixelSize, Color color) =>
        Text?.Draw(text, position, pixelSize * Scale, color);

    /// <summary>logic<paramref name="pixelSize"/>text dimensions (<b>device space</b>＝<see cref="Scale"/>included). </summary>
    public Vec2 MeasureText(string text, float pixelSize) => Text is { } t ? t.Measure(text, pixelSize * Scale) : default;
}

/// <summary>A user-side implementation that takes over the drawing of the game world (cache and pass instead of generating it every frame).</summary>
public interface ISceneRenderer
{
    void Draw(in SceneDrawContext context);
}

/// <summary>
/// Embed the game world (MonoGame drawing) as one widget in the UI tree (Flutter<c>Texture</c>equivalent).
/// To the rectangle obtained from the layout<see cref="Renderer"/>／<see cref="OnDraw"/>delegate. <see cref="Clip"/>teeth
/// viewport clip (does not extend outside the rectangle).<see cref="Focusable"/>If you set the gamepad's default focus to
/// Put it on the play screen,<see cref="OnPointer"/>You can transfer the pointer inside the rectangle to the game.
/// Hamon doesn't know that the content is a game (general purpose/VLO independent).
/// </summary>
public sealed class SceneView : Widget
{
    /// <summary>Drawing delegate destination (interface).<see cref="OnDraw"/>Can be used in conjunction with (call both).</summary>
    public ISceneRenderer? Renderer { get; init; }

    /// <summary>Drawing delegate (delegate). </summary>
    public Action<SceneDrawContext>? OnDraw { get; init; }

    /// <summary>Transfer the pointer (touch/mouse) inside the rectangle to the game. </summary>
    public Action<PointerEvent>? OnPointer { get; init; }

    /// <summary>Width (default Auto = fill available width).</summary>
    public Dimension Width { get; init; }

    /// <summary>Height (default Auto = fill available height).</summary>
    public Dimension Height { get; init; }

    /// <summary>Clip the drawing to the viewport rectangle (default true). </summary>
    public bool Clip { get; init; } = true;

    /// <summary>Make the gamepad the focus target (such as setting the default focus on the play screen).</summary>
    public bool Focusable { get; init; }

    /// <summary>Focus holding node.<see cref="Focusable"/>(recommended to be retained by the caller to retain the state).</summary>
    public FocusNode Node { get; init; } = new();

    /// <summary><see cref="Focusable"/>Get the initial focus at the time.</summary>
    public bool Autofocus { get; init; }

    public override Element CreateElement() => new SceneViewElement(this);
}

/// <summary><see cref="SceneView"/>holding entity. </summary>
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
