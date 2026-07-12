using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Virtual stick on screen (for touch operation). Direction is delivered via <see cref="OnChanged"/>; when
/// released, if <see cref="AutoCenter"/> is set it returns to center and sends zero.
/// </summary>
public sealed class VirtualJoystick : Widget
{
    /// <summary>Normalized direction (-1..1); zero while centered via <see cref="AutoCenter"/>.</summary>
    public Action<Vec2>? OnChanged { get; init; }

    public float Size { get; init; } = 120f;

    public float KnobSize { get; init; } = 48f;

    public Color? Base { get; init; }

    public Color? Knob { get; init; }

    /// <summary>Base image skin (9-slice/sprite); overrides plain <see cref="Base"/> color drawing.</summary>
    public ImageSkin BaseSkin { get; init; }

    /// <summary>Knob image skin; overrides plain <see cref="Knob"/> color drawing.</summary>
    public ImageSkin KnobSkin { get; init; }

    /// <summary>Notification of grabbed/released (true = drag start, false = release) (custom animation/sound effects such as knob enlargement).</summary>
    public Action<bool>? OnActiveChanged { get; init; }

    /// <summary>Return to center when released (default true).</summary>
    public bool AutoCenter { get; init; } = true;

    public override Element CreateElement() => new VirtualJoystickElement(this);
}

/// <summary>The element backing <see cref="VirtualJoystick"/> (moves the knob by dragging and outputs the direction).</summary>
internal sealed class VirtualJoystickElement : Element
{
    private const int NoPointer = int.MinValue;

    private readonly LayoutNode _node;
    private Vec2 _knob; // 中心からのノブオフセット（px）
    private bool _active; // ドラッグ中か（OnActiveChanged の発火判定）
    private int _activeId = NoPointer; // 掴んでいる指の ID（他の指は無視＝ノブが横取りされない）

    public VirtualJoystickElement(VirtualJoystick widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private VirtualJoystick W => (VirtualJoystick)Widget;

    public override LayoutNode LayoutNode => _node;

    public override bool WantsPointer => true;

    /// <summary>Current normalized direction (for testing/inspection).</summary>
    internal Vec2 Direction
    {
        get
        {
            float maxR = MaxRadius;
            return maxR > 0f ? new Vec2(_knob.X / maxR, _knob.Y / maxR) : Vec2.Zero;
        }
    }

    private float MaxRadius => (W.Size - W.KnobSize) / 2f;

    public override void HandlePointer(in PointerEvent pointer)
    {
        Rect b = _node.Bounds;
        var center = new Vec2(b.X + (b.Width / 2f), b.Y + (b.Height / 2f));

        // 掴んでいる指以外（マルチタッチの別指）は無視する。未掴みなら Down で掴む。
        if (_activeId == NoPointer)
        {
            if (pointer.Phase != PointerPhase.Down)
            {
                return;
            }

            _activeId = pointer.PointerId;
        }
        else if (pointer.PointerId != _activeId)
        {
            return;
        }

        switch (pointer.Phase)
        {
            case PointerPhase.Down:
            case PointerPhase.Move:
                if (pointer.Phase == PointerPhase.Down && !_active)
                {
                    _active = true;
                    W.OnActiveChanged?.Invoke(true); // 掴んだ
                }

                float dx = pointer.Position.X - center.X;
                float dy = pointer.Position.Y - center.Y;
                float len = MathF.Sqrt((dx * dx) + (dy * dy));
                float maxR = MaxRadius;
                if (len > maxR && len > 0f)
                {
                    dx = dx / len * maxR;
                    dy = dy / len * maxR;
                }

                _knob = new Vec2(dx, dy);
                W.OnChanged?.Invoke(Direction);
                Context.Owner?.MarkElementDirty(this);
                break;

            case PointerPhase.Up:
            case PointerPhase.Cancel:
                if (_active)
                {
                    _active = false;
                    W.OnActiveChanged?.Invoke(false); // 離した
                }

                if (W.AutoCenter)
                {
                    _knob = Vec2.Zero;
                    W.OnChanged?.Invoke(Vec2.Zero);
                    Context.Owner?.MarkElementDirty(this);
                }

                _activeId = NoPointer;
                break;
        }
    }

    public override void Paint(in PaintContext context)
    {
        Rect b = _node.Bounds;
        HamonTheme theme = Context.Theme;
        if (W.BaseSkin.HasValue)
        {
            W.BaseSkin.Paint(context, b);
        }
        else
        {
            context.FillRoundedRect(b, W.Base ?? theme.SurfaceVariant, b.Width / 2f);
        }

        float kx = b.X + (b.Width / 2f) + _knob.X - (W.KnobSize / 2f);
        float ky = b.Y + (b.Height / 2f) + _knob.Y - (W.KnobSize / 2f);
        var knob = new Rect(kx, ky, W.KnobSize, W.KnobSize);
        if (W.KnobSkin.HasValue)
        {
            W.KnobSkin.Paint(context, knob);
        }
        else
        {
            context.FillRoundedRect(knob, W.Knob ?? theme.Primary, W.KnobSize / 2f);
        }
    }

    private Size MeasureSelf(BoxConstraints constraints) => new(W.Size, W.Size);
}

/// <summary>
/// On-screen D-pad (for touch operation). Delivers <see cref="OnPressed"/> on press and
/// <see cref="OnReleased"/> on release, determining up/down/left/right from the position relative to center.
/// </summary>
public sealed class Dpad : Widget
{
    public Action<FocusDirection>? OnPressed { get; init; }

