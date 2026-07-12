using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for text input (editing model + key distribution + tap focus). </summary>
public class TextFieldTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 100);

    private static (HamonRoot host, TextEditingController ctrl, FocusNode node) MountField(bool autofocus, string initial = "", Action<string>? onSubmitted = null)
    {
        var host = new HamonRoot(new StubTextRenderer());
        var ctrl = new TextEditingController(initial);
        var node = new FocusNode();
        host.SetRoot(() => new TextField { Controller = ctrl, Node = node, Autofocus = autofocus, OnSubmitted = onSubmitted });
        host.Update(Viewport);
        return (host, ctrl, node);
    }

    [Fact]
    public void TypingInserts_AndMovesCaret()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: true);
        Assert.True(node.HasFocus);

        host.DispatchText('h');
        host.DispatchText('i');

        Assert.Equal("hi", ctrl.Text);
        Assert.Equal(2, ctrl.Caret);
    }

    [Fact]
    public void ControlChar_IsIgnored()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: true);
        host.DispatchText('\n');
        host.DispatchText('\t');
        Assert.Equal(string.Empty, ctrl.Text);
    }

    [Fact]
    public void CaretMove_Then_Backspace_RemovesBeforeCaret()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: true, initial: "abc");
        Assert.Equal(3, ctrl.Caret);

        host.DispatchEditKey(TextEditKey.Left); // caret=2
        host.DispatchEditKey(TextEditKey.Backspace); // 'b' を削除
        Assert.Equal("ac", ctrl.Text);
        Assert.Equal(1, ctrl.Caret);
    }

    [Fact]
    public void HomeEnd_Delete()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: true, initial: "abc");

        host.DispatchEditKey(TextEditKey.Home);
        Assert.Equal(0, ctrl.Caret);
        host.DispatchEditKey(TextEditKey.Delete); // 先頭 'a' を削除
        Assert.Equal("bc", ctrl.Text);

        host.DispatchEditKey(TextEditKey.End);
        Assert.Equal(2, ctrl.Caret);
    }

    [Fact]
    public void Enter_FiresSubmitted()
    {
        string? submitted = null;
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: true, initial: "go", onSubmitted: s => submitted = s);

        host.DispatchEditKey(TextEditKey.Enter);
        Assert.Equal("go", submitted);
    }

    [Fact]
    public void Tap_FocusesField_ThenReceivesInput()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: false);
        Assert.False(node.HasFocus);

        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Down, 0.01f));
        host.DispatchPointer(new PointerEvent(new Vec2(10, 10), PointerPhase.Up, 0.02f));
        Assert.True(node.HasFocus); // タップでフォーカス

        host.DispatchText('z');
        Assert.Equal("z", ctrl.Text);
    }

    [Fact]
    public void SetText_ClampsCaret()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var ctrl = new TextEditingController("abcde"); // caret=5
        ctrl.SetText("ab");
        Assert.Equal("ab", ctrl.Text);
        Assert.Equal(2, ctrl.Caret); // 範囲内へクランプ
    }

    [Fact]
    public void Composition_SetsPreedit_WithoutChangingText()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: true);

        host.DispatchComposition("にほん", 3); // IME 変換中（未確定）
        Assert.Equal("にほん", ctrl.Composition);
        Assert.Equal(3, ctrl.CompositionCaret);
        Assert.Equal(string.Empty, ctrl.Text); // 確定テキストはまだ変わらない
    }

    [Fact]
    public void Commit_AfterComposition_ClearsPreedit_AndInsertsText()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: true);

        host.DispatchComposition("にほん", 3);
        host.DispatchText('日'); // 変換確定（commit）
        host.DispatchText('本');

        Assert.Equal("日本", ctrl.Text);
        Assert.Equal(string.Empty, ctrl.Composition); // 変換中は確定で消える
        Assert.Equal(2, ctrl.Caret);
    }

    [Fact]
    public void Composition_CanceledByEmpty()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: true);
        host.DispatchComposition("にほん", 3);
        host.DispatchComposition(string.Empty, 0); // 変換取り消し
        Assert.Equal(string.Empty, ctrl.Composition);
        Assert.Equal(string.Empty, ctrl.Text);
    }

    [Fact]
    public void Unfocused_DoesNotReceiveInput()
    {
        (HamonRoot host, TextEditingController ctrl, FocusNode node) = MountField(autofocus: false);
        host.DispatchText('x'); // フォーカス無し＝配送先なし
        Assert.Equal(string.Empty, ctrl.Text);
    }
}
