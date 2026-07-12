using Hamon.Layout;
using Hamon.MonoGame;
using Hamon.Widgets;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Hamon.SampleApp3;

/// <summary>Procedurally generated texture<see cref="ImageSkin"/>(9-slice panel/slot/circle joystick).</summary>
public sealed class GameSkins
{
    private GameSkins(ImageSkin panel, ImageSkin slot, ImageSkin slotSelected, ImageSkin joyBase, ImageSkin joyKnob)
    {
        Panel = panel;
        Slot = slot;
        SlotSelected = slotSelected;
        JoyBase = joyBase;
        JoyKnob = joyKnob;
    }

    public ImageSkin Panel { get; }

    public ImageSkin Slot { get; }

    public ImageSkin SlotSelected { get; }

    public ImageSkin JoyBase { get; }

    public ImageSkin JoyKnob { get; }

    public static GameSkins Create(GraphicsDevice d)
    {
        ITexture panel = new MonoGameTexture(MakePanel(d, 64, 16, 3, new XnaColor(96, 116, 158), new XnaColor(28, 34, 46, 235)));
        ITexture slot = new MonoGameTexture(MakePanel(d, 56, 12, 3, new XnaColor(74, 88, 116), new XnaColor(22, 26, 36, 235)));
        ITexture slotSel = new MonoGameTexture(MakePanel(d, 56, 12, 3, new XnaColor(250, 200, 90), new XnaColor(62, 54, 28, 240)));
        ITexture joyBase = new MonoGameTexture(MakeCircle(d, 128, new XnaColor(30, 36, 50, 170), new XnaColor(84, 100, 136), 4));
        ITexture joyKnob = new MonoGameTexture(MakeCircle(d, 72, new XnaColor(120, 150, 210), new XnaColor(190, 212, 255), 3));

        return new GameSkins(
            new ImageSkin(panel, EdgeInsets.All(16f)),
            new ImageSkin(slot, EdgeInsets.All(12f)),
            new ImageSkin(slotSel, EdgeInsets.All(12f)),
            new ImageSkin(joyBase),
            new ImageSkin(joyKnob));
    }

    // 角丸の枠（外周 frame 色＋内側 fill 色＋外縁 AA）の 9-slice 用テクスチャ。
    private static Texture2D MakePanel(GraphicsDevice device, int size, int radius, int frame, XnaColor frameColor, XnaColor fillColor)
    {
        var data = new XnaColor[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = RoundedBoxSdf(x + 0.5f - half, y + 0.5f - half, half - 1f, half - 1f, radius);
                XnaColor c;
                if (d >= 0f)
                {
                    float a = System.Math.Clamp(1f + d, 0f, 1f); // 外縁 1px AA
                    c = new XnaColor(frameColor.R, frameColor.G, frameColor.B, (byte)(frameColor.A * (1f - a)));
                }
                else if (d > -frame)
                {
                    c = frameColor;
                }
                else
                {
                    c = fillColor;
                }

                data[(y * size) + x] = c;
            }
        }

        var tex = new Texture2D(device, size, size);
        tex.SetData(data);
        return tex;
    }

    // 円（fill＋外周 ring＋外縁 AA）。ジョイスティックのベース/ノブ用。
    private static Texture2D MakeCircle(GraphicsDevice device, int size, XnaColor fill, XnaColor ring, int ringWidth)
    {
        var data = new XnaColor[size * size];
        float r = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float dist = System.MathF.Sqrt((dx * dx) + (dy * dy)) - (r - 1f);
                XnaColor c;
                if (dist >= 0f)
                {
                    float a = System.Math.Clamp(1f - dist, 0f, 1f);
                    c = new XnaColor(ring.R, ring.G, ring.B, (byte)(ring.A * a));
                }
                else if (dist > -ringWidth)
                {
                    c = ring;
                }
                else
                {
                    c = fill;
                }

                data[(y * size) + x] = c;
            }
        }

        var tex = new Texture2D(device, size, size);
        tex.SetData(data);
        return tex;
    }

    private static float RoundedBoxSdf(float px, float py, float bx, float by, float radius)
    {
        float qx = System.MathF.Abs(px) - bx + radius;
        float qy = System.MathF.Abs(py) - by + radius;
        float ax = System.MathF.Max(qx, 0f);
        float ay = System.MathF.Max(qy, 0f);
        return System.MathF.Sqrt((ax * ax) + (ay * ay)) + System.MathF.Min(System.MathF.Max(qx, qy), 0f) - radius;
    }
}

