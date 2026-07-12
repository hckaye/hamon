using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Headless Smoke: Driving a screen with a complete widget catalog in multiple frames (dt progress, pointer, hover,
/// Gamepad/keyboard/theme switching) and verify stable operation without exception.
/// Confirm determinism (same input = same drawing = golden foundation).
/// </summary>
public class HeadlessSmokeTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    // 描画コマンドを文字列化して記録する painter（draw-command golden の土台＝ピクセル不要）。
    private sealed class DrawRecorder : IPainter
    {
        private readonly StringBuilder _sb = new();

        public string Commands => _sb.ToString();

        public void BeginFrame() => _sb.Append("begin\n");

        public void EndFrame() => _sb.Append("end\n");

        public void FillRect(Rect r, Color c) => _sb.Append("rect ").Append(Fmt(r)).Append(' ').Append(Fmt(c)).Append('\n');

        public void FillRoundedRect(Rect r, Color c, float radius) => _sb.Append("rrect ").Append(Fmt(r)).Append(' ').Append(Fmt(c)).Append(' ').Append((int)radius).Append('\n');

        public void DrawTexture(ITexture t, Rect dest, RectInt src, Color tint) => _sb.Append("tex ").Append(Fmt(dest)).Append('\n');

        public object? PushClip(Rect r)
        {
            _sb.Append("pushclip ").Append(Fmt(r)).Append('\n');
            return null;
        }

        public void PopClip(object? token) => _sb.Append("popclip\n");

        private static string Fmt(Rect r) => $"({(int)r.X},{(int)r.Y},{(int)r.Width},{(int)r.Height})";

        private static string Fmt(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
    }

    private static readonly Size Viewport = new(360, 640);

    private static Widget BuildRichScreen(TabController tabs, TextEditingController text, ScrollController list)
    {
        return new Column
        {
            Children = new Widget[]
            {
                new SafeArea
                {
                    Child = new Tabs
                    {
                        Controller = tabs,
                        Items = new[]
                        {
                            new TabItem(new Text("Home") { FontSize = 14 }, () => new SizedBox()),
                            new TabItem(new Text("Items") { FontSize = 14 }, () => new SizedBox()),
                        },
                    },
                },
                new Wrap
                {
                    Spacing = 6f,
                    RunSpacing = 6f,
                    Children = new Widget[]
                    {
                        new Button { OnPressed = () => { }, Background = new Color(40, 44, 54), Child = new Text("A") },
                        new Checkbox { Value = true, OnChanged = _ => { } },
                        new Switch { Value = false, OnChanged = _ => { } },
                        new Radio<int> { Value = 1, GroupValue = 1, OnChanged = _ => { } },
                        new SlotButton { Count = 3, OnPressed = () => { } },
                        new CooldownButton { Progress = 0.5f, OnPressed = () => { } },
                    },
                },
                new Slider { Value = 0.5f, OnChanged = _ => { } },
                new TextField { Controller = text, Node = new FocusNode(), Placeholder = "Search" },
                new Dropdown<int>
                {
                    Value = 1,
                    OnChanged = _ => { },
                    Items = new[]
                    {
                        new DropdownItem<int>(1, new Text("One") { FontSize = 13 }),
                        new DropdownItem<int>(2, new Text("Two") { FontSize = 13 }),
                    },
                },
                new SizedBox
                {
                    Width = Dimension.Px(360),
                    Height = Dimension.Px(200),
                    Child = new ListView
                    {
                        ItemCount = 500,
                        ItemExtent = 40f,
                        Controller = list,
                        Builder = i => new Button { OnPressed = () => { }, Child = new Text($"row {i}") { FontSize = 13 } },
                    },
                },
            },
        };
    }

    [Fact]
    public void RichScreen_ManyFrames_MixedInput_NoCrash()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var tabs = new TabController(2);
        var text = new TextEditingController();
        var listCtrl = new ScrollController();
        host.SetRoot(() => BuildRichScreen(tabs, text, listCtrl));
        var painter = new DrawRecorder();

        for (int frame = 0; frame < 120; frame++)
        {
            host.Update(Viewport, 0.016f);
            host.Render(painter);

            // 多様な入力を毎フレーム織り交ぜる。
            host.DispatchHover(new Vec2(frame % 360, (frame * 3) % 640));
            host.DispatchPointer(new PointerEvent(new Vec2(60, 120), PointerPhase.Down, frame * 0.016f));
            host.DispatchPointer(new PointerEvent(new Vec2(60, 120 + (frame % 5)), PointerPhase.Move, (frame * 0.016f) + 0.005f));
            host.DispatchPointer(new PointerEvent(new Vec2(60, 121), PointerPhase.Up, (frame * 0.016f) + 0.01f));
            host.DispatchScroll(new Vec2(180, 500), frame % 2 == 0 ? -20f : 20f);

            if (frame % 7 == 0)
            {
                host.HandleButtonDown(GamepadButton.DpadDown);
                host.HandleButtonDown(GamepadButton.A);
                host.MoveNext();
            }

            if (frame % 11 == 0)
            {
                host.DispatchText('x');
                host.DispatchEditKey(TextEditKey.Backspace);
            }

            if (frame == 60)
            {
                host.Theme = HamonTheme.Light; // 実行時テーマ切替
                tabs.Select(1);
            }
        }

        Assert.NotNull(host.Root); // ここまで例外なく到達＝スモーク成功
    }

    [Fact]
    public void DrawCommands_AreDeterministic_AcrossIdenticalRenders()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var tabs = new TabController(2);
        var text = new TextEditingController("hello");
        var listCtrl = new ScrollController();
        host.SetRoot(() => BuildRichScreen(tabs, text, listCtrl));
        host.Update(Viewport); // 静的レイアウト（dt 無し＝アニメ非進行）

        var a = new DrawRecorder();
        var b = new DrawRecorder();
        host.Render(a);
        host.Render(b);

        Assert.False(string.IsNullOrEmpty(a.Commands));
        Assert.Equal(a.Commands, b.Commands); // 同一状態＝同一描画コマンド列（golden の前提）
    }

    [Fact]
    public void GoldenSnapshot_KnownUi_MatchesExpectedShape()
    {
        var host = new HamonRoot(new StubTextRenderer()) { Theme = HamonTheme.Dark };
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Container
            {
                Width = Dimension.Px(80),
                Height = Dimension.Px(40),
                Radius = 6f,
                Color = new Color(10, 20, 30, 255),
            },
        });
        host.Update(new Size(200, 200));

        var rec = new DrawRecorder();
        host.Render(rec);

        // 既知 UI の描画コマンドに、その角丸矩形（golden 断片）が含まれる。
        Assert.Contains("rrect (0,0,80,40) #0A141EFF 6", rec.Commands);
    }
}
