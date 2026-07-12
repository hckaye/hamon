using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>A single tab (label and body builder).</summary>
public sealed class TabItem
{
    public TabItem(Widget label, Func<Widget> content)
    {
        Label = label;
        Content = content;
    }

    public Widget Label { get; }

    public Func<Widget> Content { get; }
}

/// <summary>
/// Holds tab selection state and animates switching (equivalent to Flutter's <c>TabController</c>). Pass the same
/// instance to <see cref="Tabs"/> (do not let a rebuild recreate it). <see cref="Fraction"/> is the underline
/// indicator's continuous position (interpolated from the old index to the new one while switching), and
/// <see cref="BodyFade"/> is the fade progress of the main body.
/// </summary>
public sealed class TabController
{
    private readonly float _duration;
    private readonly Curve? _curve;
    private IHamonHost? _host;          // 再描画通知の宛先。Tabs のビルド時に Attach で接続。
    private AnimationController? _anim; // 切替アニメ。Attach で遅延生成（Flutter の vsync 相当をホストが供給）。
    private float _fromFraction;

    public TabController(int count, int initial = 0, float duration = 0.2f, Curve? curve = null)
    {
        Count = Math.Max(1, count);
        Index = Math.Clamp(initial, 0, Count - 1);
        _fromFraction = Index;
        _duration = duration;
        _curve = curve;
    }

    /// <summary>Connects to the host and prepares the switching animation. Called by <see cref="Tabs"/> at build time (idempotent).</summary>
    internal void Attach(IHamonHost host)
    {
        if (_host is not null)
        {
            return;
        }

        _host = host;
        _anim = host.CreateAnimation(_duration, _curve ?? Curves.EaseOut);
        _anim.JumpTo(1f); // 初期は落ち着いた状態
    }

    public int Count { get; }

    public int Index { get; private set; }

    /// <summary>Index of the tab currently under mouse hover (-1 = none).</summary>
    public int HoveredIndex { get; private set; } = -1;

    /// <summary>Sets the hovered tab index (called from each tab's <see cref="MouseRegion"/>).</summary>
    public void SetHovered(int index)
    {
        if (index != HoveredIndex)
        {
            HoveredIndex = index;
            _host?.MarkDirty();
        }
    }

    /// <summary>Continuous position of the underline indicator (interpolates from the old index to the new one while switching).</summary>
    public float Fraction => _anim is null ? Index : _fromFraction + ((Index - _fromFraction) * _anim.Curved);

    /// <summary>Fade progress of the main body content (0 to 1 while switching).</summary>
    public float BodyFade => _anim?.Curved ?? 1f;

    public void Select(int index)
    {
        index = Math.Clamp(index, 0, Count - 1);
        if (index == Index)
        {
            return;
        }

        _fromFraction = Fraction; // 途中切替でも滑らかに
        Index = index;
        _anim?.JumpTo(0f);
        _anim?.Forward();
        _host?.MarkDirty();
    }
}

/// <summary>
/// Tab UI (equivalent to Flutter's <c>TabBar</c> + <c>TabBarView</c>). Renders a row of tab buttons above the main
/// body; each tab's underline crossfades according to the controller's continuous <see cref="TabController.Fraction"/>,
/// and the body fades in when switching. Because the indicator's position is expressed via each tab underline's
/// opacity rather than by moving a single indicator with <see cref="Transform"/>, no layout feedback pass is
/// needed after measurement.
/// </summary>
public sealed class Tabs : StatelessWidget
{
    public TabController Controller { get; init; } = null!;

    public IReadOnlyList<TabItem> Items { get; init; } = Array.Empty<TabItem>();

    public Color IndicatorColor { get; init; } = new(120, 180, 255);

    public float Spacing { get; init; } = 8f;

    public override Widget Build(BuildContext context)
    {
        TabController controller = Controller
            ?? throw new InvalidOperationException("Tabs requires a Controller.");

        if (context.Owner is { } owner)
        {
            controller.Attach(owner); // ホスト接続＋切替アニメ生成（冪等）。コンストラクタに host を要求しない。
        }

        var buttons = new Widget[Items.Count];
        for (int i = 0; i < Items.Count; i++)
        {
            buttons[i] = BuildTab(controller, i);
        }

        return new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new Row { Spacing = Spacing, Children = buttons },
                BuildBody(controller),
            },
        };
    }

    private Widget BuildTab(TabController controller, int index)
    {
        int i = index;
        bool hovered = controller.HoveredIndex == i;
        var column = new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Stretch, // 下線をラベル幅いっぱいに
            Spacing = 6f,
            Children = new Widget[]
            {
                new Padding { Insets = EdgeInsets.Symmetric(14f, 8f), Child = Items[i].Label },
                new Opacity
                {
                    ValueGetter = () => Math.Clamp(1f - Math.Abs(controller.Fraction - i), 0f, 1f),
                    Child = new Container { Height = Dimension.Px(3f), Color = IndicatorColor },
                },
            },
        };

        return new MouseRegion
        {
            Cursor = MouseCursor.Click,
            OnEnter = _ => controller.SetHovered(i),
            OnExit = _ => controller.SetHovered(-1),
            Child = new GestureDetector
            {
                OnTap = () => controller.Select(i),
                // hover 中は薄いハイライト背景（インジケータ色を低不透明度で）。
                Child = hovered
                    ? new Container { Radius = 6f, Color = new Color(IndicatorColor.R, IndicatorColor.G, IndicatorColor.B, 28), Child = column }
                    : column,
            },
        };
    }

    private Widget BuildBody(TabController controller) => new Opacity
    {
        Key = controller.Index, // index が変われば本体を作り直してフェードイン
        ValueGetter = () => controller.BodyFade,
        Child = Items[controller.Index].Content(),
    };
}
