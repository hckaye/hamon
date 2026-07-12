using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A controller that holds and manipulates the view transform (scale + translation) of an
/// <see cref="InteractiveViewer"/> (equivalent to Flutter's <c>TransformationController</c>). It can be driven both
/// by internal pan/pinch gestures within the <see cref="InteractiveViewer"/> and by external operations such as
/// button-triggered <see cref="ZoomIn"/>/<see cref="ZoomOut"/>/<see cref="Reset"/> calls. <see cref="OnChanged"/> is
/// fired whenever the transform changes (i.e. a host redraw request).
/// </summary>
public sealed class InteractiveViewerController
{
    private Transform2D _value = Transform2D.Identity;

    /// <summary>Fired when the transform changes (used by <see cref="InteractiveViewer"/> as a redraw request).</summary>
    internal Action? OnChanged;

    /// <summary>The viewport rectangle, reported by <see cref="InteractiveViewer"/> when it draws. <see cref="ZoomIn"/>/<see cref="ZoomOut"/> zoom about its center.</summary>
    internal Rect Viewport;

    /// <summary>Minimum magnification (default 0.5).</summary>
    public float MinScale { get; set; } = 0.5f;

    /// <summary>Maximum magnification (default 4.0).</summary>
    public float MaxScale { get; set; } = 4f;

    /// <summary>The current magnification (1 = normal).</summary>
    public float Scale => _value.Scale.X;

    /// <summary>Zooms in by <paramref name="factor"/> about the center of the viewport (for a + button).</summary>
    public void ZoomIn(float factor = 1.25f) => ZoomAbout(Center(), factor);

    /// <summary>Zooms out by <paramref name="factor"/> about the center of the viewport (for a - button).</summary>
    public void ZoomOut(float factor = 1.25f) => ZoomAbout(Center(), 1f / factor);

    /// <summary>Resets to the original size and position.</summary>
    public void Reset() => SetRaw(Transform2D.Identity);

    internal Transform2D Value => _value;

    internal void SetViewport(Rect viewport) => Viewport = viewport; // 通知しない（描画中の呼び出し＝ループ防止）

    /// <summary>Translates by <paramref name="delta"/> (viewport coordinates).</summary>
    internal void PanBy(Vec2 delta) =>
        SetRaw(new Transform2D(_value.Scale, new Vec2(_value.Translate.X + delta.X, _value.Translate.Y + delta.Y)));

    /// <summary>Zooms by <paramref name="factor"/> while keeping the content point under <paramref name="focal"/> (viewport coordinates) fixed.</summary>
    internal void ZoomAbout(Vec2 focal, float factor) => ApplyZoom(focal, ContentAt(focal), _value.Scale.X * factor);

    /// <summary>Returns the content coordinates under <paramref name="focal"/> (viewport coordinates), using the current transform.</summary>
    internal Vec2 ContentAt(Vec2 focal)
    {
        float s = _value.Scale.X == 0f ? 1f : _value.Scale.X;
        return new Vec2((focal.X - _value.Translate.X) / s, (focal.Y - _value.Translate.Y) / s);
    }

    /// <summary>Sets the scale to <paramref name="newScale"/> while keeping <paramref name="anchorContent"/> (content coordinates) aligned under <paramref name="focal"/> (viewport coordinates).</summary>
    internal void ApplyZoom(Vec2 focal, Vec2 anchorContent, float newScale)
    {
        float ns = Math.Clamp(newScale, MinScale, MaxScale);
        var translate = new Vec2(focal.X - (anchorContent.X * ns), focal.Y - (anchorContent.Y * ns));
        SetRaw(new Transform2D(new Vec2(ns, ns), translate));
    }

    private void SetRaw(Transform2D value)
    {
        _value = value;
        OnChanged?.Invoke();
    }

    private Vec2 Center() => new(Viewport.X + (Viewport.Width / 2f), Viewport.Y + (Viewport.Height / 2f));
}

/// <summary>
/// A container that pans/zooms its child while still <b>allowing child interactions (taps, etc.) to work</b>
/// (equivalent to Flutter's <c>InteractiveViewer</c>). Internally, a <see cref="GestureDetector"/> covering the free
/// space handles <b>one-finger drag = pan / two-finger pinch = zoom</b>, and the resulting transform is applied via
/// an <see cref="InteractiveViewerController"/> (external operations such as +/- buttons go through the same
/// controller). The child is drawn with a <see cref="Transform2D"/> and clipped, while hit testing/pointer delivery
/// uses the inverse transform (<see cref="Element.ChildHitTestTransform"/>) so hit detection for buttons, etc.,
/// remains aligned with what is displayed even while scaling or panning.
/// <para>
/// The child is laid out in parent (viewport) coordinates <b>as-is</b>; only the pan/zoom transform is applied on
/// top, so no camera calculations are required on the child's side. Pans/zooms only start from an area not covered
/// by a pointer-consuming child (a gap or the background). See also <see cref="InteractiveViewerController.Reset"/>.
/// </para>
/// </summary>
public sealed class InteractiveViewer : HookWidget
{
    /// <summary>The child to pan/zoom.</summary>
    public Widget? Child { get; init; }

