using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A determinate progress bar (equivalent to Flutter's <c>LinearProgressIndicator</c>). Progress is reflected
/// via <see cref="Value"/> (0..1) or <see cref="ValueGetter"/> (read at draw time, for binding/animation).
/// The fill is drawn by scaling its width along the X axis, independent of the bar's overall <see cref="Width"/>.
/// </summary>
public sealed class ProgressBar : Widget
{
    public float Value { get; init; }

    public Func<float>? ValueGetter { get; init; }

    /// <summary>Track color (defaults to the theme's <see cref="HamonTheme.SurfaceVariant"/> if unspecified).</summary>
    public Color? Track { get; init; }

    /// <summary>Fill color (defaults to the theme's <see cref="HamonTheme.Primary"/> if unspecified).</summary>
    public Color? Fill { get; init; }

    /// <summary>Image skin (9-slice/sprite) for the rail (the entire track).</summary>
    public ImageSkin TrackSkin { get; init; }

    /// <summary>Image skin for the fill (drawn from the left up to the current value).</summary>
    public ImageSkin FillSkin { get; init; }

    public float Height { get; init; } = 8f;

    public Dimension Width { get; init; }

    public override Element CreateElement() => new ProgressBarElement(this);
}

/// <summary>The element that holds a <see cref="ProgressBar"/>. Reads <see cref="ProgressBar.ValueGetter"/>/<see cref="ProgressBar.Value"/> and draws the track plus fill (using the skin if specified, otherwise falling back to color).</summary>
internal sealed class ProgressBarElement : Element
{
    private readonly LayoutNode _node;

    public ProgressBarElement(ProgressBar widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private ProgressBar W => (ProgressBar)Widget;

    public override LayoutNode LayoutNode => _node;

    public override void Paint(in PaintContext context)
    {
        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;
        float v = Math.Clamp(W.ValueGetter?.Invoke() ?? W.Value, 0f, 1f);
        float radius = W.Height / 2f;

        if (W.TrackSkin.HasValue)
        {
            W.TrackSkin.Paint(context, b);
        }
        else
        {
            context.FillRoundedRect(b, W.Track ?? theme.SurfaceVariant, radius);
        }

        if (v <= 0f)
        {
            return;
        }

        var fill = new Rect(b.X, b.Y, b.Width * v, b.Height);
        if (W.FillSkin.HasValue)
        {
            W.FillSkin.Paint(context, fill);
        }
        else
        {
            context.FillRoundedRect(fill, W.Fill ?? theme.Primary, radius);
        }
    }

    private Size MeasureSelf(BoxConstraints constraints)
    {
        float? fixedW = W.Width.Resolve(constraints.MaxWidth);
        float width = fixedW ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : 200f);
        return new Size(width, W.Height);
    }
}

/// <summary>Common base for focusable toggle controls: handles registration with the <see cref="Focus"/> system, toggling via tap/OK, and hover tracking.</summary>
public abstract class ToggleControlElement : Element, IHoverTarget
{
    private bool _hovered;

    protected ToggleControlElement(Widget widget)
        : base(widget)
    {
        Node = new LayoutNode(measure: Measure);
    }

    private WidgetState _toggleState = WidgetState.None;

    protected LayoutNode Node { get; }

    /// <summary>Whether the mouse is hovering (used by derived classes to draw state layers).</summary>
    protected bool Hovered => _hovered;

    /// <summary>Whether the control is selected (on). Reflected in <see cref="WidgetState.Selected"/>.</summary>
    protected virtual bool IsSelected => false;

    /// <summary>Destination for state transition (hover/focus/selected) notifications (the derived widget's <c>OnStateChanged</c>).</summary>
    protected virtual Action<WidgetState>? StateChangedCallback => null;

    public override LayoutNode LayoutNode => Node;

    public override bool WantsPointer => true;

    internal sealed override FocusNode? FocusNodeOrNull => FocusNodeOf();

    bool IHoverTarget.HoverOpaque => true;

    MouseCursor IHoverTarget.HoverCursor => MouseCursor.Click;

    void IHoverTarget.HoverEnter(Vec2 position) => SetHovered(true);

    void IHoverTarget.HoverMove(Vec2 position)
    {
    }

    void IHoverTarget.HoverExit(Vec2 position) => SetHovered(false);

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        FocusNode node = FocusNodeOf();
        node.OnActivate = OnActivate;
        node.OnFocusChange = _ => NotifyStateChanged();
        node.SemanticLabel = SemanticLabel;
        if (context.Focusable)
        {
            context.Focus?.Register(node, () => LayoutNode.Bounds, EnclosingScope(), EnclosingScrollable(), context.Traversable);
            if (Autofocus)
            {
                context.Focus?.AutofocusIfNone(node);
            }
        }

        OnMounted(context);
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        NotifyStateChanged(); // Value/選択の変化を反映
    }

    public override void Unmount()
    {
        Context.Focus?.Unregister(FocusNodeOf());
        (Context.Owner as HamonRoot)?.NotifyHoverTargetUnmounted(this);
        base.Unmount();
    }

    /// <summary>Overlays a thin state layer on the rectangle while hovering (shared drawing logic called at the end of a derived class's Paint).</summary>
    protected void PaintHoverLayer(in PaintContext context, Rect bounds, float radius)
    {
        if (_hovered)
        {
            HamonTheme theme = Context.Theme;
            context.FillRoundedRect(bounds, new Color(theme.Primary.R, theme.Primary.G, theme.Primary.B, 28), radius);
        }
    }

    private void SetHovered(bool value)
    {
        if (_hovered != value)
        {
            _hovered = value;
            Context.Owner?.MarkElementDirty(this);
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        WidgetState s = WidgetState.None;
        if (_hovered)
        {
            s |= WidgetState.Hovered;
        }

        if (FocusNodeOf().HasFocus)
        {
            s |= WidgetState.Focused;
        }

        if (IsSelected)
        {
            s |= WidgetState.Selected;
        }

        if (s != _toggleState)
        {
            _toggleState = s;
            StateChangedCallback?.Invoke(s);
        }
    }

    public override void HandlePointer(in PointerEvent pointer)
    {
        if (pointer.Phase == PointerPhase.Up && LayoutNode.Bounds.Contains(pointer.Position.X, pointer.Position.Y))
        {
            OnActivate();
        }
    }

    protected abstract FocusNode FocusNodeOf();

    protected abstract bool Autofocus { get; }

    /// <summary>Accessibility label for derived widgets (their <c>SemanticLabel</c>).</summary>
    protected virtual string? SemanticLabel => null;

    protected abstract void OnActivate();

    protected abstract Size Measure(BoxConstraints constraints);

    protected virtual void OnMounted(BuildContext context)
    {
    }
}

/// <summary>A checkbox (equivalent to Flutter's <c>Checkbox</c>). Activating it invokes <see cref="OnChanged"/> with the negated <see cref="Value"/>.</summary>
public sealed class Checkbox : Widget
{
    public bool Value { get; init; }

    public Action<bool>? OnChanged { get; init; }

    public FocusNode Node { get; init; } = new();

    public bool Autofocus { get; init; }

    public float Size { get; init; } = 24f;

    public string? SemanticLabel { get; init; }

    public Color? Border { get; init; }

    public Color? CheckColor { get; init; }

    /// <summary>Image skin (9-slice/sprite) shown when checked.</summary>
    public ImageSkin OnSkin { get; init; }

    /// <summary>Image skin shown when unchecked.</summary>
    public ImageSkin OffSkin { get; init; }

    /// <summary>Notification of hover/focus/selected transitions (for custom animations/sound effects).</summary>
    public Action<WidgetState>? OnStateChanged { get; init; }

    public override Element CreateElement() => new CheckboxElement(this);
}

/// <summary>The element that holds a <see cref="Checkbox"/>.</summary>
internal sealed class CheckboxElement : ToggleControlElement
{
    public CheckboxElement(Checkbox widget)
        : base(widget)
    {
    }

    private Checkbox W => (Checkbox)Widget;

    protected override FocusNode FocusNodeOf() => W.Node;

    protected override bool Autofocus => W.Autofocus;

    protected override bool IsSelected => W.Value;

    protected override Action<WidgetState>? StateChangedCallback => W.OnStateChanged;

    protected override string? SemanticLabel => W.SemanticLabel;

    protected override void OnActivate() => W.OnChanged?.Invoke(!W.Value);

    protected override Size Measure(BoxConstraints constraints) => new(W.Size, W.Size);

    public override void Paint(in PaintContext context)
    {
        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;
        PaintHoverLayer(context, b, b.Width * 0.2f);

        ImageSkin skin = W.Value ? W.OnSkin : W.OffSkin;
        if (skin.HasValue)
        {
            skin.Paint(context, b);
            return;
        }

        context.DrawOutline(b, W.Border ?? theme.Border, 2f);
        if (W.Value)
        {
            float inset = b.Width * 0.25f;
            context.FillRect(new Rect(b.X + inset, b.Y + inset, b.Width - (2f * inset), b.Height - (2f * inset)), W.CheckColor ?? theme.Primary);
        }
    }
}

/// <summary>An on/off switch (equivalent to Flutter's <c>Switch</c>). The knob slides using an <see cref="AnimationController"/>.</summary>
public sealed class Switch : Widget
{
    public bool Value { get; init; }

    public Action<bool>? OnChanged { get; init; }

    public FocusNode Node { get; init; } = new();

    public bool Autofocus { get; init; }

    public string? SemanticLabel { get; init; }

    public Color? TrackOff { get; init; }

    public Color? TrackOn { get; init; }

    public Color? Knob { get; init; }

    /// <summary>Image skin (9-slice/sprite) for the track (base).</summary>
    public ImageSkin TrackSkin { get; init; }

    /// <summary>Image skin for the knob.</summary>
    public ImageSkin KnobSkin { get; init; }

    /// <summary>Notification of hover/focus/selected transitions (for custom animations/sound effects).</summary>
    public Action<WidgetState>? OnStateChanged { get; init; }

    public override Element CreateElement() => new SwitchElement(this);
}

/// <summary>The element that holds a <see cref="Switch"/>.</summary>
internal sealed class SwitchElement : ToggleControlElement
{
    private const float TrackWidth = 48f;
    private const float TrackHeight = 26f;
    private const float KnobSize = 20f;
    private const float Pad = 3f;

    private AnimationController? _knob;

    public SwitchElement(Switch widget)
        : base(widget)
    {
    }

    private Switch W => (Switch)Widget;

    protected override FocusNode FocusNodeOf() => W.Node;

    protected override bool Autofocus => W.Autofocus;

    protected override bool IsSelected => W.Value;

    protected override Action<WidgetState>? StateChangedCallback => W.OnStateChanged;

    protected override string? SemanticLabel => W.SemanticLabel;

    protected override void OnActivate() => W.OnChanged?.Invoke(!W.Value);

    protected override Size Measure(BoxConstraints constraints) => new(TrackWidth, TrackHeight);

    protected override void OnMounted(BuildContext context)
    {
        if (context.Owner is { } owner)
        {
            _knob = owner.CreateAnimation(0.16f, Curves.EaseOut);
            _knob.JumpTo(W.Value ? 1f : 0f);
        }
    }

    public override void Update(Widget newWidget)
    {
        bool was = W.Value;
        base.Update(newWidget);
        if (W.Value != was && _knob is not null)
        {
            if (W.Value)
            {
                _knob.Forward();
            }
            else
            {
                _knob.Reverse();
            }
        }
    }

    public override void Unmount()
    {
        _knob?.Stop(); // アニメ中に外された場合のティッカー漏れを防ぐ
        base.Unmount();
    }

    public override void Paint(in PaintContext context)
    {
        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;
        float t = _knob?.Value ?? (W.Value ? 1f : 0f);
        if (W.TrackSkin.HasValue)
        {
            W.TrackSkin.Paint(context, b);
        }
        else
        {
            context.FillRoundedRect(b, Color.Lerp(W.TrackOff ?? theme.SurfaceVariant, W.TrackOn ?? theme.Primary, t), b.Height / 2f);
        }

        PaintHoverLayer(context, b, b.Height / 2f);

        float travel = b.Width - KnobSize - (2f * Pad);
        float knobX = b.X + Pad + (travel * t);
        var knob = new Rect(knobX, b.Y + Pad, KnobSize, b.Height - (2f * Pad));
        if (W.KnobSkin.HasValue)
        {
            W.KnobSkin.Paint(context, knob);
        }
        else
        {
            context.FillRoundedRect(knob, W.Knob ?? theme.OnSurface, KnobSize / 2f);
        }
    }
}

/// <summary>
/// A continuous value slider over 0..1 (equivalent to Flutter's <c>Slider</c>).
/// Gamepad D-pad left/right (dispatched via <c>DispatchButtonDown</c>) adjusts the value in <see cref="Step"/> increments.
/// </summary>
public sealed class Slider : Widget
{
    public float Value { get; init; }

    public Action<float>? OnChanged { get; init; }

    public FocusNode Node { get; init; } = new();

    public bool Autofocus { get; init; }

    public Dimension Width { get; init; }

    public float Step { get; init; } = 0.1f;

    public string? SemanticLabel { get; init; }

    public Color? Track { get; init; }

    public Color? Fill { get; init; }

    public Color? Thumb { get; init; }

    /// <summary>Image skin (9-slice/sprite) for the rail (the entire track).</summary>
    public ImageSkin TrackSkin { get; init; }

    /// <summary>Image skin for the fill (drawn from the left up to the current value).</summary>
    public ImageSkin FillSkin { get; init; }

    /// <summary>Image skin for the thumb.</summary>
    public ImageSkin ThumbSkin { get; init; }

    /// <summary>Notification of hover/focus/pressed (while dragging) transitions (for custom animations/sound effects, such as enlarging the thumb when it's grabbed).</summary>
    public Action<WidgetState>? OnStateChanged { get; init; }

    public override Element CreateElement() => new SliderElement(this);
}

/// <summary>The element that holds a <see cref="Slider"/>.</summary>
internal sealed class SliderElement : Element, IHoverTarget
{
    private const float Height = 28f;
    private const float ThumbSize = 16f;
    private const float TrackThickness = 4f;

    private readonly LayoutNode _node;
    private bool _hovered;
    private bool _dragging;
    private WidgetState _lastState = WidgetState.None;

    public SliderElement(Slider widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private Slider W => (Slider)Widget;

    public override LayoutNode LayoutNode => _node;

    public override bool WantsPointer => true;

    internal override FocusNode? FocusNodeOrNull => W.Node;

    bool IHoverTarget.HoverOpaque => true;

    MouseCursor IHoverTarget.HoverCursor => MouseCursor.Click;

    void IHoverTarget.HoverEnter(Vec2 position) => SetHovered(true);

    void IHoverTarget.HoverMove(Vec2 position)
    {
    }

    void IHoverTarget.HoverExit(Vec2 position) => SetHovered(false);

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        W.Node.OnButtonDown = OnButton;
        W.Node.OnFocusChange = _ => NotifyStateChanged();
        W.Node.SemanticLabel = W.SemanticLabel;
        if (context.Focusable)
        {
            context.Focus?.Register(W.Node, () => LayoutNode.Bounds, EnclosingScope(), EnclosingScrollable(), context.Traversable);
            if (W.Autofocus)
            {
                context.Focus?.AutofocusIfNone(W.Node);
            }
        }
    }

    public override void Unmount()
    {
        Context.Focus?.Unregister(W.Node);
        (Context.Owner as HamonRoot)?.NotifyHoverTargetUnmounted(this);
        base.Unmount();
    }

    public override void HandlePointer(in PointerEvent pointer)
    {
        switch (pointer.Phase)
        {
            case PointerPhase.Down:
                _dragging = true;
                SetFromX(pointer.Position.X);
                NotifyStateChanged();
                break;

            case PointerPhase.Move:
                SetFromX(pointer.Position.X);
                break;

            case PointerPhase.Up:
            case PointerPhase.Cancel:
                if (_dragging)
                {
                    _dragging = false;
                    NotifyStateChanged();
                }

                break;
        }
    }

    private void SetHovered(bool value)
    {
        if (_hovered != value)
        {
            _hovered = value;
            Context.Owner?.MarkElementDirty(this);
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        WidgetState s = WidgetState.None;
        if (_hovered)
        {
            s |= WidgetState.Hovered;
        }

        if (W.Node.HasFocus)
        {
            s |= WidgetState.Focused;
        }

        if (_dragging)
        {
            s |= WidgetState.Pressed;
        }

        if (s != _lastState)
        {
            _lastState = s;
            W.OnStateChanged?.Invoke(s);
        }
    }

    public override void Paint(in PaintContext context)
    {
        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;
        float value = Math.Clamp(W.Value, 0f, 1f);
        float trackY = b.Y + ((b.Height - TrackThickness) / 2f);
        float usable = Math.Max(0f, b.Width - ThumbSize);
        float thumbX = b.X + (usable * value);
        float center = thumbX + (ThumbSize / 2f);

        var trackRect = new Rect(b.X, trackY, b.Width, TrackThickness);
        if (W.TrackSkin.HasValue)
        {
            W.TrackSkin.Paint(context, trackRect);
        }
        else
        {
            context.FillRoundedRect(trackRect, W.Track ?? theme.SurfaceVariant, TrackThickness / 2f);
        }

        var fillRect = new Rect(b.X, trackY, center - b.X, TrackThickness);
        if (W.FillSkin.HasValue)
        {
            W.FillSkin.Paint(context, fillRect);
        }
        else
        {
            context.FillRoundedRect(fillRect, W.Fill ?? theme.Primary, TrackThickness / 2f);
        }

        // hover 中はつまみの周りに薄いハロー（ステートレイヤー）。
        if (_hovered)
        {
            const float halo = 10f;
            float haloX = thumbX - (halo / 2f);
            float haloY = b.Y + ((b.Height - ThumbSize - halo) / 2f);
            context.FillRoundedRect(new Rect(haloX, haloY, ThumbSize + halo, ThumbSize + halo), new Color(theme.Primary.R, theme.Primary.G, theme.Primary.B, 36), (ThumbSize + halo) / 2f);
        }

        var thumbRect = new Rect(thumbX, b.Y + ((b.Height - ThumbSize) / 2f), ThumbSize, ThumbSize);
        if (W.ThumbSkin.HasValue)
        {
            W.ThumbSkin.Paint(context, thumbRect);
        }
        else
        {
            context.FillRoundedRect(thumbRect, W.Thumb ?? theme.OnSurface, ThumbSize / 2f);
        }
    }

    private void OnButton(GamepadButton button)
    {
        if (button == GamepadButton.DpadLeft)
        {
            Set(W.Value - W.Step);
        }
        else if (button == GamepadButton.DpadRight)
        {
            Set(W.Value + W.Step);
        }
    }

    private void SetFromX(float x)
    {
        Rect b = LayoutNode.Bounds;
        float usable = Math.Max(1f, b.Width - ThumbSize);
        Set((x - b.X - (ThumbSize / 2f)) / usable);
    }

    private void Set(float value) => W.OnChanged?.Invoke(Math.Clamp(value, 0f, 1f));

    private Size MeasureSelf(BoxConstraints constraints)
    {
        float? fixedW = W.Width.Resolve(constraints.MaxWidth);
        float width = fixedW ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : 200f);
        return new Size(width, Height);
    }
}
