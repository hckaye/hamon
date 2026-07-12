using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.SampleApp3;

/// <summary>Equipment type (used to determine equipment slot acceptance).</summary>
public enum EquipKind
{
    Weapon,
    Shield,
    Helmet,
    Armor,
}

/// <summary>Item (icon + equipment type).</summary>
public sealed record Item(int Id, string Icon, EquipKind Kind);

/// <summary>Slot location (with equipment=true<see cref="Index"/>teeth<see cref="EquipKind"/>, false is the inventory subscript). </summary>
public readonly record struct SlotRef(bool Equipment, int Index);

/// <summary>
/// Mock model of equipment + belongings.
/// Only types that match are accepted, and swapping is only allowed if both types match.
/// </summary>
public sealed class LoadoutModel
{
    private readonly Item?[] _inv = new Item?[12];
    private readonly Item?[] _equip = new Item?[4]; // EquipKind 0..3

    public LoadoutModel()
    {
        Item[] init =
        {
            new(1, "剣", EquipKind.Weapon), new(2, "弓", EquipKind.Weapon), new(3, "杖", EquipKind.Weapon),
            new(4, "盾", EquipKind.Shield), new(5, "円", EquipKind.Shield),
            new(6, "兜", EquipKind.Helmet), new(7, "帽", EquipKind.Helmet),
            new(8, "鎧", EquipKind.Armor), new(9, "衣", EquipKind.Armor),
        };
        for (int i = 0; i < init.Length; i++)
        {
            _inv[i] = init[i];
        }
    }

    public int InventoryCount => _inv.Length;

    public int EquipCount => _equip.Length;

    public Item? Get(SlotRef r) => r.Equipment ? _equip[r.Index] : _inv[r.Index];

    private void Set(SlotRef r, Item? item)
    {
        if (r.Equipment)
        {
            _equip[r.Index] = item;
        }
        else
        {
            _inv[r.Index] = item;
        }
    }

    // 装備スロットは種別一致のみ（持ち物は何でも可・空も可）。
    private static bool Fits(Item? item, SlotRef slot) => item is null || !slot.Equipment || item.Kind == (EquipKind)slot.Index;

    /// <summary><paramref name="from"/>items of<paramref name="to"/>Can they be placed in the same place (both will fit after being replaced)?</summary>
    public bool CanPlace(SlotRef from, SlotRef to)
    {
        Item? item = Get(from);
        if (item is null || from == to)
        {
            return false;
        }

        return Fits(item, to) && Fits(Get(to), from);
    }

    /// <summary>Move (empty location = equipment/return, filled location = swap). </summary>
    public bool Move(SlotRef from, SlotRef to)
    {
        if (!CanPlace(from, to))
        {
            return false;
        }

        Item? item = Get(from);
        Item? other = Get(to);
        Set(to, item);
        Set(from, other);
        return true;
    }
}

/// <summary>
/// D&D mock screen (overlay) of equipment + inventory. <see cref="DragTarget{T}"/>(frame when it is possible to move/receive by dropping)
/// highlight) cum<see cref="Draggable{T}"/>(Drag the contents by following the pointer).<b>The contents of the slot are read from the model when drawing.</b>
/// （<see cref="SlotFace"/>), so updates after the drop do not require rebuilding/relayout = no clutter.
/// </summary>
internal sealed class LoadoutView : StatelessWidget
{
    private static readonly string[] KindLabel = { "武器", "盾", "兜", "鎧" };
    private static readonly string[] KindHint = { "剣", "盾", "兜", "鎧" };

    private readonly LoadoutModel _model;
    private readonly GameSkins _skins;
    private readonly Action _onClose;

    public LoadoutView(LoadoutModel model, GameSkins skins, Action onClose)
    {
        _model = model;
        _skins = skins;
        _onClose = onClose;
    }

    public override Widget Build(BuildContext context)
    {
        var hint = new Color(150, 158, 172);
        Widget panel = new Material
        {
            Skin = _skins.Panel,
            Child = new Container
            {
                Padding = EdgeInsets.All(18f),
                Child = new Column
                {
                    CrossAxisAlignment = CrossAxisAlignment.Stretch,
                    MainAxisSize = MainAxisSize.Min,
                    Spacing = 14f,
                    Children = new Widget[]
                    {
                        new Row
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Center,
                            Children = new Widget[]
                            {
                                new Expanded { Child = new Text("装備 / 持ち物") { FontSize = 20f, Color = new Color(235, 240, 250) } },
                                new PoppingButton { Label = "閉じる", OnPressed = () => _onClose() },
                            },
                        },
                        new Text("アイテムをドラッグして装備・入れ替え・持ち物へ戻す") { FontSize = 12f, Color = hint },
                        new Row
                        {
                            MainAxisSize = MainAxisSize.Min,
                            CrossAxisAlignment = CrossAxisAlignment.Start,
                            Spacing = 28f,
                            Children = new Widget[]
                            {
                                Section("装備", Equipment(), hint),
                                Section("持ち物", Inventory(), hint),
                            },
                        },
                    },
                },
            },
        };

