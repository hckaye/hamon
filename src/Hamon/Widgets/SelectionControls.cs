using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A radio button (equivalent to Flutter's <c>Radio&lt;T&gt;</c>). It is selected when <see cref="Value"/> matches
/// <see cref="GroupValue"/>. Tapping or pressing OK invokes <see cref="OnChanged"/> with <see cref="Value"/>.
/// </summary>
public sealed class Radio<T> : Widget
{
    public T Value { get; init; } = default!;

    public T GroupValue { get; init; } = default!;

    public Action<T>? OnChanged { get; init; }

    public FocusNode Node { get; init; } = new();

    public bool Autofocus { get; init; }

    public float Size { get; init; } = 22f;

    public string? SemanticLabel { get; init; }

    public Color? Border { get; init; }

    public Color? DotColor { get; init; }

    /// <summary>Color of the ring's interior (the "hole").</summary>
    public Color? FillColor { get; init; }

    /// <summary>Image skin (9-slice/sprite) when selected.</summary>
    public ImageSkin OnSkin { get; init; }

    /// <summary>Image skin when not selected.</summary>
    public ImageSkin OffSkin { get; init; }

    /// <summary>Notifications for hover/focus/selected transitions (custom animations/sound effects).</summary>
    public Action<WidgetState>? OnStateChanged { get; init; }

    public override Element CreateElement() => new RadioElement<T>(this);
}

/// <summary>The element that backs <see cref="Radio{T}"/> (ring + selection dot).</summary>
internal sealed class RadioElement<T> : ToggleControlElement
{
    public RadioElement(Radio<T> widget)
        : base(widget)
    {
    }

    private Radio<T> W => (Radio<T>)Widget;

    private bool Selected => EqualityComparer<T>.Default.Equals(W.Value, W.GroupValue);

    protected override FocusNode FocusNodeOf() => W.Node;

    protected override bool Autofocus => W.Autofocus;

    protected override bool IsSelected => Selected;

    protected override Action<WidgetState>? StateChangedCallback => W.OnStateChanged;

    protected override string? SemanticLabel => W.SemanticLabel;

    protected override void OnActivate() => W.OnChanged?.Invoke(W.Value);

    protected override Size Measure(BoxConstraints constraints) => new(W.Size, W.Size);

    public override void Paint(in PaintContext context)
    {
        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;
        float r = b.Width / 2f;

        ImageSkin skin = Selected ? W.OnSkin : W.OffSkin;
        if (skin.HasValue)
        {
            skin.Paint(context, b);
            PaintHoverLayer(context, b, r);
            return;
        }

        context.FillRoundedRect(b, W.Border ?? theme.Border, r); // 外周リング（塗りつぶし円）

        const float ringWidth = 2f;
        var inner = new Rect(b.X + ringWidth, b.Y + ringWidth, b.Width - (2f * ringWidth), b.Height - (2f * ringWidth));
        context.FillRoundedRect(inner, W.FillColor ?? theme.Surface, Math.Max(0f, inner.Width / 2f)); // 穴

        if (Selected)
        {
            float di = b.Width * 0.3f;
            var dot = new Rect(b.X + di, b.Y + di, b.Width - (2f * di), b.Height - (2f * di));
            context.FillRoundedRect(dot, W.DotColor ?? theme.Primary, Math.Max(0f, dot.Width / 2f));
        }

        PaintHoverLayer(context, b, r); // hover の薄いステートレイヤー（最前面に淡く）
    }
}

/// <summary>One segment (value and label).</summary>
public sealed class SegmentItem<T>
{
    public SegmentItem(T value, Widget label)
    {
        Value = value;
        Label = label;
    }

    public T Value { get; }

    public Widget Label { get; }
}

/// <summary>
/// A single-selection segmented control (equivalent to Flutter/Cupertino's <c>SegmentedControl</c> / Material's
/// <c>SegmentedButton</c>). Lets you choose one of several horizontal segments. Built on top of <see cref="Button"/>.
/// </summary>
public sealed class SegmentedControl<T> : StatelessWidget
{
    public IReadOnlyList<SegmentItem<T>> Segments { get; init; } = Array.Empty<SegmentItem<T>>();

    public T Value { get; init; } = default!;

    public Action<T>? OnChanged { get; init; }

    /// <summary>Background of the selected segment (defaults to theme Primary if unspecified).</summary>
    public Color? SelectedColor { get; init; }

    /// <summary>Outer border/separator color (defaults to theme Border if unspecified).</summary>
    public Color? BorderColor { get; init; }

    public float Radius { get; init; } = 10f;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        Color selected = SelectedColor ?? theme.Primary;

        var children = new Widget[Segments.Count];
        for (int i = 0; i < Segments.Count; i++)
        {
            SegmentItem<T> seg = Segments[i];
            bool isSelected = EqualityComparer<T>.Default.Equals(seg.Value, Value);
            T value = seg.Value;
            children[i] = new Button
            {
                Background = isSelected ? selected : new Color(0, 0, 0, 0),
                Radius = Math.Max(0f, Radius - 2f),
                Padding = EdgeInsets.Symmetric(14f, 8f),
                OnPressed = () => OnChanged?.Invoke(value),
                Child = seg.Label,
            };
        }

        return new Container
        {
            Radius = Radius,
            Color = theme.SurfaceVariant,
            Padding = EdgeInsets.All(2f),
            Child = new Row { Spacing = 2f, Children = children },
        };
    }
}
