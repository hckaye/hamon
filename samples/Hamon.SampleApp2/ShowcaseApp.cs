using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.SampleApp2;

/// <summary>
/// The core of the showcase. <see cref="Scaffold"/>put on home<see cref="NavigatorController"/>。
/// From gallery to details<see cref="Hero"/>Push on transition. <see cref="RotationClock"/>) to host.
/// </summary>
public sealed class Showcase
{
    private readonly HamonRoot _host;
    private readonly NavigatorController _nav;
    private readonly RotationClock _clock = new();

    public Showcase(HamonRoot host)
    {
        _host = host;
        _host.RegisterTicker(_clock);
        _nav = new NavigatorController(host, () => new ShowcaseHome(host, _clock, OpenDetail)) { TransitionDuration = 0.3f, Curve = Curves.FastOutSlowIn };
    }

    public Widget Root => new Navigator { Controller = _nav };

    public NavigatorController Nav => _nav;

    private void OpenDetail(int index, Color color) =>
        _nav.Push(() => new GalleryDetail(index, color, _nav));
}

/// <summary>A clock that advances the angle every frame (for rotation demonstration).<see cref="Transform.RotationGetter"/>(read from).</summary>
public sealed class RotationClock : ITicker
{
    public float Angle { get; private set; }

    public bool Tick(float dtSeconds)
    {
        Angle += dtSeconds * 1.1f;
        return true;
    }
}

internal static class Palette
{
    public static readonly Color[] Tiles =
    {
        new(96, 165, 250), new(167, 139, 250), new(244, 114, 182),
        new(52, 211, 153), new(251, 191, 36), new(248, 113, 113),
    };

    public static readonly Color GradA = new(99, 102, 241);
    public static readonly Color GradB = new(168, 85, 247);
}

/// <summary>Tabbed home (Scaffold + bottom navigation). </summary>
public sealed class ShowcaseHome : HookWidget
{
    private readonly HamonRoot _host;
    private readonly RotationClock _clock;
    private readonly Action<int, Color> _openDetail;

    public ShowcaseHome(HamonRoot host, RotationClock clock, Action<int, Color> openDetail)
    {
        _host = host;
        _clock = clock;
        _openDetail = openDetail;
    }

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        HookState<int> tab = hooks.UseState(0);

        Widget body = tab.Value switch
        {
            1 => new ComponentsPage(_host),
            2 => new FormsPage(_host),
            3 => new GalleryPage(_clock, _openDetail),
            _ => new DashboardPage(_clock),
        };

        string[] titles = { "ダッシュボード", "コンポーネント", "フォーム", "ギャラリー" };

        return new Scaffold
        {
            AppBar = new AppBar
            {
                Title = titles[tab.Value],
                Actions = new Widget[]
                {
                    new Button
                    {
                        Node = new FocusNode(),
                        Background = new Color(0, 0, 0, 0),
                        Radius = theme.Radius,
                        Padding = EdgeInsets.Symmetric(12f, 8f),
                        OnPressed = () => _host.ShowToast("通知はありません"),
                        Child = new Text("🔔") { FontSize = 18f },
                    },
                },
            },
            Body = body,
            BottomNavigationBar = new NavigationBar
            {
                SelectedIndex = tab.Value,
                OnDestinationSelected = i => tab.Value = i,
                Destinations = new[]
                {
                    new NavigationDestination { Label = "ホーム", Icon = "🏠" },
                    new NavigationDestination { Label = "部品", Icon = "🎛" },
                    new NavigationDestination { Label = "入力", Icon = "📝" },
                    new NavigationDestination { Label = "画像", Icon = "🖼" },
                },
            },
        };
    }
}

/// <summary>Dashboard: Gradient hero cards, stats cards, line charts, gauges, spinners, and rotation icons.</summary>
public sealed class DashboardPage : HookWidget
{
    private readonly RotationClock _clock;

    public DashboardPage(RotationClock clock) => _clock = clock;

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        ScrollController scroll = hooks.UseMemo(() => new ScrollController());

        Widget hero = new GradientBox
        {
            From = Palette.GradA,
            To = Palette.GradB,
            Elevation = 4f,
            Child = new Container
            {
                Padding = EdgeInsets.All(theme.SpacingL),
                Child = new Row
                {
                    CrossAxisAlignment = CrossAxisAlignment.Center,
                    Children = new Widget[]
                    {
                        new Expanded
                        {
                            Child = new Column
                            {
                                CrossAxisAlignment = CrossAxisAlignment.Start,
                                MainAxisSize = MainAxisSize.Min,
                                Spacing = 6f,
                                Children = new Widget[]
                                {
                                    new Text("ようこそ Hamon へ") { FontSize = theme.TextHeadline, Color = new Color(255, 255, 255) },
                                    new Text("グラデ・影・回転・チャートのショーケース") { FontSize = theme.TextLabel, Color = new Color(235, 235, 255) },
                                },
                            },
                        },
                        new CircularProgressIndicator { Value = 0.72f, Diameter = 64f, StrokeWidth = 7f, BackgroundColor = new Color(255, 255, 255, 60), Color = new Color(255, 255, 255) },
                    },
                },
            },
        };

