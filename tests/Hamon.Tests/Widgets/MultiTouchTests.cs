using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Deterministic test of multi-touch (independent capture by pointer ID = VPad + skill button simultaneous operation) and pinch (two-finger scale/rotation).
/// In the era of single pointers, the second Down took away the capture of the first = simultaneous operation impossible.
/// </summary>
public class MultiTouchTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static SizedBox Box(float w, float h) =>
        new() { Width = Dimension.Px(w), Height = Dimension.Px(h) };

    // 左右に並ぶ2要素（各 100x100）を CrossAxisAlignment.Start で y[0,100] に置く。左=x[0,100]／右=x[100,200]。
    private static HamonRoot MountRow(Widget left, Widget right)
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Row
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new[] { left, right },
        });
        host.Update(new Size(400, 200));
        return host;
    }

    [Fact]
    public void TwoFingers_DriveTwoWidgetsIndependently()
    {
        int aDowns = 0;
        int aTaps = 0;
        int aCancels = 0;
        int bTaps = 0;

        HamonRoot host = MountRow(
            new GestureDetector { OnTapDown = () => aDowns++, OnTap = () => aTaps++, OnTapCancel = () => aCancels++, Child = Box(100, 100) },
            new GestureDetector { OnTap = () => bTaps++, Child = Box(100, 100) });

        // 指1（ID1）が左を押さえて保持。指2（ID2）が右をタップ。指1を離す＝左タップ成立。
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0.1f, 1));
        host.DispatchPointer(new PointerEvent(new Vec2(150, 50), PointerPhase.Down, 0.2f, 2));
        host.DispatchPointer(new PointerEvent(new Vec2(150, 50), PointerPhase.Up, 0.3f, 2));
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Up, 0.4f, 1));

        Assert.Equal(1, aDowns);
        Assert.Equal(1, aTaps);   // 指2が割り込んでも左のキャプチャは奪われない（旧モデルなら 0）
        Assert.Equal(0, aCancels);
        Assert.Equal(1, bTaps);
    }

    [Fact]
    public void VirtualJoystickAndButton_WorkSimultaneously()
    {
        var lastDir = Vec2.Zero;
        int active = 0;
        int pressed = 0;

        HamonRoot host = MountRow(
            new VirtualJoystick { Size = 100, KnobSize = 40, OnChanged = d => lastDir = d, OnActiveChanged = a => { if (a) active++; } },
            new Button { Node = new FocusNode(), OnPressed = () => pressed++, Child = Box(100, 100) });

        // 指A：スティックを掴んで右へドラッグ（dir.X>0）。
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0.1f, 1));
        host.DispatchPointer(new PointerEvent(new Vec2(80, 50), PointerPhase.Move, 0.12f, 1));
        Assert.Equal(1, active);
        Assert.True(lastDir.X > 0f, $"dir.X={lastDir.X}");

        // 指B：別スロットでボタンをタップ（同時押し）。
        host.DispatchPointer(new PointerEvent(new Vec2(150, 50), PointerPhase.Down, 0.2f, 2));
        host.DispatchPointer(new PointerEvent(new Vec2(150, 50), PointerPhase.Up, 0.3f, 2));
        Assert.Equal(1, pressed);

        // 指Aは引き続きスティックを制御（左へ動かすと dir.X<0）。
        host.DispatchPointer(new PointerEvent(new Vec2(30, 50), PointerPhase.Move, 0.4f, 1));
        Assert.True(lastDir.X < 0f, $"dir.X after={lastDir.X}");
    }

    [Fact]
    public void VirtualJoystick_IgnoresSecondFingerOnIt()
    {
        var lastDir = Vec2.Zero;
        // 左に置いて中心 (50,50) を確定（単体ルートだと Stack 中央寄せで中心が動くため Row で左上に固定）。
        HamonRoot host = MountRow(
            new VirtualJoystick { Size = 100, KnobSize = 40, OnChanged = d => lastDir = d },
            Box(100, 100));

        // 指A が掴んで右へ（dir.X=1）。
        host.DispatchPointer(new PointerEvent(new Vec2(50, 50), PointerPhase.Down, 0.1f, 1));
        host.DispatchPointer(new PointerEvent(new Vec2(80, 50), PointerPhase.Move, 0.12f, 1));
        Assert.Equal(1f, lastDir.X, 2);
        Vec2 controlledByA = lastDir;

        // 指B が同じスティック上に乗っても無視＝OnChanged は発火せず方向は指A のまま。
        host.DispatchPointer(new PointerEvent(new Vec2(20, 50), PointerPhase.Down, 0.2f, 2));
        host.DispatchPointer(new PointerEvent(new Vec2(60, 50), PointerPhase.Move, 0.22f, 2));
        Assert.Equal(controlledByA.X, lastDir.X, 4); // 指B に揺さぶられない
    }

    [Fact]
    public void Pinch_TwoFingersSpread_ReportsScaleAndEnds()
    {
        int starts = 0;
        int ends = 0;
        int taps = 0;
        float lastScale = 1f;

        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector
        {
            OnScaleStart = _ => starts++,
            OnScaleUpdate = d => lastScale = d.Scale,
            OnScaleEnd = _ => ends++,
            OnTap = () => taps++,
            Child = Box(200, 200),
        });
        host.Update(new Size(200, 200));

        // 2本指 Down（距離 40）→ ピンチ開始。
        host.DispatchPointer(new PointerEvent(new Vec2(80, 100), PointerPhase.Down, 0.1f, 1));
        host.DispatchPointer(new PointerEvent(new Vec2(120, 100), PointerPhase.Down, 0.11f, 2));
        Assert.Equal(1, starts);

        // 広げて距離 80 → scale ≈ 2。
        host.DispatchPointer(new PointerEvent(new Vec2(60, 100), PointerPhase.Move, 0.12f, 1));
        host.DispatchPointer(new PointerEvent(new Vec2(140, 100), PointerPhase.Move, 0.13f, 2));
        Assert.Equal(2f, lastScale, 2);

        // 片方を離す＝ピンチ終了。もう片方を離してもタップにはならない。
        host.DispatchPointer(new PointerEvent(new Vec2(60, 100), PointerPhase.Up, 0.2f, 1));
        Assert.Equal(1, ends);
        host.DispatchPointer(new PointerEvent(new Vec2(140, 100), PointerPhase.Up, 0.21f, 2));
        Assert.Equal(0, taps);
    }

    [Fact]
    public void Pinch_Rotate_ReportsRotation()
    {
        float lastRotation = 0f;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector
        {
            OnScaleUpdate = d => lastRotation = d.Rotation,
            Child = Box(300, 300),
        });
        host.Update(new Size(300, 300));

        // 水平に並ぶ2点（角度0）→ 2本目を真下へ動かして角度 +π/2。
        host.DispatchPointer(new PointerEvent(new Vec2(100, 100), PointerPhase.Down, 0.1f, 1));
        host.DispatchPointer(new PointerEvent(new Vec2(200, 100), PointerPhase.Down, 0.11f, 2));
        host.DispatchPointer(new PointerEvent(new Vec2(100, 200), PointerPhase.Move, 0.12f, 2));

        Assert.Equal(MathF.PI / 2f, lastRotation, 2);
    }

    [Fact]
    public void Pinch_SuppressesTap_FiresTapCancel()
    {
        int taps = 0;
        int cancels = 0;
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GestureDetector
        {
            OnTap = () => taps++,
            OnTapCancel = () => cancels++,
            OnScaleUpdate = _ => { },
            Child = Box(200, 200),
        });
        host.Update(new Size(200, 200));

        host.DispatchPointer(new PointerEvent(new Vec2(80, 100), PointerPhase.Down, 0.1f, 1)); // タップ候補
        host.DispatchPointer(new PointerEvent(new Vec2(120, 100), PointerPhase.Down, 0.11f, 2)); // 2本目＝ピンチ昇格でタップ打ち切り

        Assert.Equal(1, cancels);
        Assert.Equal(0, taps);
    }
}
