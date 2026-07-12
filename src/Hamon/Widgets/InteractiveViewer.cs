using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// <see cref="InteractiveViewer"/>A controller (Flutter) that maintains and operates the view transformation (scaling + translation) of<c>TransformationController</c>equivalent).
/// Bread/pinch<see cref="InteractiveViewer"/>From internal gestures to external operations such as buttons<see cref="ZoomIn"/>/<see cref="ZoomOut"/>/
/// <see cref="Reset"/>Drive from. <see cref="OnChanged"/>(=host redraw request) is fired.
/// </summary>
public sealed class InteractiveViewerController
{
    private Transform2D _value = Transform2D.Identity;

    /// <summary>When the transformation changes (due to a redraw request)<see cref="InteractiveViewer"/>).</summary>
    internal Action? OnChanged;

    /// <summary>Viewport rectangle (<see cref="InteractiveViewer"/>(Notify when drawing).<see cref="ZoomIn"/>/<see cref="ZoomOut"/>Use the fulcrum = center.</summary>
    internal Rect Viewport;

    /// <summary>Minimum magnification (default 0.5).</summary>
    public float MinScale { get; set; } = 0.5f;

    /// <summary>Maximum magnification (default 4.0).</summary>
    public float MaxScale { get; set; } = 4f;

    /// <summary>Current magnification (1=normal).</summary>
    public float Scale => _value.Scale.X;

    /// <summary>Center of viewport as fulcrum<paramref name="factor"/>Zoom in twice (for + button).</summary>
    public void ZoomIn(float factor = 1.25f) => ZoomAbout(Center(), factor);

    /// <summary>Reduce the viewport using the center as the fulcrum (for - button).</summary>
    public void ZoomOut(float factor = 1.25f) => ZoomAbout(Center(), 1f / factor);

    /// <summary>Same size/return to origin.</summary>
    public void Reset() => SetRaw(Transform2D.Identity);

    internal Transform2D Value => _value;

    internal void SetViewport(Rect viewport) => Viewport = viewport; // 通知しない（描画中の呼び出し＝ループ防止）

    /// <summary><paramref name="delta"/>Translate by (viewport coordinates).</summary>
    internal void PanBy(Vec2 delta) =>
        SetRaw(new Transform2D(_value.Scale, new Vec2(_value.Translate.X + delta.X, _value.Translate.Y + delta.Y)));

    /// <summary><paramref name="focal"/>Keep the content point pointed to by (viewport coordinates) fixed<paramref name="factor"/>Zoom in twice.</summary>
    internal void ZoomAbout(Vec2 focal, float factor) => ApplyZoom(focal, ContentAt(focal), _value.Scale.X * factor);

    /// <summary>with the current conversion<paramref name="focal"/>Content coordinates pointed to by (viewport coordinates). </summary>
    internal Vec2 ContentAt(Vec2 focal)
    {
        float s = _value.Scale.X == 0f ? 1f : _value.Scale.X;
        return new Vec2((focal.X - _value.Translate.X) / s, (focal.Y - _value.Translate.Y) / s);
    }

    /// <summary><paramref name="anchorContent"/>(content coordinates)<paramref name="focal"/>Below (viewport coordinates)<paramref name="newScale"/>Match with</summary>
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
/// While panning/zooming the child<b>Make use of child operations (tap, etc.)</b>Container (Flutter<c>InteractiveViewer</c>equivalent).
/// inside<see cref="GestureDetector"/>is the free space<b>1 finger drag = pan / 2 finger pinch = zoom</b>and the conversion is
/// <see cref="InteractiveViewerController"/>(External operations such as +/- buttons also go through the same controller).
/// <see cref="Transform2D"/>Draw + clip with , hit test/pointer delivery is the inverse transformation (<see cref="Element.ChildHitTestTransform"/>)in
/// Since it is copied to the child coordinates, the hit detection of buttons etc. will match the display even during scaling/parallel movement.
/// <para>
/// child to parent (viewport) coordinates<b>As is</b>Layout, pan/zoom conversion only = no camera calculation required on child side.
/// Start from an area not covered by the pointer receiver (gap/background). <see cref="InteractiveViewerController.Reset"/>).
/// </para>
/// </summary>
public sealed class InteractiveViewer : HookWidget
{
    /// <summary>Child to pan/zoom to.</summary>
    public Widget? Child { get; init; }

    /// <summary>Controller to be shared/operated externally (generated internally if not specified). </summary>
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

/// <summary>Pinch (two fingers) to hold the zoom start anchor and scale while keeping the starting content point under focus (<see cref="InteractiveViewer"/>internal).</summary>
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
/// child<see cref="Transform2D"/>An internal element that draws + (optionally) clips to the viewport and provides inverse transformation for hit testing/shipping.
/// drawing only<see cref="Transform"/>Unlike<see cref="Element.ChildHitTestTransform"/>, so the child remains interactive.
/// </summary>
internal sealed class TransformViewport : Widget, IRenderConfig
{
    public required Transform2D Transform { get; init; }

    public Widget? Child { get; init; }

    public bool Clip { get; init; } = true;

    /// <summary>Viewport rectangle (confirmed) when drawing<c>Bounds</c>) (used as the center/fulcrum of the controller).</summary>
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
