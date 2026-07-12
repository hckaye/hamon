using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.SampleApp;

/// <summary>
/// The core of the sample catalog. <see cref="NavigatorController"/>Put it in the root of
/// In each entry selection<see cref="NavigatorController.Push"/>to open individual samples (Back/Esc/pop with pad B).
/// Each sample is<see cref="Scaffold"/>Wrap with (top bar with return + body).
/// </summary>
public sealed class SampleCatalog
{
    private readonly HamonRoot _host;
    private readonly ITexture[] _gallery;
    private readonly NavigatorController _nav;

    public SampleCatalog(HamonRoot host, ITexture[] gallery)
    {
        _host = host;
        _gallery = gallery;
        _nav = new NavigatorController(host, BuildHome) { TransitionDuration = 0.18f };
    }

    public Widget Root => new Navigator { Controller = _nav };

    public NavigatorController Nav => _nav;

    private Widget BuildHome()
    {
        return new ScaffoldHome(new (string Title, string Desc, Action Open)[]
        {
            ("電卓", "ボタングリッド＋ゲームパッド方向フォーカス＋State<T>", OpenCalculator),
            ("ストップウォッチ", "ticker＋Bind（高頻度値を再構築ゼロで反映）＋ラップ", OpenStopwatch),
            ("天気（async）", "UseAsync で取得・Loading/Ok/Fail を出し分け", OpenWeather),
            ("ToDo", "hooks＋atom・仮想化リスト・テキスト入力", OpenTodo),
            ("ギャラリー", "GridView（2D仮想化）＋Image＋タップでモーダル拡大", OpenGallery),
            ("設定", "Switch/Slider/Checkbox＋ダイアログ/ボトムシート/ドロワー(≡)", OpenSettings),
            ("コンポーネントカタログ", "全ウィジェット一覧＝Radio/Segmented/Dropdown/Tooltip/Wrap/RichText/Badge/Cooldown/Slot/Dpad/Joystick", OpenComponents),
        });
    }

    private void OpenCalculator() => _nav.Push(() => new Scaffold("電卓", new CalculatorApp(), _nav));

    private void OpenStopwatch() => _nav.Push(() => new Scaffold("ストップウォッチ", new StopwatchApp(), _nav));

    private void OpenWeather() => _nav.Push(() => new Scaffold("天気（async）", new WeatherApp(), _nav));

    private void OpenTodo() => _nav.Push(() => new Scaffold("ToDo", new TodoApp(), _nav));

    private void OpenGallery() => _nav.Push(() => new Scaffold("ギャラリー", new GalleryApp(_host, _gallery), _nav));

    private void OpenSettings() => _nav.Push(() => new Scaffold("設定", new SettingsApp(_host), _nav, onMenu: OpenDrawer));

    private void OpenComponents() => _nav.Push(() => new Scaffold("コンポーネントカタログ", new ComponentGalleryApp(), _nav));

    // ハンバーガー（≡）で端から滑り込むナビゲーション・ドロワー。
    private void OpenDrawer() => _host.ShowDrawer(
        close => new DrawerPanel(close),
        width: 260f);
}

/// <summary>Catalog home (list) page. </summary>
public sealed class ScaffoldHome : StatelessWidget
{
    private readonly (string Title, string Desc, Action Open)[] _entries;

    public ScaffoldHome((string Title, string Desc, Action Open)[] entries) => _entries = entries;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        var rows = new Widget[_entries.Length + 1];
        rows[0] = new Text("Hamon サンプルカタログ") { FontSize = 28f, Color = theme.OnSurface };
        for (int i = 0; i < _entries.Length; i++)
        {
            (string title, string desc, Action open) = _entries[i];
            rows[i + 1] = new Button
            {
                Node = new FocusNode(),
                Autofocus = i == 0,
                Background = theme.Surface,
                Radius = theme.Radius,
                Padding = EdgeInsets.Symmetric(16f, 14f),
                OnPressed = open,
                // Button は子を中央寄せする（Flutter 同様）。一覧は左寄せにしたいので Align(CenterLeft) で全幅＋左寄せ。
                Child = new Align
                {
                    Alignment = Alignment.CenterLeft,
                    Child = new Column
                    {
                        CrossAxisAlignment = CrossAxisAlignment.Start,
                        MainAxisSize = MainAxisSize.Min,
                        Spacing = 4f,
                        Children = new Widget[]
                        {
                            new Text(title) { FontSize = 18f, Color = theme.OnSurface },
                            new Text(desc) { FontSize = 13f, Color = theme.OnSurfaceVariant },
                        },
                    },
                },
            };
        }