/// <summary>A clock that advances the time every frame (used for gauge "breathing" and animation/no reconstruction).</summary>
public sealed class HudClock : ITicker
{
    public float T { get; private set; }

    public bool Tick(float dtSeconds)
    {
        T += dtSeconds;
        return true;
    }
}

/// <summary>
/// Mock game HUD (declarative).
/// An example of a game UI that combines menu buttons that animate their states, sound switches, etc. on one screen.
/// </summary>
public sealed class GameHud
{
    private readonly HamonRoot _host;
    private readonly GameSkins _skins;
    private readonly HudClock _clock = new();
    private readonly CooldownState _cooldowns = new(4, 3f);
    private readonly FxController _fx = new();
    private readonly LoadoutModel _loadout = new();

    public GameHud(HamonRoot host, GameSkins skins)
    {
        _host = host;
        _skins = skins;
        _host.RegisterTicker(_clock);
        _host.RegisterTicker(_cooldowns);
        _host.RegisterTicker(_fx);
    }

    public Widget Root => new HudView(_host, _skins, _clock, _cooldowns, _fx, _loadout);
}

/// <summary>Cooldown per slot (fills on press, goes to 0 on time; when drawing<see cref="Remaining"/>(no reconstruction).</summary>
public sealed class CooldownState : ITicker
{
    private readonly float[] _cd;
    private readonly float _duration;

    public CooldownState(int count, float duration)
    {
        _cd = new float[count];
        _duration = duration;
    }

    /// <summary>Remaining cooldown (1=immediately after use, 0=can be used).</summary>
    public float Remaining(int i) => _duration <= 0f ? 0f : _cd[i] / _duration;

    public void Trigger(int i) => _cd[i] = _duration;

    public bool Tick(float dtSeconds)
    {
        for (int i = 0; i < _cd.Length; i++)
        {
            if (_cd[i] > 0f)
            {
                _cd[i] = System.MathF.Max(0f, _cd[i] - dtSeconds);
            }
        }

        return true;
    }
}

/// <summary>Cooldown blackout curtain (cover remaining amount from above/read getter when drawing = zero reconstruction). </summary>
public sealed class CooldownCover : Widget
{
    public required Func<float> Value { get; init; }

    public float Radius { get; init; } = 8f;

    public override Element CreateElement() => new CooldownCoverElement(this);
}

internal sealed class CooldownCoverElement : Element
{
    private readonly LayoutNode _node;

    public CooldownCoverElement(CooldownCover widget)
        : base(widget)
    {
        _node = new LayoutNode(new Style { Kind = LayoutKind.Stack, StackExpandChildren = true });
    }

    public override LayoutNode LayoutNode => _node;

    public override void Paint(in PaintContext context)
    {
        float v = System.Math.Clamp(((CooldownCover)Widget).Value(), 0f, 1f);
        if (v <= 0f)
        {
            return;
        }

        Rect b = _node.Bounds;
        // スライドする下辺は常に直線にする：角丸矩形を残量高+半径で描いて残量高でクリップ＝矩形自身の下隅角丸は
        // クリップ線より下に押し出されて切れる（残量が高く下隅ゾーンに入っても境界は水平直線・上隅だけ角丸）。
        float r = ((CooldownCover)Widget).Radius;
        float h = b.Height * v;
        object? clip = context.PushClip(new Rect(b.X, b.Y, b.Width, h));
        context.FillRoundedRect(new Rect(b.X, b.Y, b.Width, h + r), new Color(0, 0, 0, 150), r);
        context.PopClip(clip);
    }
}

internal sealed class HudView : HookWidget
{
    private readonly HamonRoot _host;
    private readonly GameSkins _skins;
    private readonly HudClock _clock;
    private readonly CooldownState _cooldowns;
    private readonly FxController _fx;
    private readonly LoadoutModel _loadout;

    private static readonly string[] SlotIcons = { "剣", "盾", "術", "薬" };
    private static readonly int[] SlotCounts = { 1, 1, 1, 5 };