        Widget stats = new Row
        {
            Spacing = theme.SpacingM,
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            Children = new Widget[]
            {
                new Expanded { Child = StatCard(theme, "売上", "¥128k", new Color(52, 211, 153)) },
                new Expanded { Child = StatCard(theme, "訪問", "3,420", new Color(96, 165, 250)) },
                new Expanded { Child = StatCard(theme, "課題", "12", new Color(248, 113, 113)) },
            },
        };

        Widget chart = new Card
        {
            Elevation = 2f,
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Stretch,
                MainAxisSize = MainAxisSize.Min,
                Spacing = theme.SpacingS,
                Children = new Widget[]
                {
                    new Text("週次トレンド") { FontSize = theme.TextTitle, Color = theme.OnSurface },
                    new MiniChart { Data = new[] { 12f, 18f, 9f, 22f, 17f, 28f, 24f }, Line = theme.Primary },
                },
            },
        };

        Widget spinners = new Card
        {
            Elevation = 2f,
            Child = new Row
            {
                MainAxisAlignment = MainAxisAlignment.SpaceAround,
                CrossAxisAlignment = CrossAxisAlignment.Center,
                Children = new Widget[]
                {
                    LabeledBox(theme, "読込中", new CircularProgressIndicator { Diameter = 40f }),
                    LabeledBox(theme, "ゲージ", new CircularProgressIndicator { Value = 0.4f, Diameter = 48f }),
                    LabeledBox(theme, "回転", new Transform
                    {
                        RotationGetter = () => _clock.Angle,
                        Origin = Alignment.Center,
                        Child = new Material { Color = new Color(251, 191, 36), Radius = 8f, Child = new SizedBox { Width = Dimension.Px(40f), Height = Dimension.Px(40f) } },
                    }),
                },
            },
        };

        return new Scrollbar
        {
            Controller = scroll,
            Child = new ScrollView
            {
                Controller = scroll,
                Child = new Container
                {
                    Padding = EdgeInsets.All(theme.SpacingM),
                    Child = new Column
                    {
                        CrossAxisAlignment = CrossAxisAlignment.Stretch,
                        MainAxisSize = MainAxisSize.Min,
                        Spacing = theme.SpacingM,
                        Children = new Widget[] { hero, stats, chart, spinners },
                    },
                },
            },
        };
    }

    private static Widget StatCard(HamonTheme theme, string label, string value, Color accent) => new Card
    {
        Elevation = 2f,
        Padding = EdgeInsets.All(theme.SpacingM),
        Child = new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            MainAxisSize = MainAxisSize.Min,
            Spacing = 4f,
            Children = new Widget[]
            {
                new Text(value) { FontSize = theme.TextTitle, Color = accent },
                new Text(label) { FontSize = theme.TextCaption, Color = theme.OnSurfaceVariant },
            },
        },
    };

    private static Widget LabeledBox(HamonTheme theme, string label, Widget child) => new Column
    {
        MainAxisSize = MainAxisSize.Min,
        CrossAxisAlignment = CrossAxisAlignment.Center,
        Spacing = theme.SpacingS,
        Children = new Widget[] { child, new Text(label) { FontSize = theme.TextCaption, Color = theme.OnSurfaceVariant } },
    };
}

/// <summary>Components: Switch/Slider/Checkbox/ProgressBar/Gauge + Snackbar/Toast trigger.</summary>
public sealed class ComponentsPage : HookWidget
{
    private readonly HamonRoot _host;

    public ComponentsPage(HamonRoot host) => _host = host;

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        HookState<bool> on = hooks.UseState(true);
        HookState<float> level = hooks.UseState(0.5f);
        HookState<bool> agree = hooks.UseState(false);