    /// <summary>The controller to share/operate externally (created internally if not specified).</summary>
    public InteractiveViewerController? Controller { get; init; }

    /// <summary>Minimum magnification (default 0.5).</summary>
    public float MinScale { get; init; } = 0.5f;

    /// <summary>Maximum magnification (default 4.0).</summary>
    public float MaxScale { get; init; } = 4f;

    /// <summary>Pan with one finger drag (default true).</summary>
    public bool PanEnabled { get; init; } = true;

    /// <summary>Zoom with two-finger pinch (default true).</summary>
    public bool ZoomEnabled { get; init; } = true;

    /// <summary>Clip the display to the viewport (default true).</summary>
    public bool Clip { get; init; } = true;

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        InteractiveViewerController controller = Controller ?? hooks.UseMemo(() => new InteractiveViewerController());
        controller.MinScale = MinScale;
        controller.MaxScale = MaxScale;
        InteractiveViewerPinch pinch = hooks.UseMemo(() => new InteractiveViewerPinch());

        IHamonHost? owner = context.Owner;
        hooks.UseEffect(
            () =>
            {
                controller.OnChanged = () => owner?.MarkDirty();
                return () => controller.OnChanged = null;
            },
            controller);

        return new GestureDetector
        {
            OnPanUpdate = PanEnabled ? d => controller.PanBy(d.Delta) : null,
            OnScaleStart = ZoomEnabled ? s => pinch.Begin(controller, s.FocalPoint) : null,
            OnScaleUpdate = ZoomEnabled ? s => pinch.Update(controller, s.FocalPoint, s.Scale) : null,
            Child = new TransformViewport
            {
                Transform = controller.Value,
                Clip = Clip,
                OnLayout = controller.SetViewport,
                Child = Child,
            },
        };
    }
}

/// <summary>Holds the zoom start scale and anchor point for a two-finger pinch, keeping the starting content point under focus (internal to <see cref="InteractiveViewer"/>).</summary>
internal sealed class InteractiveViewerPinch
{
    private float _startScale = 1f;
    private Vec2 _anchor;

    public void Begin(InteractiveViewerController controller, Vec2 focal)
    {
        _startScale = controller.Scale;
        _anchor = controller.ContentAt(focal);
    }

    public void Update(InteractiveViewerController controller, Vec2 focal, float scaleRelative) =>
        controller.ApplyZoom(focal, _anchor, _startScale * scaleRelative);
}

/// <summary>
/// An internal widget that draws its child with a <see cref="Transform2D"/> and (optionally) clips it to the
/// viewport, providing the inverse transform for hit testing/pointer delivery. Unlike drawing, which applies
/// <see cref="Transform"/> only, hit testing uses <see cref="Element.ChildHitTestTransform"/>, so the child remains
/// interactive.
/// </summary>
internal sealed class TransformViewport : Widget, IRenderConfig
{
    public required Transform2D Transform { get; init; }

    public Widget? Child { get; init; }

    public bool Clip { get; init; } = true;

    /// <summary>The viewport rectangle (the finalized <c>Bounds</c>) when drawing, used by the controller as its center/pivot.</summary>
    public Action<Rect>? OnLayout { get; init; }

    // 単一子パススルー（子はビューポートを充填）。レイアウトは透過し、描画とヒットテストに変換を掛ける。
    Style IRenderConfig.Style => new() { Kind = LayoutKind.Stack, StackExpandChildren = true };
    IReadOnlyList<Widget>? IRenderConfig.Children => Child is null ? null : new[] { Child };
    Color? IRenderConfig.Background => null;
    Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

    public override Element CreateElement() => new TransformViewportElement(this);
}

internal sealed class TransformViewportElement : RenderElement
{
    public TransformViewportElement(TransformViewport widget)
        : base(widget)
    {
    }

    private TransformViewport View => (TransformViewport)Widget;

    internal override Transform2D? ChildHitTestTransform => View.Transform.Inverse();

    public override void Paint(in PaintContext context)
    {
        TransformViewport view = View;
        view.OnLayout?.Invoke(LayoutNode.Bounds);

        if (view.Clip)
        {
            object? token = context.PushClip(LayoutNode.Bounds);
            base.Paint(context.WithTransform(view.Transform));
            context.PopClip(token);
        }
        else
        {
            base.Paint(context.WithTransform(view.Transform));
        }
    }
}
