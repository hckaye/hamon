using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.E2E.Tests;

/// <summary>
/// Full-screen compositing (realistic combinations of multiple widgets) for integration testing.
/// </summary>
public static class Screens
{
    private static readonly Color Surface = new(30, 32, 40);
    private static readonly Color Card = new(44, 48, 60);
    private static readonly Color Accent = new(80, 130, 220);
    private static readonly Color OnSurface = new(230, 232, 240);
    private static readonly Color Muted = new(150, 154, 165);

    // ---- ダッシュボード（ヘッダ＋統計カード＋進捗＋リスト） ----
    public static Widget Dashboard() => new Container
    {
        Color = Surface,
        Padding = EdgeInsets.All(16f),
        Child = new Column
        {
            Children = new Widget[]
            {
                new Row
                {
                    CrossAxisAlignment = CrossAxisAlignment.Center,
                    Children = new Widget[]
                    {
                        new Text("Dashboard") { FontSize = 24f, Color = OnSurface },
                        new Spacer(),
                        new Switch { Value = true, OnChanged = _ => { } },
                    },
                },
                new SizedBox { Height = Dimension.Px(16) },
                new Row
                {
                    Children = new Widget[]
                    {
                        new Expanded { Child = StatCard("Users", "1,240") },
                        new SizedBox { Width = Dimension.Px(12) },
                        new Expanded { Child = StatCard("Revenue", "$5.2k") },
                    },
                },
                new SizedBox { Height = Dimension.Px(16) },
                new Material
                {
                    Color = Card,
                    Radius = 12f,
                    Elevation = 3f,
                    Child = new Container
                    {
                        Padding = EdgeInsets.All(16f),
                        Child = new Row
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Center,
                            Children = new Widget[]
                            {
                                new CircularProgressIndicator { Value = 0.72f, Diameter = 56f, StrokeWidth = 7f, BackgroundColor = new Color(70, 74, 90), Color = Accent },
                                new SizedBox { Width = Dimension.Px(16) },
                                new Column
                                {
                                    Children = new Widget[]
                                    {
                                        new Text("Monthly goal") { FontSize = 15f, Color = OnSurface },
                                        new Text("72% complete") { FontSize = 12f, Color = Muted },
                                    },
                                },
                            },
                        },
                    },
                },
                new SizedBox { Height = Dimension.Px(16) },
                ListRow("Inbox", "12 new"),
                ListRow("Tasks", "3 due"),
                ListRow("Messages", "5 unread"),
            },
        },
    };

    private static Widget StatCard(string label, string value) => new Material
    {
        Color = Card,
        Radius = 12f,
        Elevation = 2f,
        Child = new Container
        {
            Padding = EdgeInsets.All(14f),
            Child = new Column
            {
                Children = new Widget[]
                {
                    new Text(value) { FontSize = 22f, Color = OnSurface },
                    new Text(label) { FontSize = 12f, Color = Muted },
                },
            },
        },
    };

    private static Widget ListRow(string title, string trailing) => new Container
    {
        Padding = EdgeInsets.Symmetric(4f, 8f),
        Child = new Row
        {
            CrossAxisAlignment = CrossAxisAlignment.Center,
            Children = new Widget[]
            {
                new Container { Width = Dimension.Px(10), Height = Dimension.Px(10), Radius = 5f, Color = Accent },
                new SizedBox { Width = Dimension.Px(12) },
                new Text(title) { FontSize = 15f, Color = OnSurface },
                new Spacer(),
                new Text(trailing) { FontSize = 13f, Color = Muted },
            },
        },
    };

    // ---- 設定（ラベル＋トグル/スライダーの行） ----
    public static Widget Settings() => new Container
    {
        Color = Surface,
        Padding = EdgeInsets.All(16f),
        Child = new Column
        {
            Children = new Widget[]
            {
                new Text("Settings") { FontSize = 22f, Color = OnSurface },
                new SizedBox { Height = Dimension.Px(16) },
                SettingRow("Notifications", new Switch { Value = true, OnChanged = _ => { } }),
                SettingRow("Dark mode", new Switch { Value = true, OnChanged = _ => { } }),
                SettingRow("Sounds", new Switch { Value = false, OnChanged = _ => { } }),
                new SizedBox { Height = Dimension.Px(8) },
                new Text("Volume") { FontSize = 14f, Color = Muted },
                new Slider { Value = 0.65f, OnChanged = _ => { } },
                new Text("Brightness") { FontSize = 14f, Color = Muted },
                new Slider { Value = 0.3f, OnChanged = _ => { } },
            },
        },
    };

    private static Widget SettingRow(string label, Widget control) => new Container
    {
        Padding = EdgeInsets.Symmetric(0f, 10f),
        Child = new Row
        {
            CrossAxisAlignment = CrossAxisAlignment.Center,
            Children = new Widget[]
            {
                new Text(label) { FontSize = 16f, Color = OnSurface },
                new Spacer(),
                control,
            },
        },
    };

    // ---- 仮想リスト（多数カード・スクロール/perf 用） ----
    public static Widget VirtualList(int count, ScrollController? controller = null) => new Container
    {
        Color = Surface,
        Child = new ListView
        {
            ItemCount = count,
            ItemExtent = 64f,
            Controller = controller,
            Builder = i => new Container
            {
                Padding = EdgeInsets.All(8f),
                Child = new Material
                {
                    Color = (i % 2 == 0) ? Card : new Color(50, 54, 66),
                    Radius = 10f,
                    Child = new Container
                    {
                        Padding = EdgeInsets.All(12f),
                        Child = new Row
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Center,
                            Children = new Widget[]
                            {
                                new Container { Width = Dimension.Px(20), Height = Dimension.Px(20), Radius = 10f, Color = Accent },
                                new SizedBox { Width = Dimension.Px(12) },
                                new Text("Item") { FontSize = 15f, Color = OnSurface },
                                new Spacer(),
                                new Text("›") { FontSize = 16f, Color = Muted },
                            },
                        },
                    },
                },
            },
        },
    };

    // ---- インベントリ（D&D：ドラッグ元/ドロップ先＋DragLayer） ----
    public static Widget Inventory() => new Container
    {
        Color = Surface,
        Padding = EdgeInsets.All(16f),
        Child = new Stack
        {
            Children = new Widget[]
            {
                new Column
                {
                    Children = new Widget[]
                    {
                        new Text("Inventory") { FontSize = 22f, Color = OnSurface },
                        new SizedBox { Height = Dimension.Px(16) },
                        new Row
                        {
                            Children = new Widget[]
                            {
                                Slot(new Draggable<int>
                                {
                                    Data = 1,
                                    Child = Item(new Color(200, 120, 60)),
                                    Feedback = Item(new Color(200, 120, 60, 200)),
                                }),
                                new SizedBox { Width = Dimension.Px(12) },
                                Slot(new Draggable<int>
                                {
                                    Data = 2,
                                    Child = Item(new Color(80, 170, 120)),
                                    Feedback = Item(new Color(80, 170, 120, 200)),
                                }),
                                new SizedBox { Width = Dimension.Px(12) },
                                Slot(new SizedBox()),
                            },
                        },
                        new SizedBox { Height = Dimension.Px(24) },
                        new DragTarget<int>
                        {
                            OnAccept = _ => { },
                            Child = new Container
                            {
                                Width = Dimension.Px(260),
                                Height = Dimension.Px(100),
                                Radius = 12f,
                                Color = new Color(40, 44, 56),
                                Child = new Align { Alignment = Alignment.Center, Child = new Text("Drop here") { FontSize = 14f, Color = Muted } },
                            },
                        },
                    },
                },
                new DragLayer(),
            },
        },
    };

    private static Widget Slot(Widget child) => new Container
    {
        Width = Dimension.Px(64),
        Height = Dimension.Px(64),
        Radius = 10f,
        Color = new Color(40, 44, 56),
        Child = new Align { Alignment = Alignment.Center, Child = child },
    };

    private static Widget Item(Color color) => new Container { Width = Dimension.Px(48), Height = Dimension.Px(48), Radius = 8f, Color = color };

    // ---- タブ ----
    public static Widget TabsScreen(TabController controller) => new Container
    {
        Color = Surface,
        Padding = EdgeInsets.All(16f),
        Child = new Column
        {
            Children = new Widget[]
            {
                new Tabs
                {
                    Controller = controller,
                    Items = new[]
                    {
                        new TabItem(new Text("Overview") { FontSize = 14f }, () => Panel("Overview content", new Color(70, 110, 180))),
                        new TabItem(new Text("Details") { FontSize = 14f }, () => Panel("Details content", new Color(170, 90, 120))),
                    },
                },
            },
        },
    };

    private static Widget Panel(string text, Color color) => new Container
    {
        Padding = EdgeInsets.All(16f),
        Child = new Material
        {
            Color = color,
            Radius = 10f,
            Child = new Container { Padding = EdgeInsets.All(24f), Child = new Text(text) { FontSize = 16f, Color = OnSurface } },
        },
    };

    // ---- ストレス（多種ウィジェットの密な合成・perf 用） ----
    public static Widget Stress()
    {
        var rows = new List<Widget>
        {
            new Text("Stress") { FontSize = 20f, Color = OnSurface },
        };
        for (int i = 0; i < 12; i++)
        {
            rows.Add(new Container
            {
                Padding = EdgeInsets.Symmetric(0f, 4f),
                Child = new Row
                {
                    CrossAxisAlignment = CrossAxisAlignment.Center,
                    Children = new Widget[]
                    {
                        new CircularProgressIndicator { Value = (i % 10) / 10f, Diameter = 28f, StrokeWidth = 4f, BackgroundColor = new Color(70, 74, 90), Color = Accent },
                        new SizedBox { Width = Dimension.Px(8) },
                        new Expanded { Child = new Slider { Value = (i % 7) / 7f, OnChanged = _ => { } } },
                        new SizedBox { Width = Dimension.Px(8) },
                        new Switch { Value = i % 2 == 0, OnChanged = _ => { } },
                    },
                },
            });
        }

        return new Container { Color = Surface, Padding = EdgeInsets.All(12f), Child = new Column { Children = rows.ToArray() } };
    }

    /// <summary>
    /// In order to verify the composition and z-order of the overlay (scrim + front panel), the standard library
    /// <see cref="Modals.ShowBottomSheet"/>Take out the lower sheet at (explicit height = size is definitively determined).
    /// </summary>
    public static OverlayEntry ShowSheet(HamonRoot host) => host.ShowBottomSheet(
        close => new Container
        {
            Color = Card,
            Padding = EdgeInsets.All(20f),
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Start,
                Children = new Widget[]
                {
                    new Text("Options") { FontSize = 18f, Color = OnSurface },
                    new SizedBox { Height = Dimension.Px(12) },
                    new Text("Choose an action below.") { FontSize = 14f, Color = Muted },
                    new SizedBox { Height = Dimension.Px(16) },
                    new Button { OnPressed = close, Background = Accent, Radius = 8f, Padding = EdgeInsets.Symmetric(16f, 10f), Child = new Text("Confirm") { FontSize = 14f } },
                },
            },
        },
        height: 220f);
}
