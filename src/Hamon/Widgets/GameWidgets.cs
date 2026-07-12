using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Overlays a small badge (item count/notification dot) on the top-right of the child (equivalent to
/// Flutter's <c>Badge</c>).
/// </summary>
public sealed class Badge : StatelessWidget
{
    public Widget? Child { get; init; }

    /// <summary>Label inside the badge (empty = dot badge).</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Should the badge be displayed?</summary>
    public bool Show { get; init; } = true;

    public Color? Color { get; init; }

    public Color? TextColor { get; init; }

    public float FontSize { get; init; } = 10f;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        if (!Show)
        {
            return Child ?? new SizedBox();
        }

        Widget badge = Label.Length == 0
            ? new Container { Width = Dimension.Px(8f), Height = Dimension.Px(8f), Radius = 4f, Color = Color ?? theme.Danger }
            : new Container
            {
                Color = Color ?? theme.Danger,
                Radius = 8f,
                Padding = EdgeInsets.Symmetric(5f, 1f),
                Child = new Text(Label) { FontSize = FontSize, Color = TextColor ?? theme.OnPrimary },
            };

        return new Stack
        {
            Children = new Widget[]
            {
                Child ?? new SizedBox(),
                new Positioned { Right = Dimension.Px(-4f), Top = Dimension.Px(-4f), Child = badge },
            },
        };
    }
}

/// <summary>
/// Action button with cooldown (for action bars). Disabled until <see cref="Progress"/> (0..1, 1 = available)
/// reaches full; the remaining cooldown is shown as a blackout curtain that shrinks from the top. An example
/// built on <see cref="FocusableActionDetector"/>/<see cref="Button"/>.
/// </summary>
public sealed class CooldownButton : StatelessWidget
{
    public Widget? Child { get; init; }

    /// <summary>Cooldown progress (0 = immediately after start, 1 = available). Used only when <see cref="ProgressGetter"/> is not specified.</summary>
    public float Progress { get; init; } = 1f;

    /// <summary>
    /// Cooldown progress, <b>read every frame at paint time</b> (analogous to <see cref="ProgressBar.ValueGetter"/>).
    /// This button is not rebuilt as the cooldown progresses; pressing only succeeds when <c>getter() &gt;= 1</c>
    /// holds true. Intended for action bars that want to avoid putting frequently changing cooldowns into
    /// atoms/state and rebuilding every frame.
    /// </summary>
    public Func<float>? ProgressGetter { get; init; }

    public Action? OnPressed { get; init; }

    public FocusNode? Node { get; init; }

    public float Size { get; init; } = 56f;

    public Color? Background { get; init; }

    public Color? OverlayColor { get; init; }

    /// <summary>Notifications of hover/pressed/focused transitions (for driving sound effects/custom animation); delegated to <see cref="Button.OnStateChanged"/>.</summary>
    public Action<WidgetState>? OnStateChanged { get; init; }

    /// <summary>Frame image skin (9-slice/sprite). </summary>
    public ImageSkin FrameSkin { get; init; }

    public float Radius { get; init; } = 10f;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        Color coverColor = OverlayColor ?? new Color(0, 0, 0, 150);

        var children = new System.Collections.Generic.List<Widget>(2)
        {
            new Align { Alignment = Alignment.Center, Child = Child ?? new SizedBox() },
        };

        Action? onPressed;
        bool enabled;
        if (ProgressGetter is { } getter)
        {
            // 描画時読み：CD が進んでも再構築せず、毎フレーム getter を読んで暗幕を上から縮める。押下は ready のときだけ成立。
            children.Add(new Positioned
            {
                Left = Dimension.Px(0f),
                Right = Dimension.Px(0f),
                Top = Dimension.Px(0f),
                Bottom = Dimension.Px(0f),
                Child = new CooldownCover { Progress = getter, Color = coverColor, Radius = Radius },
            });
            enabled = true; // 構造上は常に有効。可否は描画時 getter で表現＋押下で gate（再構築不要）。
            onPressed = () =>
            {
                if (getter() >= 1f)
                {
                    OnPressed?.Invoke();
                }
            };
        }
        else
        {
            bool ready = Progress >= 1f;
            if (!ready)
            {
                // getter 版と暗幕の見た目を揃える（上隅角丸・下辺は直線）。静的版は構造的に無効化（enabled=ready）。
                float p = Progress;
                children.Add(new Positioned
                {
                    Left = Dimension.Px(0f),
                    Right = Dimension.Px(0f),
                    Top = Dimension.Px(0f),
                    Bottom = Dimension.Px(0f),
                    Child = new CooldownCover { Progress = () => p, Color = coverColor, Radius = Radius },
                });
            }

            enabled = ready;
            onPressed = OnPressed;
        }