    public HudView(HamonRoot host, GameSkins skins, HudClock clock, CooldownState cooldowns, FxController fx, LoadoutModel loadout)
    {
        _host = host;
        _skins = skins;
        _clock = clock;
        _cooldowns = cooldowns;
        _fx = fx;
        _loadout = loadout;
    }

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        HookState<int> selected = hooks.UseState(0);
        HookState<bool> sound = hooks.UseState(true);
        HookState<bool> inventory = hooks.UseState(false);

        var pops = new AnimationController[SlotIcons.Length];
        for (int i = 0; i < pops.Length; i++)
        {
            pops[i] = hooks.UseAnimation(0.22f, Curves.EaseOutBack);
        }

        return new Stack
        {
            Fit = StackFit.Expand,
            Children = new Widget[]
            {
                StatusPanel(theme),
                TopRight(theme, sound, inventory),
                Joystick(),
                ActionBar(theme, selected, pops),
                AttackArea(),
                Minimap(),
                inventory.Value ? new LoadoutView(_loadout, _skins, () => inventory.Value = false) : new SizedBox(),
                new FxLayer { Controller = _fx },
                new DragLayer(), // ドラッグ追従の見た目を最前面に（開始時の全再構築＝カクつきを回避）
            },
        };
    }

    // 中央：丸をタップで攻撃＝ダメージ数字＋パーティクルをタップした位置に出す（FX は宣言ツリー外＝再構築なし）。
    private Widget AttackArea() => new Positioned
    {
        Left = Dimension.Px(0f),
        Top = Dimension.Px(0f),
        Right = Dimension.Px(0f),
        Bottom = Dimension.Px(0f),
        Child = new Align { Alignment = Alignment.Center, Child = new AttackTarget { Fx = _fx } },
    };

    // 左上：9-slice パネル＋プレイヤー名＋HP/MP ゲージ（時計で軽く呼吸）。
    private Widget StatusPanel(HamonTheme theme) => new Positioned
    {
        Left = Dimension.Px(20f),
        Top = Dimension.Px(20f),
        Width = Dimension.Px(280f),
        Child = new Material
        {
            Skin = _skins.Panel,
            Child = new Container
            {
                Padding = EdgeInsets.All(16f),
                Child = new Column
                {
                    CrossAxisAlignment = CrossAxisAlignment.Stretch,
                    MainAxisSize = MainAxisSize.Min,
                    Spacing = 8f,
                    Children = new Widget[]
                    {
                        new Text("勇者 Lv.27") { FontSize = 18f, Color = new Color(235, 240, 250) },
                        Gauge("HP", new Color(232, 84, 96), () => 0.62f + (0.05f * MathF.Sin(_clock.T * 1.7f))),
                        Gauge("MP", new Color(96, 150, 240), () => 0.45f + (0.04f * MathF.Sin(_clock.T * 1.1f))),
                    },
                },
            },
        },
    };

    private static Widget Gauge(string label, Color color, Func<float> value) => new Row
    {
        CrossAxisAlignment = CrossAxisAlignment.Center,
        Spacing = 8f,
        Children = new Widget[]
        {
            new SizedBox { Width = Dimension.Px(28f), Child = new Text(label) { FontSize = 12f, Color = new Color(180, 190, 205) } },
            new Expanded { Child = new ProgressBar { ValueGetter = value, Fill = color, Height = 12f } },
        },
    };

    // 右上：状態でアニメするメニューボタン＋サウンドのスイッチ＋持ち物（全幅 Positioned＋Align で右寄せ）。
    private Widget TopRight(HamonTheme theme, HookState<bool> sound, HookState<bool> inventory) => new Positioned
    {
        Left = Dimension.Px(0f),
        Right = Dimension.Px(0f),
        Top = Dimension.Px(20f),
        Child = new Align
        {
            Alignment = Alignment.TopRight,
            Child = new Padding
            {
                Insets = new EdgeInsets(0f, 0f, 20f, 0f),
                Child = new Row
                {
                    MainAxisSize = MainAxisSize.Min,
                    CrossAxisAlignment = CrossAxisAlignment.Center,
                    Spacing = 12f,
                    Children = new Widget[]
                    {
                        new Text("♪") { FontSize = 18f, Color = sound.Value ? theme.Primary : new Color(120, 128, 140) },
                        new Switch { Value = sound.Value, OnChanged = v => sound.Value = v },
                        new PoppingButton { Label = "持ち物", OnPressed = () => inventory.Value = !inventory.Value },
                        new PoppingButton { Label = "メニュー", OnPressed = () => _host.ShowToast("メニューを開く") },
                    },
                },
            },
        },
    };

    // 右下：ミニマップ（SceneView で生描画＝プレイヤー＋時間で周回する敵ドット。描画時に読むので再構築なし）。
    private Widget Minimap() => new Positioned
    {
        Right = Dimension.Px(20f),
        Bottom = Dimension.Px(20f),
        Width = Dimension.Px(170f),
        Height = Dimension.Px(170f),
        Child = new Material
        {
            Skin = _skins.Panel,
            Child = new Container
            {
                Padding = EdgeInsets.All(10f),
                Child = new SceneView
                {
                    Width = Dimension.Percent(100f),
                    Height = Dimension.Percent(100f),
                    OnDraw = s => DrawMinimap(s, _clock.T),
                },
            },
        },
    };

    private static void DrawMinimap(in SceneDrawContext scene, float t)
    {
        IPainter p = scene.Painter;
        Rect b = scene.Bounds;
        var center = new Vec2(b.X + (b.Width / 2f), b.Y + (b.Height / 2f));

        // 薄いグリッド。
        var grid = new Color(255, 255, 255, 22);
        for (int k = 1; k < 4; k++)
        {
            float fx = b.X + (b.Width * k / 4f);
            float fy = b.Y + (b.Height * k / 4f);
            p.DrawLine(new Vec2(fx, b.Y), new Vec2(fx, b.Bottom), 1f, grid);
            p.DrawLine(new Vec2(b.X, fy), new Vec2(b.Right, fy), 1f, grid);
        }

        // プレイヤー（中心の緑ブリップ＋向き）。
        p.FillCircle(center, 5f, new Color(120, 230, 150));
        p.DrawLine(center, new Vec2(center.X + (12f * MathF.Cos(t * 0.8f)), center.Y + (12f * MathF.Sin(t * 0.8f))), 2f, new Color(120, 230, 150));

        // 時間で周回する敵/POI ドット。
        for (int i = 0; i < 3; i++)
        {
            float ang = (t * 0.6f) + (i * 2.1f);
            float r = 28f + (i * 16f);
            var pos = new Vec2(center.X + (MathF.Cos(ang) * r), center.Y + (MathF.Sin(ang) * r));
            p.FillCircle(pos, 3.5f, i == 0 ? new Color(240, 220, 110) : new Color(230, 96, 96));
        }
    }

    // 左下：画像スキンの仮想スティック。
    private Widget Joystick() => new Positioned
    {
        Left = Dimension.Px(40f),
        Bottom = Dimension.Px(40f),
        Child = new VirtualJoystick
        {
            Size = 150f,
            KnobSize = 64f,
            BaseSkin = _skins.JoyBase,
            KnobSkin = _skins.JoyKnob,
            OnChanged = _ => { },
        },
    };

    // 下中央：アクションスロット（9-slice 枠・選択ハイライト・押下で弾む＝OnStateChanged）。
    private Widget ActionBar(HamonTheme theme, HookState<int> selected, AnimationController[] pops)
    {
        var slots = new Widget[SlotIcons.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            AnimationController pop = pops[i];
            slots[i] = new Transform
            {
                Origin = Alignment.Center,
                ScaleGetter = () => 1f + (0.12f * pop.Curved),
                Child = new Stack
                {
                    Width = Dimension.Px(64f),
                    Height = Dimension.Px(64f),
                    Fit = StackFit.Expand,
                    Children = new Widget[]
                    {
                        new SlotButton
                        {
                            Size = 64f,
                            Icon = new Text(SlotIcons[i]) { FontSize = 26f, Color = new Color(235, 240, 250) },
                            Count = SlotCounts[i],
                            Selected = selected.Value == index,
                            FrameSkin = _skins.Slot,
                            SelectedFrameSkin = _skins.SlotSelected,
                            OnPressed = () =>
                            {
                                selected.Value = index;
                                _cooldowns.Trigger(index); // 使用＝クールダウン開始
                            },
                            OnStateChanged = s =>
                            {
                                if (s.Has(WidgetState.Pressed))
                                {
                                    pop.JumpTo(0f);
                                    pop.Forward();
                                }
                            },
                        },
                        new CooldownCover { Value = () => _cooldowns.Remaining(index), Radius = 8f },
                    },
                },
            };
        }

        return new Positioned
        {
            Left = Dimension.Px(0f),
            Right = Dimension.Px(0f),
            Bottom = Dimension.Px(40f),
            Child = new Align
            {
                Alignment = Alignment.BottomCenter,
                Child = new Row { MainAxisSize = MainAxisSize.Min, Spacing = 12f, Children = slots },
            },
        };
    }
}