        return new Positioned
        {
            Left = Dimension.Px(0f),
            Top = Dimension.Px(0f),
            Right = Dimension.Px(0f),
            Bottom = Dimension.Px(0f),
            Child = new Stack
            {
                Fit = StackFit.Expand,
                Children = new Widget[]
                {
                    new GestureDetector { OnTap = () => _onClose(), Child = new Container { Color = new Color(0, 0, 0, 130) } },
                    new Align { Alignment = Alignment.Center, Child = panel },
                },
            },
        };
    }

    private static Widget Section(string title, Widget body, Color hint) => new Column
    {
        MainAxisSize = MainAxisSize.Min,
        CrossAxisAlignment = CrossAxisAlignment.Start,
        Spacing = 8f,
        Children = new Widget[] { new Text(title) { FontSize = 13f, Color = hint }, body },
    };

    private Widget Equipment()
    {
        var rows = new Widget[_model.EquipCount];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new Row
            {
                MainAxisSize = MainAxisSize.Min,
                CrossAxisAlignment = CrossAxisAlignment.Center,
                Spacing = 10f,
                Children = new Widget[]
                {
                    new SizedBox { Width = Dimension.Px(40f), Child = new Text(KindLabel[i]) { FontSize = 13f, Color = new Color(186, 192, 204) } },
                    Slot(new SlotRef(true, i)),
                },
            };
        }

        return new Column { MainAxisSize = MainAxisSize.Min, CrossAxisAlignment = CrossAxisAlignment.Start, Spacing = 8f, Children = rows };
    }

    private Widget Inventory()
    {
        const int cols = 4;
        int total = _model.InventoryCount;
        int rowCount = (total + cols - 1) / cols;
        var rows = new Widget[rowCount];
        for (int r = 0; r < rowCount; r++)
        {
            var cells = new Widget[cols];
            for (int c = 0; c < cols; c++)
            {
                int idx = (r * cols) + c;
                cells[c] = idx < total ? Slot(new SlotRef(false, idx)) : new SizedBox { Width = Dimension.Px(60f), Height = Dimension.Px(60f) };
            }

            rows[r] = new Row { MainAxisSize = MainAxisSize.Min, Spacing = 8f, Children = cells };
        }

        return new Column { MainAxisSize = MainAxisSize.Min, Spacing = 8f, Children = rows };
    }

    // スロット＝ドロップ先（DragTarget<SlotRef>）兼 ドラッグ元（Draggable<SlotRef>）。型付きなので is/キャスト不要・他型のドラッグは自動で無視。
    // 中身は描画時にモデルを読むので構造は固定（ドロップで再構築しない）。ドラッグ中は元スロットを ChildWhenDragging で減光表示。
    private Widget Slot(SlotRef r) => new DragTarget<SlotRef>
    {
        HighlightColor = new Color(250, 210, 110),
        CanAccept = s => _model.CanPlace(s, r),
        OnAccept = s => _model.Move(s, r), // 反映は描画時に読む（再構築/再レイアウトなし）
        Child = new Draggable<SlotRef>
        {
            Data = r,
            Feedback = new SlotFace { Model = _model, Slot = r, Skin = _skins.SlotSelected },
            ChildWhenDragging = new Opacity { Value = 0.35f, Child = new SlotFace { Model = _model, Slot = r, Skin = _skins.Slot } },
            Child = new SlotFace { Model = _model, Slot = r, Skin = _skins.Slot, EmptyHint = r.Equipment ? KindHint[r.Index] : null },
        },
    };
}

/// <summary>Fixed size slot surface. <b>Read from model when drawing</b>(After dropping, it will not be rebuilt and will be reflected in the next drawing).</summary>
internal sealed class SlotFace : Widget
{
    public required LoadoutModel Model { get; init; }

    public required SlotRef Slot { get; init; }

    public required ImageSkin Skin { get; init; }

    /// <summary>Hints given when empty (e.g. type of equipment slot).</summary>
    public string? EmptyHint { get; init; }

    public float Size { get; init; } = 60f;

    public override Element CreateElement() => new SlotFaceElement(this);
}

internal sealed class SlotFaceElement : Element
{
    private readonly LayoutNode _node;

    public SlotFaceElement(SlotFace widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: _ => new Size(((SlotFace)Widget).Size, ((SlotFace)Widget).Size));
    }

    private SlotFace W => (SlotFace)Widget;

    public override LayoutNode LayoutNode => _node;

    public override void Paint(in PaintContext context)
    {
        Rect b = _node.Bounds;
        W.Skin.Paint(context, b);

        Item? item = W.Model.Get(W.Slot);
        string? icon = item?.Icon ?? W.EmptyHint;
        if (icon is null)
        {
            return;
        }

        const float fontSize = 24f;
        Vec2 m = context.MeasureText(icon, fontSize);
        Color color = item is not null ? new Color(236, 240, 250) : new Color(92, 100, 114);
        context.DrawText(icon, new Vec2(b.X + ((b.Width - m.X) / 2f), b.Y + ((b.Height - m.Y) / 2f)), fontSize, color);
    }
}