        return new Stack
        {
            Fit = StackFit.Expand,
            Background = theme.Background,
            Children = new Widget[]
            {
                // Positioned の四辺 inset で tight な内側矩形を与える＝Column(Stretch) が全幅に広がる
                // （Container/ScrollView を間に挟むと tight 幅が伝わらず Stretch が効かない）。
                new Positioned
                {
                    Left = Dimension.Px(20f),
                    Top = Dimension.Px(20f),
                    Right = Dimension.Px(20f),
                    Bottom = Dimension.Px(20f),
                    Child = new Column
                    {
                        CrossAxisAlignment = CrossAxisAlignment.Stretch,
                        Spacing = 12f,
                        Children = rows,
                    },
                },
            },
        };
    }
}

/// <summary>Outer frame of individual samples: top bar with return (optional hamburger) + main body.</summary>
public sealed class Scaffold : StatelessWidget
{
    private readonly string _title;
    private readonly Widget _body;
    private readonly NavigatorController _nav;
    private readonly Action? _onMenu;

    public Scaffold(string title, Widget body, NavigatorController nav, Action? onMenu = null)
    {
        _title = title;
        _body = body;
        _nav = nav;
        _onMenu = onMenu;
    }

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;

        var barChildren = new List<Widget>
        {
            new Button
            {
                Node = new FocusNode(),
                Background = theme.SurfaceVariant,
                Radius = theme.Radius,
                Padding = EdgeInsets.Symmetric(12f, 8f),
                OnPressed = () => _nav.Pop(),
                Child = new Text("← 戻る") { FontSize = 15f, Color = theme.OnSurface },
            },
            new SizedBox { Width = Dimension.Px(12f) },
            new Text(_title) { FontSize = 20f, Color = theme.OnSurface },
            new Expanded { Child = new SizedBox() },
        };

        if (_onMenu is not null)
        {
            barChildren.Add(new Button
            {
                Node = new FocusNode(),
                Background = theme.SurfaceVariant,
                Radius = theme.Radius,
                Padding = EdgeInsets.Symmetric(14f, 8f),
                OnPressed = _onMenu,
                Child = new Text("≡") { FontSize = 20f, Color = theme.OnSurface },
            });
        }

        Widget topBar = new Container
        {
            Color = theme.Surface,
            Padding = EdgeInsets.Symmetric(12f, 10f),
            Child = new Row { CrossAxisAlignment = CrossAxisAlignment.Center, Children = barChildren },
        };

        return new Stack
        {
            Fit = StackFit.Expand,
            Background = theme.Background,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f),
                    Top = Dimension.Px(0f),
                    Right = Dimension.Px(0f),
                    Bottom = Dimension.Px(0f),
                    Child = new Column
                    {
                        CrossAxisAlignment = CrossAxisAlignment.Stretch,
                        Children = new Widget[] { topBar, new Expanded { Child = _body } },
                    },
                },
            },
        };
    }
}

/// <summary>The contents of the navigation drawer that opens with Hamburger.</summary>
public sealed class DrawerPanel : StatelessWidget
{
    private readonly Action _close;

    public DrawerPanel(Action close) => _close = close;

    public override Widget Build(BuildContext context)
    {
        HamonTheme theme = context.Theme;
        Widget Item(string label) => new Button
        {
            Node = new FocusNode(),
            Background = theme.SurfaceVariant,
            Radius = theme.Radius,
            Padding = EdgeInsets.Symmetric(14f, 12f),
            OnPressed = _close,
            Child = new Text(label) { FontSize = 16f, Color = theme.OnSurface },
        };

        return new Container
        {
            Color = theme.Surface,
            Padding = EdgeInsets.All(16f),
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Stretch,
                Spacing = 10f,
                Children = new Widget[]
                {
                    new Text("メニュー") { FontSize = 20f, Color = theme.OnSurface },
                    Item("プロフィール"),
                    Item("通知"),
                    Item("ヘルプ"),
                    new Button
                    {
                        Node = new FocusNode(),
                        Autofocus = true,
                        Background = theme.Primary,
                        Radius = theme.Radius,
                        Padding = EdgeInsets.Symmetric(14f, 12f),
                        OnPressed = _close,
                        Child = new Text("閉じる") { FontSize = 16f, Color = theme.OnPrimary },
                    },
                },
            },
        };
    }
}