        Widget controls = new Card
        {
            Elevation = 2f,
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Stretch,
                MainAxisSize = MainAxisSize.Min,
                Spacing = theme.SpacingM,
                Children = new Widget[]
                {
                    Labeled(theme, "通知を受け取る", new Switch { Value = on.Value, OnChanged = v => on.Value = v }),
                    Labeled(theme, "規約に同意", new Checkbox { Value = agree.Value, OnChanged = v => agree.Value = v }),
                    new Text($"音量 {(int)(level.Value * 100)}%") { FontSize = theme.TextLabel, Color = theme.OnSurfaceVariant },
                    new Slider { Value = level.Value, OnChanged = v => level.Value = v },
                    new ProgressBar { Value = level.Value, Height = 10f },
                },
            },
        };

        Widget gauges = new Card
        {
            Elevation = 2f,
            Child = new Row
            {
                MainAxisAlignment = MainAxisAlignment.SpaceAround,
                CrossAxisAlignment = CrossAxisAlignment.Center,
                Children = new Widget[]
                {
                    new CircularProgressIndicator { Value = level.Value, Diameter = 72f, StrokeWidth = 8f },
                    new CircularProgressIndicator { Diameter = 44f },
                },
            },
        };

        Widget actions = new Row
        {
            Spacing = theme.SpacingM,
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            Children = new Widget[]
            {
                new Expanded
                {
                    Child = new Button
                    {
                        Node = new FocusNode(),
                        Background = theme.Primary,
                        Radius = theme.Radius,
                        Padding = EdgeInsets.Symmetric(16f, 12f),
                        OnPressed = () => _host.ShowSnackbar("保存しました", actionLabel: "取消", onAction: () => _host.ShowToast("取り消しました")),
                        Child = new Text("Snackbar") { FontSize = theme.TextLabel, Color = theme.OnPrimary },
                    },
                },
                new Expanded
                {
                    Child = new Button
                    {
                        Node = new FocusNode(),
                        Background = theme.SurfaceVariant,
                        Radius = theme.Radius,
                        Padding = EdgeInsets.Symmetric(16f, 12f),
                        OnPressed = () => _host.ShowToast("コピーしました"),
                        Child = new Text("Toast") { FontSize = theme.TextLabel, Color = theme.OnSurface },
                    },
                },
            },
        };

        Widget animated = new Card
        {
            Elevation = 2f,
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Start,
                MainAxisSize = MainAxisSize.Min,
                Spacing = theme.SpacingS,
                Children = new Widget[]
                {
                    new Text("アニメするボタン（hoverで色替え＝Builder／押下で弾む＝OnStateChanged）") { FontSize = theme.TextLabel, Color = theme.OnSurfaceVariant },
                    new Row
                    {
                        Spacing = theme.SpacingM,
                        Children = new Widget[]
                        {
                            new AnimatedGameButton { Label = "プレイ", OnPressed = () => _host.ShowToast("プレイ！") },
                            new AnimatedGameButton { Label = "設定", OnPressed = () => _host.ShowToast("設定") },
                        },
                    },
                },
            },
        };

        return new ScrollView
        {
            Child = new Container
            {
                Padding = EdgeInsets.All(theme.SpacingM),
                Child = new Column
                {
                    CrossAxisAlignment = CrossAxisAlignment.Stretch,
                    MainAxisSize = MainAxisSize.Min,
                    Spacing = theme.SpacingM,
                    Children = new Widget[] { controls, gauges, animated, actions },
                },
            },
        };
    }

    private static Widget Labeled(HamonTheme theme, string label, Widget control) => new Row
    {
        CrossAxisAlignment = CrossAxisAlignment.Center,
        Children = new Widget[]
        {
            new Expanded { Child = new Text(label) { FontSize = theme.TextBody, Color = theme.OnSurface } },
            control,
        },
    };
}

/// <summary>Form: Snackbar/Toast with Validated Input/DatePicker/Submit.</summary>
public sealed class FormsPage : HookWidget
{
    private readonly HamonRoot _host;

    public FormsPage(HamonRoot host) => _host = host;

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        FormController form = hooks.UseMemo(() => new FormController());
        TextEditingController name = hooks.UseMemo(() => new TextEditingController(string.Empty));
        TextEditingController email = hooks.UseMemo(() => new TextEditingController(string.Empty));
        HookState<DateTime?> date = hooks.UseState<DateTime?>(null);

