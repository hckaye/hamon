using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.SampleApp;

/// <summary>
/// Standard mobile UI sample: ToDo app (**hook + atom version**). <see cref="Atom{T}"/>(items/filter) and
/// <c>hooks.UseAtom</c>Read and write.
/// This subtree is automatically rebuilt. <c>UseMemo</c>/<c>UseFocusNode</c>Permanent.
/// </summary>
public sealed class TodoApp : HookWidget
{
    private static readonly Atom<TodoItem[]> ItemsAtom = new(new[]
    {
        new TodoItem("牛乳を買う") { Done = true },
        new TodoItem("装備を強化する"),
        new TodoItem("ボスに挑戦する"),
    });

    private static readonly Atom<int> FilterAtom = new(0); // 0=すべて 1=未完了 2=完了

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        (TodoItem[] items, Action<TodoItem[]> setItems) = hooks.UseAtom(ItemsAtom);
        (int filter, Action<int> setFilter) = hooks.UseAtom(FilterAtom);
        var input = hooks.UseMemo(() => new TextEditingController());
        FocusNode inputNode = hooks.UseFocusNode();
        FocusNode addNode = hooks.UseFocusNode();
        FocusNode[] filterNodes = { hooks.UseFocusNode(), hooks.UseFocusNode(), hooks.UseFocusNode() };

        void Add()
        {
            string text = input.Text.Trim();
            if (text.Length > 0)
            {
                // Append＝新配列を1つだけ確保して既存要素をコピー（LINQ の Append().ToArray() より割り当てが少ない）。
                var next = new TodoItem[items.Length + 1];
                Array.Copy(items, next, items.Length);
                next[items.Length] = new TodoItem(text);
                setItems(next);
                input.SetText(string.Empty);
            }
        }

        // フィルタ適用は Build ごと（描画中は毎フレーム）に走るのでホットパス。LINQ を避け明示ループで組む。
        var visible = new List<TodoItem>(items.Length);
        int active = 0;
        foreach (TodoItem item in items)
        {
            if (!item.Done)
            {
                active++;
            }

            bool show = filter switch { 1 => !item.Done, 2 => item.Done, _ => true };
            if (show)
            {
                visible.Add(item);
            }
        }

        Widget FilterButton(string label, int value) => new Button
        {
            Node = filterNodes[value],
            Background = filter == value ? theme.Primary : theme.SurfaceVariant,
            Padding = EdgeInsets.Symmetric(14f, 8f),
            OnPressed = () => setFilter(value),
            Child = new Text(label) { FontSize = 14f, Color = filter == value ? theme.OnPrimary : theme.OnSurface },
        };

        Widget Row_(TodoItem item) => new Container
        {
            Key = item, // 行の安定 Key＝削除/並べ替えで index 再利用による状態混線を防ぐ
            Padding = EdgeInsets.Symmetric(12f, 10f),
            Child = new Row
            {
                MainAxisAlignment = MainAxisAlignment.SpaceBetween,
                CrossAxisAlignment = CrossAxisAlignment.Center,
                Children = new Widget[]
                {
                    new Row
                    {
                        Spacing = 12f,
                        CrossAxisAlignment = CrossAxisAlignment.Center,
                        Children = new Widget[]
                        {
                            new Checkbox
                            {
                                Node = new FocusNode(),
                                Value = item.Done,
                                OnChanged = _ =>
                                {
                                    var next = new TodoItem[items.Length];
                                    for (int i = 0; i < items.Length; i++)
                                    {
                                        next[i] = ReferenceEquals(items[i], item) ? item.Toggled() : items[i];
                                    }

                                    setItems(next);
                                },
                            },
                            new Text(item.Text)
                            {
                                FontSize = 18f,
                                Color = item.Done ? theme.OnSurfaceVariant : theme.OnSurface,
                            },
                        },
                    },
                    new Button
                    {
                        Node = new FocusNode(),
                        Background = theme.SurfaceVariant,
                        Padding = EdgeInsets.Symmetric(10f, 6f),
                        OnPressed = () =>
                        {
                            var next = new List<TodoItem>(items.Length);
                            foreach (TodoItem t in items)
                            {
                                if (!ReferenceEquals(t, item))
                                {
                                    next.Add(t);
                                }
                            }

                            setItems(next.ToArray());
                        },
                        Child = new Text("✕") { FontSize = 16f, Color = theme.Danger },
                    },
                },
            },
        };

        // Stack(expand)＋Positioned(inset) で全画面に背景＋内側余白を敷く。
        return new Stack
        {
            Fit = StackFit.Expand,
            Background = theme.Background,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(24f),
                    Top = Dimension.Px(24f),
                    Right = Dimension.Px(24f),
                    Bottom = Dimension.Px(24f),
                    Child = new Column
                    {
                        CrossAxisAlignment = CrossAxisAlignment.Stretch,
                        Spacing = 16f,
                        Children = new Widget[]
                        {
                            new Text("ToDo") { FontSize = 28f, Color = theme.OnSurface },
                            new Container
                            {
                                Color = theme.Surface,
                                Radius = theme.Radius,
                                Child = new TextField
                                {
                                    Controller = input,
                                    Node = inputNode,
                                    Placeholder = "新しいタスク…",
                                    FontSize = 18f,
                                    OnSubmitted = _ => Add(),
                                },
                            },
                            new Button
                            {
                                Node = addNode,
                                Background = theme.Primary,
                                Padding = EdgeInsets.Symmetric(16f, 12f),
                                OnPressed = Add,
                                Child = new Text("追加") { FontSize = 16f, Color = theme.OnPrimary },
                            },
                            new Row
                            {
                                Spacing = 8f,
                                Children = new Widget[] { FilterButton("すべて", 0), FilterButton("未完了", 1), FilterButton("完了", 2) },
                            },
                            new Expanded
                            {
                                // 残り全高を占めるリスト。背景パネルは Stack(expand) で敷く。
                                Child = new Stack
                                {
                                    Fit = StackFit.Expand,
                                    Background = theme.Surface,
                                    Radius = theme.Radius,
                                    Children = new Widget[]
                                    {
                                        new ListView
                                        {
                                            ItemCount = visible.Count,
                                            EstimatedExtent = 52f,
                                            Builder = index => Row_(visible[index]),
                                        },
                                    },
                                },
                            },
                            new Row
                            {
                                MainAxisAlignment = MainAxisAlignment.SpaceBetween,
                                CrossAxisAlignment = CrossAxisAlignment.Center,
                                Children = new Widget[]
                                {
                                    new Text($"{active} 件 未完了") { FontSize = 14f, Color = theme.OnSurfaceVariant },
                                    new Button
                                    {
                                        Node = new FocusNode(),
                                        Background = theme.SurfaceVariant,
                                        Padding = EdgeInsets.Symmetric(12f, 8f),
                                        OnPressed = () =>
                                        {
                                            var next = new List<TodoItem>(items.Length);
                                            foreach (TodoItem t in items)
                                            {
                                                if (!t.Done)
                                                {
                                                    next.Add(t);
                                                }
                                            }

                                            setItems(next.ToArray());
                                        },
                                        Child = new Text("完了を消す") { FontSize = 14f, Color = theme.OnSurface },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
    }

    private sealed class TodoItem
    {
        public TodoItem(string text) => Text = text;

        public string Text { get; }

        public bool Done { get; init; }

        public TodoItem Toggled() => new(Text) { Done = !Done };
    }
}
