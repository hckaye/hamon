using Hamon;
using Hamon.Fonts;
using Hamon.Layout;
using Hamon.MonoGame;
using Hamon.Widgets;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xna = Microsoft.Xna.Framework;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRect = Microsoft.Xna.Framework.Rectangle;

namespace Hamon.Sandbox;

/// <summary>
/// Hamon's actual drawing demo/self-test.
/// and draw TTF with FontStashTextRenderer.
/// Draw one frame, save as PNG, and exit (headless verification).
/// </summary>
internal sealed class SandboxGame : Xna.Game
{
    private readonly Xna.GraphicsDeviceManager _graphics;
    private readonly string _fontPath;
    private readonly string? _dumpPath;

    private readonly FocusNode[] _slotNodes = { new(), new(), new(), new() };
    private readonly FocusNode[] _modalNodes = { new(), new() };
    private readonly FocusNode _backNode = new();
    private readonly bool _showModal;
    private readonly bool _navDemo;
    private readonly bool _scrollDemo;
    private readonly bool _listDemo;
    private readonly bool _gridDemo;
    private readonly bool _animDemo;
    private readonly bool _focusDemo;
    private readonly bool _dialogDemo;
    private readonly bool _sheetDemo;
    private readonly bool _tabsDemo;
    private readonly bool _drawerDemo;
    private readonly bool _textDemo;
    private readonly bool _controlsDemo;
    private readonly bool _hooksDemo;
    private readonly bool _imageDemo;
    private readonly bool _patternsDemo;
    private ITexture _gradientTex = null!;
    private ITexture _panelTex = null!;
    private MouseState _prevMouse;
    private readonly ScrollController _scroll = new();
    private AnimationController _anim = null!;
    private TabController _tabs = null!;
    private TextEditingController _textCtrl = null!;
    private readonly FocusNode _textNode = new();
    private KeyboardState _prevKeys;
    private readonly FocusNode[] _focusButtons = { new(), new(), new() };

    private NavigatorController _nav = null!;

    private SpriteBatch _batch = null!;
    private MonoGamePainter _painter = null!;
    private FontStashTextRenderer _text = null!;
    private HamonRoot _ui = null!;
    private Texture2D _pixel = null!;

    public SandboxGame(string fontPath, string? dumpPath, bool showModal, bool navDemo, bool scrollDemo, bool listDemo, bool gridDemo, bool animDemo, bool focusDemo, bool dialogDemo, bool sheetDemo, bool tabsDemo, bool drawerDemo, bool textDemo, bool controlsDemo, bool hooksDemo, bool imageDemo, bool patternsDemo)
    {
        _textDemo = textDemo;
        _controlsDemo = controlsDemo;
        _hooksDemo = hooksDemo;
        _imageDemo = imageDemo;
        _patternsDemo = patternsDemo;
        _graphics = new Xna.GraphicsDeviceManager(this);
        _fontPath = fontPath;
        _dumpPath = dumpPath;
        _showModal = showModal;
        _navDemo = navDemo;
        _scrollDemo = scrollDemo;
        _listDemo = listDemo;
        _gridDemo = gridDemo;
        _animDemo = animDemo;
        _focusDemo = focusDemo;
        _dialogDemo = dialogDemo;
        _sheetDemo = sheetDemo;
        _tabsDemo = tabsDemo;
        _drawerDemo = drawerDemo;
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { XnaColor.White });
        _text = new FontStashTextRenderer(GraphicsDevice, File.ReadAllBytes(_fontPath), _batch);
        _ui = new HamonRoot(_text);
        _painter = new MonoGamePainter(GraphicsDevice, _batch);
        Window.TextInput += (_, e) => _ui.DispatchText(e.Character); // 物理キーボード→確定文字（TextField へ）