        Widget card = new Card
        {
            Elevation = 3f,
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Stretch,
                MainAxisSize = MainAxisSize.Min,
                Spacing = theme.SpacingM,
                Children = new Widget[]
                {
                    new Text("アカウント登録") { FontSize = theme.TextTitle, Color = theme.OnSurface },
                    new TextFormField { Form = form, Name = "name", Controller = name, Label = "お名前", Placeholder = "山田 太郎", Validator = Validators.Required("お名前は必須です") },
                    new TextFormField { Form = form, Name = "email", Controller = email, Label = "メール", Placeholder = "you@example.com", Validator = Validators.Compose(Validators.Required("メールは必須です"), Validators.Email()) },
                    new Row
                    {
                        CrossAxisAlignment = CrossAxisAlignment.Center,
                        Children = new Widget[]
                        {
                            new Expanded { Child = new Text(date.Value is DateTime d ? $"誕生日：{d:yyyy/MM/dd}" : "誕生日：未選択") { FontSize = theme.TextBody, Color = theme.OnSurfaceVariant } },
                            new Button
                            {
                                Node = new FocusNode(),
                                Background = theme.SurfaceVariant,
                                Radius = theme.Radius,
                                Padding = EdgeInsets.Symmetric(14f, 10f),
                                OnPressed = () => _host.ShowDatePicker(date.Value ?? DateTime.Today, picked => date.Value = picked, date.Value),
                                Child = new Text("選択") { FontSize = theme.TextLabel, Color = theme.OnSurface },
                            },
                        },
                    },
                    new Button
                    {
                        Node = new FocusNode(),
                        Background = theme.Primary,
                        Radius = theme.Radius,
                        Padding = EdgeInsets.Symmetric(16f, 14f),
                        OnPressed = () =>
                        {
                            if (form.Validate())
                            {
                                _host.ShowSnackbar($"登録しました：{name.Text}");
                            }
                            else
                            {
                                _host.ShowToast("入力内容を確認してください");
                            }
                        },
                        Child = new Align { Alignment = Alignment.Center, Child = new Text("登録する") { FontSize = theme.TextBody, Color = theme.OnPrimary } },
                    },
                },
            },
        };

        return new ScrollView
        {
            Child = new Container { Padding = EdgeInsets.All(theme.SpacingM), Child = card },
        };
    }
}

/// <summary>Gallery: Tiles with Hero tag. </summary>
public sealed class GalleryPage : StatelessWidget
{
    private readonly RotationClock _clock;
    private readonly Action<int, Color> _openDetail;

    public GalleryPage(RotationClock clock, Action<int, Color> openDetail)
    {
        _clock = clock;
        _openDetail = openDetail;
    }

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        var rows = new List<Widget>(3);
        for (int r = 0; r < 3; r++)
        {
            var cells = new Widget[2];
            for (int c = 0; c < 2; c++)
            {
                int index = (r * 2) + c;
                Color color = Palette.Tiles[index];
                cells[c] = new Expanded { Child = Tile(theme, index, color) };
            }

            rows.Add(new Row { Spacing = theme.SpacingM, CrossAxisAlignment = CrossAxisAlignment.Stretch, Children = cells });
        }

        return new ScrollView
        {
            Child = new Container
            {
                Padding = EdgeInsets.All(theme.SpacingM),
                Child = new Column
                {
                    CrossAxisAlignment = CrossAxisAlignment.Stretch,
                    MainAxisSize = MainAxisSize.Min,
                    Spacing = theme.SpacingM,
                    Children = rows,
                },
            },
        };
    }

    private Widget Tile(HamonTheme theme, int index, Color color) => new Button
    {
        Node = new FocusNode(),
        Background = new Color(0, 0, 0, 0),
        Padding = EdgeInsets.Zero,
        OnPressed = () => _openDetail(index, color),
        Child = new Hero
        {
            Tag = $"tile{index}",
            Child = new Material
            {
                Color = color,
                Radius = theme.Radius,
                Elevation = 2f,
                Child = new SizedBox { Height = Dimension.Px(96f) },
            },
        },
    };
}

/// <summary>Gallery details: Enlarged Hero (same tag) + Return. </summary>
public sealed class GalleryDetail : StatelessWidget
{
    private readonly int _index;
    private readonly Color _color;
    private readonly NavigatorController _nav;

    public GalleryDetail(int index, Color color, NavigatorController nav)
    {
        _index = index;
        _color = color;
        _nav = nav;
    }

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        return new Scaffold
        {
            AppBar = new AppBar
            {
                Title = $"アイテム {_index + 1}",
                Leading = new Button
                {
                    Node = new FocusNode(),
                    Autofocus = true,
                    Background = new Color(0, 0, 0, 0),
                    Radius = theme.Radius,
                    Padding = EdgeInsets.Symmetric(10f, 8f),
                    OnPressed = () => _nav.Pop(),
                    Child = new Text("←") { FontSize = 22f, Color = theme.OnSurface },
                },
            },
            Body = new Container
            {
                Padding = EdgeInsets.All(theme.SpacingL),
                Child = new Column
                {
                    CrossAxisAlignment = CrossAxisAlignment.Stretch,
                    Spacing = theme.SpacingL,
                    Children = new Widget[]
                    {
                        new Hero
                        {
                            Tag = $"tile{_index}",
                            Child = new Material
                            {
                                Color = _color,
                                Radius = theme.Radius,
                                Elevation = 6f,
                                Child = new SizedBox { Height = Dimension.Px(220f) },
                            },
                        },
                        new Text("このカードはギャラリーのタイルから Hero 遷移で拡大されました。戻ると縮みながら元の位置へ戻ります。")
                        {
                            FontSize = theme.TextBody,
                            Color = theme.OnSurfaceVariant,
                        },
                    },
                },
            },
        };
    }
}

