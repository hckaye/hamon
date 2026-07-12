using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// IME input guard (<see cref="HamonRoot.IsImeActive"/>) determinism test.
/// In order to suppress (MoveFocus/Submit), make sure that it becomes true/false depending on the presence or absence of composition.
/// </summary>
public class ImeGuardTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static readonly Size Viewport = new(300, 100);

    private static (HamonRoot host, TextEditingController ctrl) Mount()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var ctrl = new TextEditingController();
        host.SetRoot(() => new TextField { Controller = ctrl, Node = new FocusNode(), Autofocus = true });
        host.Update(Viewport);
        return (host, ctrl);
    }

    [Fact]
    public void Composition_KeepsImeActive_BeyondFrameGuard()
    {
        (HamonRoot host, _) = Mount();

        host.DispatchComposition("にほん", 3);
        Assert.True(host.IsImeActive);

        // フレームガード（数フレーム）を超えて Update しても、変換中である限り true を維持。
        for (int i = 0; i < 10; i++)
        {
            host.Update(Viewport, 0.016f);
        }

        Assert.True(host.IsImeActive); // composition がある限り抑止が続く
    }

    [Fact]
    public void Commit_ClearsImeActive()
    {
        (HamonRoot host, TextEditingController ctrl) = Mount();

        host.DispatchComposition("にほん", 3);
        Assert.True(host.IsImeActive);

        host.DispatchText('日'); // 確定
        // フレームガードが切れるまで進める。
        for (int i = 0; i < 5; i++)
        {
            host.Update(Viewport, 0.016f);
        }

        Assert.False(host.IsImeActive);
        Assert.Equal("日", ctrl.Text);
    }

    [Fact]
    public void Cancel_ClearsImeActive()
    {
        (HamonRoot host, TextEditingController ctrl) = Mount();

        host.DispatchComposition("にほん", 3);
        host.DispatchComposition(string.Empty, 0); // 変換取り消し
        for (int i = 0; i < 5; i++)
        {
            host.Update(Viewport, 0.016f);
        }

        Assert.False(host.IsImeActive);
        Assert.Equal(string.Empty, ctrl.Text);
    }

    [Fact]
    public void Composition_OverSelection_ReplacesOnCommit()
    {
        (HamonRoot host, TextEditingController ctrl) = Mount();
        ctrl.SetText("abc");
        ctrl.SelectAll(); // 全選択

        // 変換確定で選択が置換される（IME 確定は InsertString 経由）。
        host.DispatchText('X');
        Assert.Equal("X", ctrl.Text);
    }
}