        return new Button
        {
            Node = Node ?? new FocusNode(),
            Enabled = enabled,
            OnPressed = onPressed,
            OnStateChanged = OnStateChanged,
            Background = Background ?? theme.SurfaceVariant,
            Style = FrameSkin.HasValue ? new ButtonStyle { BackgroundImage = WidgetStateProperty<ImageSkin?>.All(FrameSkin) } : null,
            Radius = Radius,
            Padding = default,
            Child = new SizedBox
            {
                Width = Dimension.Px(Size),
                Height = Dimension.Px(Size),
                Child = new Stack { Children = children },
            },
        };
    }
}

/// <summary>
/// Cooldown blackout curtain, drawn <b>at paint time</b> from <see cref="CooldownButton.ProgressGetter"/>.
/// Paints the remaining amount (= 1 - progress) from the top; this widget is never rebuilt as progress
/// changes (zero reconciles).
/// </summary>
internal sealed class CooldownCover : Widget
{
    /// <summary>0..1 (1 = enabled); paints <c>1 - progress</c> from the top.</summary>
    public required Func<float> Progress { get; init; }

    public Color Color { get; init; }

    public float Radius { get; init; }

    public override Element CreateElement() => new CooldownCoverElement(this);
}

internal sealed class CooldownCoverElement : Element
{
    private readonly LayoutNode _node = new(new Style { Kind = LayoutKind.Stack, StackExpandChildren = true });

    public CooldownCoverElement(CooldownCover widget)
        : base(widget)
    {
    }

    public override LayoutNode LayoutNode => _node;

    public override void Paint(in PaintContext context)
    {
        var w = (CooldownCover)Widget;
        float remaining = 1f - Math.Clamp(w.Progress(), 0f, 1f);
        if (remaining <= 0f)
        {
            return;
        }

        Rect b = _node.Bounds;
        // スライドする下辺は<b>常に直線</b>にする：上隅だけボタン枠に合わせて角丸・下辺は矩形クリップで切る。
        // 角丸矩形を残量高+半径で描いて残量高でクリップ＝矩形自身の下隅角丸はクリップ線より下に押し出されて切り落とされ、
        // 残量がどれだけ高くても（下隅ゾーンに入っても）境界は水平直線になる。上の角丸はクリップ内に残る。
        // （角丸矩形をそのまま縮めると下隅角丸が一緒にスライドして不自然＝この退行を防ぐ）。
        float h = b.Height * remaining;
        object? clip = context.PushClip(new Rect(b.X, b.Y, b.Width, h));
        context.FillRoundedRect(new Rect(b.X, b.Y, b.Width, h + w.Radius), w.Color, w.Radius);
        context.PopClip(clip);
    }
}

/// <summary>
/// Inventory/action bar slot (a framed square with an arbitrary icon, quantity badge, selection highlight,
/// and focusability). An example built on <see cref="Button"/>.
/// </summary>
public sealed class SlotButton : StatelessWidget
{
    public Widget? Icon { get; init; }

    /// <summary>Quantity (badged if 2 or more).</summary>
    public int Count { get; init; }

    public bool Selected { get; init; }

    public Action? OnPressed { get; init; }

    public FocusNode? Node { get; init; }

    public float Size { get; init; } = 56f;

    public Color? Background { get; init; }

    public Color? SelectedColor { get; init; }

    /// <summary>Notifications of hover/pressed/focused transitions (for driving sound effects/custom animation); delegated to <see cref="Button.OnStateChanged"/>.</summary>
    public Action<WidgetState>? OnStateChanged { get; init; }

    /// <summary>Image skin for slot frame (9-slice/sprite). </summary>
    public ImageSkin FrameSkin { get; init; }

    /// <summary>Image skin of the slot frame when selected (falls back to <see cref="FrameSkin"/> if not set).</summary>
    public ImageSkin SelectedFrameSkin { get; init; }

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        Widget content = new Align { Alignment = Alignment.Center, Child = Icon ?? new SizedBox() };
        Widget inner = Count >= 2
            ? new Badge { Label = Count.ToString(), Color = theme.Surface, TextColor = theme.OnSurface, Child = content }
            : content;

        ImageSkin frame = Selected ? (SelectedFrameSkin.HasValue ? SelectedFrameSkin : FrameSkin) : FrameSkin;
        ButtonStyle? style = frame.HasValue ? new ButtonStyle { BackgroundImage = WidgetStateProperty<ImageSkin?>.All(frame) } : null;

        return new Button
        {
            Node = Node ?? new FocusNode(),
            OnPressed = OnPressed,
            OnStateChanged = OnStateChanged,
            Background = Selected ? (SelectedColor ?? theme.Primary) : (Background ?? theme.SurfaceVariant),
            Style = style,
            Radius = 8f,
            Padding = EdgeInsets.All(4f),
            Child = new SizedBox { Width = Dimension.Px(Size), Height = Dimension.Px(Size), Child = inner },
        };
    }
}