/// <summary>
/// Animate game buttons (declarative). <see cref="Button.Builder"/>), at the moment of pressing
/// Play bouncing animation (<see cref="Button.OnStateChanged"/>in<see cref="AnimationController"/>Drive the<see cref="Transform"/>of
/// getter without rebuilding).
/// </summary>
public sealed class AnimatedGameButton : HookWidget
{
    public string Label { get; init; } = string.Empty;

    public Action? OnPressed { get; init; }

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        AnimationController pop = hooks.UseAnimation(0.22f, Curves.EaseOutBack); // 押下で弾むワンショット

        return new Button
        {
            Node = hooks.UseFocusNode(),
            OnPressed = OnPressed,
            OnStateChanged = state =>
            {
                if (state.Has(WidgetState.Pressed))
                {
                    pop.JumpTo(0f);
                    pop.Forward(); // 押した瞬間にポップ再生
                }
            },
            // 見た目は状態で丸ごと組む（hover で色とテキスト色が変わる）＋ pop で全体がスケール。
            Builder = state => new Transform
            {
                Origin = Alignment.Center,
                ScaleGetter = () => 1f + (0.14f * pop.Curved),
                Child = new Container
                {
                    Color = state.Has(WidgetState.Hovered) ? theme.Primary : theme.SurfaceVariant,
                    Radius = theme.Radius,
                    Padding = EdgeInsets.Symmetric(22f, 12f),
                    Child = new Text(Label)
                    {
                        FontSize = theme.TextBody,
                        Color = state.Has(WidgetState.Hovered) ? theme.OnPrimary : theme.OnSurface,
                    },
                },
            },
        };
    }
}

/// <summary>Line minichart (demonstration of using drawing primitives = lines/circles directly).</summary>
public sealed class MiniChart : Widget
{
    public required float[] Data { get; init; }

    public Color? Line { get; init; }

    public float Height { get; init; } = 120f;

    public override Element CreateElement() => new MiniChartElement(this);
}

internal sealed class MiniChartElement : Element
{
    private readonly LayoutNode _node;

    public MiniChartElement(MiniChart widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private MiniChart W => (MiniChart)Widget;

    public override LayoutNode LayoutNode => _node;

    public override void Paint(in PaintContext context)
    {
        Rect b = LayoutNode.Bounds;
        HamonTheme theme = Context.Theme;
        Color line = W.Line ?? theme.Primary;
        float[] data = W.Data;
        if (data.Length < 2 || b.Width <= 0f || b.Height <= 0f)
        {
            return;
        }

        float min = data[0];
        float max = data[0];
        for (int i = 1; i < data.Length; i++)
        {
            min = MathF.Min(min, data[i]);
            max = MathF.Max(max, data[i]);
        }

        float range = MathF.Max(0.0001f, max - min);
        float pad = 8f;
        float plotW = b.Width - (2f * pad);
        float plotH = b.Height - (2f * pad);

        // 目盛り（薄い横線）。
        var grid = new Color(theme.OnSurface.R, theme.OnSurface.G, theme.OnSurface.B, 24);
        for (int g = 0; g <= 3; g++)
        {
            float y = b.Y + pad + (plotH * g / 3f);
            context.DrawLine(new Vec2(b.X + pad, y), new Vec2(b.X + pad + plotW, y), 1f, grid);
        }

        // 折れ線＋頂点ドット。
        Vec2 Point(int i)
        {
            float x = b.X + pad + (plotW * i / (data.Length - 1));
            float y = b.Y + pad + (plotH * (1f - ((data[i] - min) / range)));
            return new Vec2(x, y);
        }

        Vec2 prev = Point(0);
        for (int i = 1; i < data.Length; i++)
        {
            Vec2 next = Point(i);
            context.DrawLine(prev, next, 2.5f, line);
            prev = next;
        }

        for (int i = 0; i < data.Length; i++)
        {
            context.FillCircle(Point(i), 3.5f, line);
        }
    }

    private Size MeasureSelf(BoxConstraints constraints)
    {
        float width = float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : 280f;
        return new Size(width, W.Height);
    }
}