/// <summary>A button that animates on state (color changes on hover = Builder/bounces on press = OnStateChanged).</summary>
public sealed class PoppingButton : HookWidget
{
    public string Label { get; init; } = string.Empty;

    public Action? OnPressed { get; init; }

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        AnimationController pop = hooks.UseAnimation(0.22f, Curves.EaseOutBack);

        return new Button
        {
            Node = hooks.UseFocusNode(),
            OnPressed = OnPressed,
            OnStateChanged = state =>
            {
                if (state.Has(WidgetState.Pressed))
                {
                    pop.JumpTo(0f);
                    pop.Forward();
                }
            },
            Builder = state => new Transform
            {
                Origin = Alignment.Center,
                ScaleGetter = () => 1f + (0.14f * pop.Curved),
                Child = new Container
                {
                    Color = state.Has(WidgetState.Hovered) ? theme.Primary : theme.SurfaceVariant,
                    Radius = theme.Radius,
                    Padding = EdgeInsets.Symmetric(20f, 12f),
                    Child = new Text(Label)
                    {
                        FontSize = 16f,
                        Color = state.Has(WidgetState.Hovered) ? theme.OnPrimary : theme.OnSurface,
                    },
                },
            },
        };
    }
}

/// <summary>Attack by tapping = target that emits damage number + particles at the tapped position (FX demo).</summary>
public sealed class AttackTarget : Widget
{
    public required FxController Fx { get; init; }