        if (_patternsDemo)
        {
            var hp = new ValueNotifier<int>(75);
            var hpRatio = new ValueNotifier<float>(0.75f);
            State<int> count = _ui.CreateState(0);
            _ui.SetRoot(() => BuildPatternsDemo(hp, hpRatio, count));
        }
        else if (_imageDemo)
        {
            _gradientTex = new MonoGameTexture(MakeGradient(GraphicsDevice, 64));
            _panelTex = new MonoGameTexture(MakePanel(GraphicsDevice, 48, 10));
            _ui.SetRoot(BuildImageDemo);
        }
        else if (_hooksDemo)
        {
            _ui.SetRoot(() => new HooksDemo());
        }
        else if (_controlsDemo)
        {
            _ui.SetRoot(BuildControlsDemo);
        }
        else if (_textDemo)
        {
            _textCtrl = new TextEditingController("勇者の名前");
            _ui.SetRoot(BuildTextDemo);
        }
        else if (_tabsDemo)
        {
            _tabs = new TabController(3, 0, 0.25f);
            _ui.SetRoot(BuildTabsDemo);
        }
        else if (_drawerDemo)
        {
            _ui.SetRoot(BuildUi);
            _ui.ShowDrawer(BuildDrawerPanel, 240f); // 左端から滑り込む抽斗
        }
        else if (_dialogDemo)
        {
            _ui.SetRoot(BuildUi);
            _ui.ShowDialog(BuildDialogCard); // 中央モーダル（scrim フェード＋カード拡大）
        }
        else if (_sheetDemo)
        {
            _ui.SetRoot(BuildUi);
            _ui.ShowBottomSheet(BuildSheet, 180f); // 下から迫り上がるシート
        }
        else if (_focusDemo)
        {
            _ui.Cursor.Enabled = true; // 単一カーソルが枠から枠へ滑る
            _ui.Cursor.GlideDuration = 0.16f;
            _ui.Cursor.Padding = 5f;
            _ui.Cursor.Thickness = 3f;
            _ui.Cursor.PulseAmplitude = 0.35f;
            _ui.SetRoot(BuildFocusDemo);
        }
        else if (_animDemo)
        {
            _anim = _ui.CreateAnimation(1f, Curves.EaseInOut);
            _ui.SetRoot(BuildAnimDemo);
        }
        else if (_gridDemo)
        {
            _ui.SetRoot(BuildGridDemo);
        }
        else if (_listDemo)
        {
            _ui.SetRoot(BuildListDemo);
        }
        else if (_scrollDemo)
        {
            _ui.SetRoot(BuildScrollDemo);
        }
        else if (_navDemo)
        {
            // home（既存UI）の上に詳細画面を push（Navigator＝各ルートを FocusScope で包んで積む）。
            _nav = new NavigatorController(_ui, BuildUi) { TransitionDuration = 0.3f }; // フェード＋拡大で遷移
            _nav.Push(BuildDetailScreen);
            _ui.SetRoot(() => new Navigator { Controller = _nav });
        }
        else
        {
            _ui.SetRoot(BuildUi);
            if (_showModal)
            {
                _ui.PushOverlay(BuildModal, 0.3f); // モーダル（scrim＋FocusScope ダイアログ）をフェード＋拡大で最前面に
            }
        }

        if (_showModal)
        {
            _ui.Update(new Size(680, 440));        // 一度レイアウト（モーダル入場アニメ進捗0＝透明）
            _ui.Update(new Size(680, 440), 0.12f); // 入場の途中（0.12/0.3＝約40%）でダンプ
        }

        if (_scrollDemo)
        {
            _ui.Update(new Size(680, 440)); // 一度レイアウトしてから途中までスクロール（クリップ可視化）
            _scroll.JumpTo(120f);
        }

        if (_listDemo)
        {
            _ui.Update(new Size(680, 440)); // 一度レイアウトしてから途中までスクロール
            _scroll.JumpTo(40_000f * 0.5f); // 1万件×54px の中ほどへ
        }

        if (_gridDemo)
        {
            _ui.Update(new Size(680, 440)); // 一度レイアウトしてから途中までスクロール
            _scroll.JumpTo(2_000f); // 1万セル×5列のグリッドの途中へ
        }

        if (_animDemo)
        {
            _ui.Update(new Size(680, 440)); // 一度レイアウト
            _anim.Forward();
            _ui.Update(new Size(680, 440), 0.4f); // 途中（進捗0.4＝フェード＋拡大の途中）まで進める
        }

        if (_navDemo)
        {
            _ui.Update(new Size(680, 440));        // 一度レイアウト（詳細ルートは入場アニメ進捗0＝透明）
            _ui.Update(new Size(680, 440), 0.12f); // 入場の途中（0.12/0.3＝約40%＝フェード＋拡大の最中）でダンプ
        }

        if (_dialogDemo || _sheetDemo)
        {
            _ui.Update(new Size(680, 440));        // 一度レイアウト（入場アニメ進捗0）
            _ui.Update(new Size(680, 440), 0.1f);  // 入場の途中（フェード＋拡大／迫り上がりの最中）でダンプ
        }

        if (_drawerDemo)
        {
            _ui.Update(new Size(680, 440));        // 一度レイアウト（進捗0）
            _ui.Update(new Size(680, 440), 0.17f); // 滑り込みがほぼ終盤（パネルが十分見える位置）でダンプ
        }

        if (_tabsDemo)
        {
            _ui.Update(new Size(680, 440));        // タブ0で一度レイアウト
            _tabs.Select(1);                       // タブ1へ切替
            _ui.Update(new Size(680, 440), 0.12f); // 切替アニメの途中（下線クロスフェード＋本体フェード）
        }

        if (_focusDemo)
        {
            _ui.Update(new Size(680, 440));            // 一度レイアウト＆初期フォーカス（ボタン1へスナップ）
            _ui.MoveFocus(FocusDirection.Right);       // 2番目へフォーカス移動
            _ui.Update(new Size(680, 440), 0f);        // 変化検知＝グライド開始（t=0）
            _ui.Update(new Size(680, 440), 0.08f);     // グライドを途中（0.08/0.16＝約半分）まで進めてダンプ
        }

