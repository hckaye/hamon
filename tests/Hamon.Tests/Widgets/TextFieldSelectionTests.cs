using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Determinism test for TextField selection/clipboard/grapheme cluster unit caret.</summary>
public class TextFieldSelectionTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static TextEditingController Ctrl(string initial)
    {
        var host = new HamonRoot(new StubTextRenderer());
        return new TextEditingController(initial);
    }

    [Fact]
    public void SelectLeft_ExtendsSelection()
    {
        TextEditingController c = Ctrl("hello"); // caret=5
        c.SelectLeft();
        c.SelectLeft();
        Assert.True(c.HasSelection);
        Assert.Equal("lo", c.SelectedText);
        Assert.Equal(3, c.SelectionStart);
        Assert.Equal(5, c.SelectionEnd);
    }

    [Fact]
    public void SelectAll_ThenTyping_ReplacesAll()
    {
        TextEditingController c = Ctrl("hello");
        c.SelectAll();
        Assert.Equal("hello", c.SelectedText);
        c.Insert('x');
        Assert.Equal("x", c.Text);
        Assert.Equal(1, c.Caret);
        Assert.False(c.HasSelection);
    }

    [Fact]
    public void Backspace_WithSelection_DeletesRange()
    {
        TextEditingController c = Ctrl("hello");
        c.SelectToHome(); // 全体を選択（caret は 0 へ、anchor=5）
        c.Backspace();
        Assert.Equal(string.Empty, c.Text);
    }

    [Fact]
    public void MoveLeft_CollapsesSelectionToStart()
    {
        TextEditingController c = Ctrl("hello");
        c.SelectLeft(); // 選択 "o"（caret=4, anchor=5）
        c.SelectLeft(); // 選択 "lo"（caret=3）
        c.MoveLeft();   // 選択を畳んで開始（3）へ
        Assert.False(c.HasSelection);
        Assert.Equal(3, c.Caret);
    }

    [Fact]
    public void Copy_Paste_RoundTrips()
    {
        var clip = new InMemoryClipboard();
        TextEditingController c = Ctrl("hello");
        c.SelectToHome();   // 全選択
        c.Copy(clip);
        c.MoveRight();      // 畳む（caret=5＝末尾）
        c.Paste(clip);
        Assert.Equal("hellohello", c.Text);
        Assert.Equal(10, c.Caret);
    }

    [Fact]
    public void Cut_RemovesSelection_AndCopies()
    {
        var clip = new InMemoryClipboard();
        TextEditingController c = Ctrl("hello");
        c.SetSelection(0);
        c.SelectToEnd(); // anchor=0, caret=5
        c.Cut(clip);
        Assert.Equal(string.Empty, c.Text);
        Assert.Equal("hello", clip.GetText());
    }

    [Fact]
    public void Paste_OverSelection_Replaces()
    {
        var clip = new InMemoryClipboard();
        clip.SetText("XY");
        TextEditingController c = Ctrl("abcde");
        c.SetSelection(1);         // caret=1
        c.SetSelection(3, extend: true); // 選択 "bc"
        c.Paste(clip);
        Assert.Equal("aXYde", c.Text);
    }

    [Fact]
    public void SurrogatePair_MovesAndDeletesAsOneGrapheme()
    {
        // 😀 = U+1F600（サロゲートペア＝2 char）。1 回の左移動/削除で 1 絵文字。
        TextEditingController c = Ctrl("a😀b"); // length = 4 (a + 2 + b)
        Assert.Equal(4, c.Text.Length);
        c.End();           // caret=4
        c.MoveLeft();      // 'b' を越える → caret=3
        Assert.Equal(3, c.Caret);
        c.MoveLeft();      // 😀（2 char）を 1 単位で越える → caret=1
        Assert.Equal(1, c.Caret);
        c.Delete();        // 😀 を 1 回で消す（2 char 分）
        Assert.Equal("ab", c.Text);
    }

    [Fact]
    public void Backspace_DeletesWholeSurrogatePair()
    {
        TextEditingController c = Ctrl("😀"); // caret=2
        c.Backspace();
        Assert.Equal(string.Empty, c.Text); // 2 char をまとめて削除
    }
}