    public override Element CreateElement() => new AttackTargetElement(this);
}

internal sealed class AttackTargetElement : Element
{
    private readonly LayoutNode _node;
    private int _hits;

    public AttackTargetElement(AttackTarget widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: static _ => new Size(96f, 96f));
    }

    private AttackTarget W => (AttackTarget)Widget;

    public override LayoutNode LayoutNode => _node;

    public override bool WantsPointer => true;

    public override void HandlePointer(in PointerEvent pointer)
    {
        if (pointer.Phase != PointerPhase.Up || !_node.Bounds.Contains(pointer.Position.X, pointer.Position.Y))
        {
            return;
        }

        Vec2 hit = pointer.Position; // タップした座標に出す（カーソル追従）
        _hits++;
        bool crit = _hits % 4 == 0;
        int dmg = 80 + ((_hits * 17) % 120);

        W.Fx.SpawnText(
            new Vec2(hit.X, hit.Y - 12f),
            crit ? dmg + "!!" : dmg.ToString(),
            crit ? new Color(255, 120, 96) : new Color(255, 226, 124),
            size: crit ? 34f : 26f,
            life: 1.1f,
            rise: 72f);
        W.Fx.SpawnBurst(hit, crit ? 22 : 12, crit ? new Color(255, 140, 96) : new Color(255, 202, 112), speed: crit ? 180f : 130f, life: 0.7f, additive: crit);
    }

    public override void Paint(in PaintContext context)
    {
        Rect b = _node.Bounds;
        var center = new Vec2(b.X + (b.Width / 2f), b.Y + (b.Height / 2f));
        float r = b.Width / 2f;
        context.FillCircle(center, r, new Color(58, 40, 46, 190));
        context.FillCircle(center, r * 0.64f, new Color(150, 64, 70, 220));
        context.FillCircle(center, r * 0.3f, new Color(228, 92, 92));
    }
}