        if (_dumpPath is not null)
        {
            DumpToPng(_dumpPath, 680, 440);
            Exit();
        }
    }

    // 仮想化リストのデモ：1万件を ItemExtent 固定で可視のみ実体化。中ほどまでスクロール。
    private Widget BuildListDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            Spacing = 10f,
            Children = new Widget[]
            {
                new Text("ListView（仮想化・1万件／可視のみ実体化）") { FontSize = 22f, Color = Color.White },
                new Container
                {
                    Color = new Color(12, 14, 18),
                    Child = new ListView
                    {
                        Controller = _scroll,
                        ItemCount = 10_000,
                        ItemExtent = 54f,
                        Height = Dimension.Px(300),
                        Width = Dimension.Px(420),
                        Builder = ListRow,
                    },
                },
                new Text("↑ 中ほどへスクロール。実体化されるのは画面に映る数行だけ") { FontSize = 15f, Color = Color.SkyBlue },
            },
        },
    };

    private static Widget ListRow(int index)
    {
        byte shade = (byte)(40 + ((index % 8) * 16));
        return new Container
        {
            Color = new Color(shade, (byte)(70 + ((index % 5) * 10)), 110),
            Padding = EdgeInsets.Symmetric(16f, 16f),
            Child = new Text($"アイテム #{index:00000} — 仮想化セル") { FontSize = 18f, Color = Color.White },
        };
    }

    // 小物コントロールのデモ：ProgressBar/Checkbox/Switch/Slider をそれぞれの状態で並べる。
    private Widget BuildControlsDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(24),
        Child = new Column
        {
            Spacing = 20f,
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new Text("Controls（ProgressBar / Checkbox / Switch / Slider）") { FontSize = 22f, Color = Color.White },
                new ProgressBar { Value = 0.65f, Width = Dimension.Px(360), Height = 10f },
                new Row
                {
                    Spacing = 16f,
                    CrossAxisAlignment = CrossAxisAlignment.Center,
                    Children = new Widget[]
                    {
                        new Checkbox { Value = true, Autofocus = true },
                        new Text("自動セーブ") { FontSize = 18f, Color = Color.White },
                    },
                },
                new Row
                {
                    Spacing = 16f,
                    CrossAxisAlignment = CrossAxisAlignment.Center,
                    Children = new Widget[]
                    {
                        new Switch { Value = true },
                        new Text("通知") { FontSize = 18f, Color = Color.White },
                    },
                },
                new Slider { Value = 0.6f, Width = Dimension.Px(360) },
            },
        },
    };

    // テキスト入力のデモ：フォーカス済みの TextField（初期テキスト＋点滅キャレット）。実行中はキーボードで編集可。
    private Widget BuildTextDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            Spacing = 14f,
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            Children = new Widget[]
            {
                new Text("TextField（キー入力で編集・キャレット点滅・フォーカス連動）") { FontSize = 22f, Color = Color.White },
                new Container
                {
                    Color = new Color(36, 42, 52),
                    Padding = EdgeInsets.All(4f),
                    Child = new TextField
                    {
                        Controller = _textCtrl,
                        Node = _textNode,
                        Autofocus = true,
                        FontSize = 22f,
                        Placeholder = "名前を入力…",
                        Background = new Color(12, 14, 18),
                    },
                },
                new Text("↑ 実行中はキーボードで入力／Backspace・矢印・Enter（本格IMEは後続）") { FontSize = 15f, Color = Color.SkyBlue },
            },
        },
    };

    // 使い方サンプル：状態なし合成・Bind・描画時読み・State<T>・HookWidget を1画面に並べる（UsagePatterns.cs）。
    private Widget BuildPatternsDemo(ValueNotifier<int> hp, ValueNotifier<float> hpRatio, State<int> count) => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Spacing = 14f,
            Children = new Widget[]
            {
                new Text("使い方サンプル（状態なし→Bind→State→Hook）") { FontSize = 22f, Color = Color.White },
                new UsagePatterns.PriceTag("ポーション", 120),            // StatelessWidget 合成（hooks 不要）
                UsagePatterns.HpBarBind(hp),                              // ValueNotifier+Bind（部分再構築）
                UsagePatterns.HpBarCheap(hpRatio),                        // ValueGetter（描画時読み＝再構築ゼロ）
                new Row                                                   // State<T>（命令的・素朴）
                {
                    Spacing = 12f,
                    CrossAxisAlignment = CrossAxisAlignment.Center,
                    Children = new Widget[]
                    {
                        new Text($"State count = {count.Value}") { FontSize = 18f, Color = Color.White },
                        new Button
                        {
                            Autofocus = true,
                            Background = new Color(52, 60, 76),
                            Padding = EdgeInsets.Symmetric(14f, 10f),
                            OnPressed = () => count.Value++,
                            Child = new Text("+1") { FontSize = 16f, Color = Color.White },
                        },
                    },
                },
                new UsagePatterns.HookCounter(),                          // HookWidget（フック）
            },
        },
    };

    // 画像/9-slice のデモ：手続き生成テクスチャを Image（各 Fit）と NineSlice パネルで描く。
    private Widget BuildImageDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Spacing = 16f,
            Children = new Widget[]
            {
                new Text("Image（Fill/Contain/Cover）／NineSlice パネル") { FontSize = 22f, Color = Color.White },
                new Row
                {
                    Spacing = 16f,
                    Children = new Widget[]
                    {
                        new Image { Texture = _gradientTex, Width = Dimension.Px(80), Height = Dimension.Px(80), Fit = BoxFit.Fill },
                        new Image { Texture = _gradientTex, Width = Dimension.Px(80), Height = Dimension.Px(50), Fit = BoxFit.Contain },
                        new Image { Texture = _gradientTex, Width = Dimension.Px(80), Height = Dimension.Px(50), Fit = BoxFit.Cover },
                    },
                },
                new NineSlice
                {
                    Texture = _panelTex, // 48px ソースを 380×120 へ：角は原寸・辺/中央のみ伸縮
                    Border = EdgeInsets.All(10f),
                    Width = Dimension.Px(380),
                    Height = Dimension.Px(120),
                    Child = new Padding
                    {
                        Insets = EdgeInsets.All(14f),
                        Child = new Text("9-slice パネル（枠を歪めず拡大）") { FontSize = 18f, Color = Color.White },
                    },
                },
            },
        },
    };

    private static Texture2D MakeGradient(GraphicsDevice device, int size)
    {
        var tex = new Texture2D(device, size, size);
        var data = new XnaColor[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                data[(y * size) + x] = new XnaColor(60 + (x * 3), 80 + (y * 2), 200 - (x * 2));
            }
        }

        tex.SetData(data);
        return tex;
    }

    private static Texture2D MakePanel(GraphicsDevice device, int size, int border)
    {
        var tex = new Texture2D(device, size, size);
        var data = new XnaColor[size * size];
        var edge = new XnaColor(120, 180, 255);
        var fill = new XnaColor(36, 44, 58);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x < border || y < border || x >= size - border || y >= size - border;
                data[(y * size) + x] = isBorder ? edge : fill;
            }
        }

        tex.SetData(data);
        return tex;
    }

    // タブのデモ：3タブ。タブ1への切替アニメの途中（下線クロスフェード＋本体フェードイン）でダンプ。
    private Widget BuildTabsDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            Spacing = 14f,
            Children = new Widget[]
            {
                new Text("Tabs（下線インジケータ＝クロスフェード／本体フェード）") { FontSize = 22f, Color = Color.White },
                new Tabs
                {
                    Controller = _tabs,
                    IndicatorColor = new Color(120, 180, 255),
                    Items = new[]
                    {
                        new TabItem(new Text("装備") { FontSize = 18f, Color = Color.White }, () => TabBody("装備の中身", new Color(40, 50, 66))),
                        new TabItem(new Text("スキル") { FontSize = 18f, Color = Color.White }, () => TabBody("スキルの中身", new Color(50, 42, 66))),
                        new TabItem(new Text("倉庫") { FontSize = 18f, Color = Color.White }, () => TabBody("倉庫の中身", new Color(40, 56, 50))),
                    },
                },
            },
        },
    };

    private static Widget TabBody(string label, Color color) => new Container
    {
        Color = color,
        Padding = EdgeInsets.All(28f),
        Child = new Text(label) { FontSize = 20f, Color = Color.White },
    };

    // ドロワーの中身（close で閉じられる）。
    private Widget BuildDrawerPanel(Action close) => new Container
    {
        Color = new Color(46, 54, 70),
        Padding = EdgeInsets.All(18f),
        Child = new Column
        {
            Spacing = 12f,
            Children = new Widget[]
            {
                new Text("メニュー") { FontSize = 20f, Color = Color.White },
                new Button { Node = new FocusNode(), Autofocus = true, Background = new Color(52, 60, 76), Padding = EdgeInsets.Symmetric(14f, 10f), Child = new Text("ホーム") { FontSize = 16f, Color = Color.White }, OnPressed = close },
                new Button { Node = new FocusNode(), Background = new Color(52, 60, 76), Padding = EdgeInsets.Symmetric(14f, 10f), Child = new Text("設定") { FontSize = 16f, Color = Color.White }, OnPressed = close },
            },
        },
    };

    // ダイアログのカード（close で閉じられる）。
    private Widget BuildDialogCard(Action close) => new Container
    {
        Color = new Color(38, 44, 56),
        Padding = EdgeInsets.All(24f),
        Child = new Column
        {
            Spacing = 16f,
            Children = new Widget[]
            {
                new Text("確認") { FontSize = 22f, Color = Color.White },
                new Text("この装備を売却しますか？") { FontSize = 17f, Color = new Color(200, 206, 216) },
                new Row
                {
                    Spacing = 12f,
                    Children = new Widget[]
                    {
                        new Button { Node = new FocusNode(), Background = new Color(60, 68, 84), Padding = EdgeInsets.Symmetric(18f, 10f), Child = new Text("キャンセル") { FontSize = 16f, Color = Color.White }, OnPressed = close },
                        new Button { Node = new FocusNode(), Autofocus = true, Background = new Color(196, 80, 120), Padding = EdgeInsets.Symmetric(18f, 10f), Child = new Text("売却") { FontSize = 16f, Color = Color.White }, OnPressed = close },
                    },
                },
            },
        },
    };

    // ボトムシートの中身。
    private Widget BuildSheet(Action close) => new Container
    {
        Color = new Color(30, 36, 46),
        Padding = EdgeInsets.All(20f),
        Child = new Column
        {
            Spacing = 12f,
            Children = new Widget[]
            {
                new Text("アクション") { FontSize = 20f, Color = Color.White },
                new Button { Node = new FocusNode(), Autofocus = true, Background = new Color(54, 62, 78), Padding = EdgeInsets.Symmetric(16f, 12f), Child = new Text("装備する") { FontSize = 16f, Color = Color.White }, OnPressed = close },
                new Button { Node = new FocusNode(), Background = new Color(54, 62, 78), Padding = EdgeInsets.Symmetric(16f, 12f), Child = new Text("捨てる") { FontSize = 16f, Color = Color.White }, OnPressed = close },
            },
        },
    };

    // フォーカスカーソルのデモ：単一カーソルがボタン1→2へ滑る途中（グライド）をダンプ。明滅付き。
    private Widget BuildFocusDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            Spacing = 16f,
            Children = new Widget[]
            {
                new Text("FocusCursor（単一カーソルが枠→枠へ滑る・明滅）") { FontSize = 22f, Color = Color.White },
                new Row
                {
                    Spacing = 24f,
                    Children = new Widget[]
                    {
                        FocusButton(0, "アイテム"),
                        FocusButton(1, "スキル"),
                        FocusButton(2, "ステータス"),
                    },
                },
                new Text("↑ ボタン1→2への移動アニメの途中（カーソルが中間位置）") { FontSize = 15f, Color = Color.SkyBlue },
            },
        },
    };

    private Widget FocusButton(int index, string label) => new Button
    {
        Node = _focusButtons[index],
        Autofocus = index == 0,
        Background = new Color(48, 56, 72),
        Padding = EdgeInsets.Symmetric(20f, 14f),
        Child = new Text(label) { FontSize = 20f, Color = Color.White },
    };

    // アニメのデモ：Opacity＋Transform をコントローラ進捗で駆動（フェードイン＋拡大）。途中（0.4）でダンプ。
    private Widget BuildAnimDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            Spacing = 16f,
            Children = new Widget[]
            {
                new Text("Animation（Opacity＋Transform を controller で駆動＝再構築なし）") { FontSize = 22f, Color = Color.White },
                new Opacity
                {
                    ValueGetter = () => _anim.Curved, // 0→1 でフェードイン
                    Child = new Transform
                    {
                        ScaleGetter = () => 0.6f + (0.4f * _anim.Curved), // 0.6→1.0 で拡大
                        Origin = Alignment.Center,
                        Child = new Container
                        {
                            Color = new Color(220, 90, 140),
                            Padding = EdgeInsets.All(36f),
                            Child = new Text("Fade + Scale") { FontSize = 28f, Color = Color.White },
                        },
                    },
                },
                new Text("↑ 進捗0.4 時点：半透明＋やや拡大の途中（EaseInOut）") { FontSize = 15f, Color = Color.SkyBlue },
            },
        },
    };

    // 仮想化グリッドのデモ：1万セルを5列・正方セルで、可視行のみ実体化。途中までスクロール（行単位仮想化）。
    private Widget BuildGridDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            Spacing = 10f,
            Children = new Widget[]
            {
                new Text("GridView（仮想化・1万セル／5列・可視行のみ実体化）") { FontSize = 22f, Color = Color.White },
                new Container
                {
                    Color = new Color(12, 14, 18),
                    Child = new GridView
                    {
                        Controller = _scroll,
                        ItemCount = 10_000,
                        CrossAxisCount = 5,
                        ChildAspectRatio = 1f, // 正方セル
                        CrossAxisSpacing = 6f,
                        MainAxisSpacing = 6f,
                        Height = Dimension.Px(300),
                        Width = Dimension.Px(430),
                        Builder = GridCell,
                    },
                },
                new Text("↑ 中ほどへスクロール。実体化されるのは画面に映る数行ぶんのセルだけ") { FontSize = 15f, Color = Color.SkyBlue },
            },
        },
    };

    private static Widget GridCell(int index)
    {
        byte shade = (byte)(40 + ((index % 9) * 12));
        return new Container
        {
            Color = new Color(shade, (byte)(60 + ((index % 6) * 12)), 130),
            Padding = EdgeInsets.All(20f),
            Child = new Text($"#{index}") { FontSize = 16f, Color = Color.White },
        };
    }

    // 縦スクロールのデモ：高さ固定のビューポートに、はみ出す行リストを入れて途中までスクロール（上下がクリップ）。
    private Widget BuildScrollDemo() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            Spacing = 10f,
            Children = new Widget[]
            {
                new Text("ScrollView（縦・クリップ＋スクロール）") { FontSize = 22f, Color = Color.White },
                new Container
                {
                    Color = new Color(12, 14, 18),
                    Child = new ScrollView
                    {
                        Controller = _scroll,
                        Height = Dimension.Px(260),
                        Width = Dimension.Px(360),
                        Child = ScrollRows(),
                    },
                },
                new Text("↑ 高さ260のビューポートを120pxスクロール（上端で行が切れる）") { FontSize = 15f, Color = Color.SkyBlue },
            },
        },
    };

    private static Widget ScrollRows()
    {
        var rows = new Widget[12];
        for (int i = 0; i < rows.Length; i++)
        {
            byte shade = (byte)(60 + (i * 12));
            rows[i] = new Container
            {
                Color = new Color(shade, (byte)(90 - (i * 4)), 120),
                Padding = EdgeInsets.Symmetric(14f, 12f),
                Child = new Text($"行 {i + 1:00} — スクロールで上下クリップ") { FontSize = 17f, Color = Color.White },
            };
        }

        return new Column { Spacing = 6f, Children = rows };
    }

    private Widget BuildUi() => new Stack
    {
        Children = new Widget[]
        {
            BuildPanel(),
            // 絶対配置オーバーレイ（右下アンカー＝ADR-0010 親指リーチ）。
            new Positioned
            {
                Right = Dimension.Px(16),
                Bottom = Dimension.Px(16),
                Width = Dimension.Px(120),
                Height = Dimension.Px(40),
                Child = new Container
                {
                    Color = new Color(200, 80, 80),
                    Padding = EdgeInsets.Symmetric(10f, 8f),
                    Child = new Text("● 行動") { FontSize = 20f, Color = Color.White },
                },
            },
        },
    };

    private Widget BuildPanel() => new Container
    {
        Color = new Color(24, 28, 36),
        Padding = EdgeInsets.All(16),
        Child = new Column
        {
            Spacing = 12f,
            Children = new Widget[]
            {
                new Text("Hamon 波紋 — 宣言的UI") { FontSize = 30f, Color = Color.White },
                new Text("こんにちは、世界。HP/MP/装備/倉庫") { FontSize = 22f, Color = Color.LightGreen },
                new Row
                {
                    Spacing = 10f,
                    Children = new Widget[]
                    {
                        SlotButton(0, Color.IndianRed, autofocus: true),
                        SlotButton(1, Color.SteelBlue),
                        SlotButton(2, Color.Goldenrod),
                        SlotButton(3, Color.MediumSeaGreen),
                    },
                },
                new Text("↑ Button（タップ／ゲームパッド両対応）。先頭に autofocus（リング）") { FontSize = 18f, Color = Color.SkyBlue },
                new SceneView
                {
                    Width = Dimension.Px(260),
                    Height = Dimension.Px(90),
                    Clip = false, // 共有バッチへ絶対座標で描く（viewport クリップは実ゲーム統合 TASK-051 で）
                    OnDraw = DrawScene,
                },
                new Text("↑ SceneView（ゲーム世界を Flex の子として埋め込み）") { FontSize = 18f, Color = Color.Khaki },
            },
        },
    };

    // ゲーム世界の代わりに市松模様を矩形内へ描く（SceneView が占める領域の可視化）。
    // push された詳細ルート（全画面・FocusScope が充填）。「戻る」で nav.Pop（B でも：配線はアプリ側1行）。
    private Widget BuildDetailScreen() => new Container
    {
        Color = new Color(30, 40, 30),
        Padding = EdgeInsets.All(24),
        Child = new Column
        {
            Spacing = 16f,
            Children = new Widget[]
            {
                new Text("詳細画面（push されたルート）") { FontSize = 28f, Color = Color.White },
                new Text("Navigator が最前面ルートを全画面で描画。フォーカスはこの画面に閉じ込め") { FontSize = 16f, Color = Color.LightGray },
                new Row
                {
                    MainAxisSize = MainAxisSize.Min,
                    Children = new Widget[]
                    {
                        new Button
                        {
                            Node = _backNode,
                            Autofocus = true,
                            Background = new Color(90, 110, 150),
                            Padding = EdgeInsets.Symmetric(18f, 10f),
                            OnPressed = () => _nav.Pop(),
                            Child = new Text("← 戻る") { FontSize = 18f, Color = Color.White },
                        },
                    },
                },
            },
        },
    };

    // 最前面モーダル：全画面 scrim（外タップで閉じる想定）＋中央の FocusScope ダイアログ。
    // FocusScope がフォーカスをダイアログ内に閉じ込め、先頭ボタンに autofocus（リング）。
    private Widget BuildModal() => new Stack
    {
        Alignment = Alignment.Center,
        Children = new Widget[]
        {
            new Positioned
            {
                Left = Dimension.Px(0),
                Top = Dimension.Px(0),
                Right = Dimension.Px(0),
                Bottom = Dimension.Px(0),
                Child = new Container { Color = new Color(0, 0, 0, 150) }, // scrim
            },
            new FocusScope
            {
                Child = new Container
                {
                    Color = new Color(36, 42, 54),
                    Padding = EdgeInsets.All(20),
                    Child = new Column
                    {
                        Spacing = 14f,
                        Children = new Widget[]
                        {
                            new Text("確認ダイアログ") { FontSize = 24f, Color = Color.White },
                            new Text("フォーカスはこの中に閉じ込められます") { FontSize = 16f, Color = Color.LightGray },
                            new Row
                            {
                                MainAxisSize = MainAxisSize.Min, // content 幅に縮める（Flex 既定 Max だと全幅）
                                Spacing = 12f,
                                Children = new Widget[]
                                {
                                    ModalButton(0, "OK", new Color(80, 150, 90), autofocus: true),
                                    ModalButton(1, "キャンセル", new Color(120, 90, 90)),
                                },
                            },
                        },
                    },
                },
            },
        },
    };

    private Widget ModalButton(int index, string label, Color color, bool autofocus = false) => new Button
    {
        Node = _modalNodes[index],
        Autofocus = autofocus,
        Background = color,
        Padding = EdgeInsets.Symmetric(16f, 10f),
        OnPressed = () => { },
        Child = new Text(label) { FontSize = 18f, Color = Color.White },
    };

    private void DrawScene(SceneDrawContext scene)
    {
        Rect b = scene.Bounds; // デバイス空間（物理px）
        int cell = Math.Max(1, (int)(18 * scene.Scale)); // 論理18pt 相当のセル（HiDPI で物理px へ拡大）
        int cols = (int)MathF.Ceiling(b.Width / cell);
        int rows = (int)MathF.Ceiling(b.Height / cell);
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                bool dark = ((row + col) & 1) == 0;
                XnaColor color = dark ? new XnaColor(40, 60, 90) : new XnaColor(80, 120, 160);
                int x = (int)b.X + (col * cell);
                int y = (int)b.Y + (row * cell);
                int w = Math.Min(cell, (int)b.Right - x);
                int h = Math.Min(cell, (int)b.Bottom - y);
                ((MonoGamePainter)scene.Painter).Batch.Draw(_pixel, new XnaRect(x, y, w, h), color);
            }
        }
    }

    private Widget SlotButton(int index, Color color, bool autofocus = false) => new Button
    {
        Node = _slotNodes[index],
        Autofocus = autofocus,
        Background = color,
        OnPressed = () => { },
        Child = new SizedBox { Width = Dimension.Px(56), Height = Dimension.Px(56) },
    };

    private void DumpToPng(string path, int width, int height)
    {
        // VLO_HIDPI_RATIO=N: 論理pt を (width/N, height/N) でレイアウトし、比率 N で描画スケールして
        // 物理px=width×height のターゲットへ描く（実機 Retina と同じ経路の検証用）。
        float ratio = float.TryParse(Environment.GetEnvironmentVariable("VLO_HIDPI_RATIO"), out var r) && r > 0f ? r : 1f;
        int pw = (int)(width * ratio), ph = (int)(height * ratio); // 物理px ターゲット（ratio=1 で従来通り）
        using var target = new RenderTarget2D(GraphicsDevice, pw, ph);
        GraphicsDevice.SetRenderTarget(target);
        _ui.DevicePixelRatio = ratio;
        _ui.Update(new Size(width, height)); // レイアウトは論理pt（width×height）
        _ui.Render(_painter); // Begin/End は Hamon が管理（シザークリップのため）
        GraphicsDevice.SetRenderTarget(null);

        using FileStream fs = File.Create(path);
        target.SaveAsPng(fs, pw, ph);
    }

    // 編集キーはエッジ検出（前フレーム up→今 down）で1回だけ配送。文字は Window.TextInput が担う。
    private void PollEditKeys()
    {
        KeyboardState keys = Keyboard.GetState();
        Send(keys, Keys.Back, TextEditKey.Backspace);
        Send(keys, Keys.Delete, TextEditKey.Delete);
        Send(keys, Keys.Left, TextEditKey.Left);
        Send(keys, Keys.Right, TextEditKey.Right);
        Send(keys, Keys.Home, TextEditKey.Home);
        Send(keys, Keys.End, TextEditKey.End);
        Send(keys, Keys.Enter, TextEditKey.Enter);
        _prevKeys = keys;
    }

    private void Send(KeyboardState keys, Keys key, TextEditKey edit)
    {
        if (keys.IsKeyDown(key) && _prevKeys.IsKeyUp(key))
        {
            _ui.DispatchEditKey(edit);
        }
    }

    // マウス左ボタンを Hamon のポインタ（Down/Move/Up）へ橋渡し（タップ/ドラッグ/スクロール）。
    private void PollMouse()
    {
        MouseState m = Mouse.GetState();
        var pos = new Vec2(m.X, m.Y);
        bool down = m.LeftButton == ButtonState.Pressed;
        bool wasDown = _prevMouse.LeftButton == ButtonState.Pressed;
        if (down && !wasDown)
        {
            _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Down));
        }
        else if (down && wasDown)
        {
            _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Move));
        }
        else if (!down && wasDown)
        {
            _ui.DispatchPointer(new PointerEvent(pos, PointerPhase.Up));
        }

        _prevMouse = m;
    }

    protected override void Draw(Xna.GameTime gameTime)
    {
        PollEditKeys(); // Backspace/矢印/Enter 等の編集キーをエッジ検出で配送
        PollMouse();    // マウス→ポインタ配送
        Viewport vp = GraphicsDevice.Viewport;
        // HiDPI/Retina: backbuffer/viewport は物理px、ClientBounds は論理pt。論理pt でレイアウトし、
        // 比率（物理÷論理）で描画スケールして物理px へ写す（テキストもこの比率でラスタライズ＝クッキリ）。
        var cb = Window.ClientBounds;
        _ui.DevicePixelRatio = cb.Width > 0 ? vp.Width / (float)cb.Width : 1f;
        _ui.Update(new Size(cb.Width, cb.Height), (float)gameTime.ElapsedGameTime.TotalSeconds); // dt で慣性フリングを前進
        GraphicsDevice.Clear(XnaColor.Black);
        _ui.Render(_painter); // Begin/End は Hamon が管理（シザークリップのため）
        base.Draw(gameTime);
    }
}