    public Action<FocusDirection>? OnReleased { get; init; }

    public float Size { get; init; } = 120f;

    public Color? Background { get; init; }

    public Color? PressedColor { get; init; }

    /// <summary>Image skin for entire D-pad (9-slice/sprite). </summary>
    public ImageSkin BaseSkin { get; init; }

    /// <summary>Highlight image skin overlaid on the arm currently being pressed; used only when <see cref="BaseSkin"/> is specified.</summary>
    public ImageSkin PressedSkin { get; init; }

    /// <summary>Center dead zone radius (px - no reaction inside this).</summary>
    public float DeadZone { get; init; } = 12f;

    public override Element CreateElement() => new DpadElement(this);
}

/// <summary>The element backing <see cref="Dpad"/> (4-way segment hit detection plus press highlight).</summary>
internal sealed class DpadElement : Element
{
    private const int NoPointer = int.MinValue;

    private readonly LayoutNode _node;
    private FocusDirection? _pressed;
    private int _activeId = NoPointer; // 押している指の ID（他の指は無視）

    public DpadElement(Dpad widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private Dpad W => (Dpad)Widget;

    public override LayoutNode LayoutNode => _node;

    public override bool WantsPointer => true;

    /// <summary>Direction currently being pressed (for inspection/testing).</summary>
    internal FocusDirection? PressedDirection => _pressed;

    public override void HandlePointer(in PointerEvent pointer)
    {
        // 押している指以外（マルチタッチの別指）は無視する。未押下なら Down で掴む。
        if (_activeId == NoPointer)
        {
            if (pointer.Phase != PointerPhase.Down)
            {
                return;
            }

            _activeId = pointer.PointerId;
        }
        else if (pointer.PointerId != _activeId)
        {
            return;
        }

        switch (pointer.Phase)
        {
            case PointerPhase.Down:
                FocusDirection? dir = RegionOf(pointer.Position);
                if (dir is FocusDirection d)
                {
                    _pressed = d;
                    W.OnPressed?.Invoke(d);
                    Context.Owner?.MarkElementDirty(this);
                }

                break;

            case PointerPhase.Up:
            case PointerPhase.Cancel:
                if (_pressed is FocusDirection prev)
                {
                    W.OnReleased?.Invoke(prev);
                    _pressed = null;
                    Context.Owner?.MarkElementDirty(this);
                }

                _activeId = NoPointer;
                break;
        }
    }

    private FocusDirection? RegionOf(Vec2 position)
    {
        Rect b = _node.Bounds;
        float dx = position.X - (b.X + (b.Width / 2f));
        float dy = position.Y - (b.Y + (b.Height / 2f));
        if ((dx * dx) + (dy * dy) < W.DeadZone * W.DeadZone)
        {
            return null; // デッドゾーン
        }

        if (MathF.Abs(dx) > MathF.Abs(dy))
        {
            return dx > 0f ? FocusDirection.Right : FocusDirection.Left;
        }

        return dy > 0f ? FocusDirection.Down : FocusDirection.Up;
    }

    public override void Paint(in PaintContext context)
    {
        Rect b = _node.Bounds;
        HamonTheme theme = Context.Theme;

        // スキン指定時：全体スプライト＋押下腕にハイライトスプライト。
        if (W.BaseSkin.HasValue)
        {
            W.BaseSkin.Paint(context, b);
            if (_pressed is FocusDirection pd && W.PressedSkin.HasValue)
            {
                W.PressedSkin.Paint(context, ArmRect(b, pd));
            }

            return;
        }

        Color bg = W.Background ?? theme.SurfaceVariant;
        Color press = W.PressedColor ?? theme.Primary;
        DrawArm(context, ArmRect(b, FocusDirection.Up), FocusDirection.Up, bg, press);
        DrawArm(context, ArmRect(b, FocusDirection.Down), FocusDirection.Down, bg, press);
        DrawArm(context, ArmRect(b, FocusDirection.Left), FocusDirection.Left, bg, press);
        DrawArm(context, ArmRect(b, FocusDirection.Right), FocusDirection.Right, bg, press);
        float third = b.Width / 3f;
        context.FillRect(new Rect(b.X + third, b.Y + third, third, third), bg); // 中央
    }

    // 各方向の腕の矩形（3x3 グリッドの該当セル）。
    private static Rect ArmRect(Rect b, FocusDirection dir)
    {
        float third = b.Width / 3f;
        return dir switch
        {
            FocusDirection.Up => new Rect(b.X + third, b.Y, third, third),
            FocusDirection.Down => new Rect(b.X + third, b.Bottom - third, third, third),
            FocusDirection.Left => new Rect(b.X, b.Y + third, third, third),
            _ => new Rect(b.Right - third, b.Y + third, third, third),
        };
    }

    private void DrawArm(in PaintContext context, Rect rect, FocusDirection dir, Color bg, Color press)
    {
        context.FillRoundedRect(rect, _pressed == dir ? press : bg, 4f);
    }

    private Size MeasureSelf(BoxConstraints constraints) => new(W.Size, W.Size);
}