internal static class Program
{
    private static void Main(string[] args)
    {
        // 既定は同梱の Noto Sans JP（OFL 1.1）。第1引数 or HAMON_FONT で差し替え可。
        string bundled = Path.Combine(AppContext.BaseDirectory, "NotoSansJP-Regular.ttf");
        string? fontPath = args.Length > 0 ? args[0] : (Environment.GetEnvironmentVariable("HAMON_FONT") ?? bundled);
        if (string.IsNullOrEmpty(fontPath) || !File.Exists(fontPath))
        {
            Console.Error.WriteLine($"フォント TTF が見つかりません: {fontPath}（第1引数 か HAMON_FONT で指定可）。HAMON_DUMP=<png> でヘッドレス検証。");
            return;
        }

        string? dumpPath = Environment.GetEnvironmentVariable("HAMON_DUMP");
        bool showModal = Environment.GetEnvironmentVariable("HAMON_MODAL") is not null;
        bool navDemo = Environment.GetEnvironmentVariable("HAMON_NAV") is not null;
        bool scrollDemo = Environment.GetEnvironmentVariable("HAMON_SCROLL") is not null;
        bool listDemo = Environment.GetEnvironmentVariable("HAMON_LIST") is not null;
        bool gridDemo = Environment.GetEnvironmentVariable("HAMON_GRID") is not null;
        bool animDemo = Environment.GetEnvironmentVariable("HAMON_ANIM") is not null;
        bool focusDemo = Environment.GetEnvironmentVariable("HAMON_FOCUS") is not null;
        bool dialogDemo = Environment.GetEnvironmentVariable("HAMON_DIALOG") is not null;
        bool sheetDemo = Environment.GetEnvironmentVariable("HAMON_SHEET") is not null;
        bool tabsDemo = Environment.GetEnvironmentVariable("HAMON_TABS") is not null;
        bool drawerDemo = Environment.GetEnvironmentVariable("HAMON_DRAWER") is not null;
        bool textDemo = Environment.GetEnvironmentVariable("HAMON_TEXTFIELD") is not null;
        bool controlsDemo = Environment.GetEnvironmentVariable("HAMON_CONTROLS") is not null;
        bool hooksDemo = Environment.GetEnvironmentVariable("HAMON_HOOKS") is not null;
        bool imageDemo = Environment.GetEnvironmentVariable("HAMON_IMAGE") is not null;
        bool patternsDemo = Environment.GetEnvironmentVariable("HAMON_PATTERNS") is not null;
        using var game = new SandboxGame(fontPath, dumpPath, showModal, navDemo, scrollDemo, listDemo, gridDemo, animDemo, focusDemo, dialogDemo, sheetDemo, tabsDemo, drawerDemo, textDemo, controlsDemo, hooksDemo, imageDemo, patternsDemo);
        game.Run();
    }
}
