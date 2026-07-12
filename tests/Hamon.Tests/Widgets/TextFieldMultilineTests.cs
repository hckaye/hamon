using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>TextField Deterministic test for multiple lines (line navigation, Enter line break, line number tracking height).</summary>
public class TextFieldMultilineTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        // 1 文字 = 10px 幅、行高 20px の決定論スタブ。
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * 10f, 20f);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static (HamonRoot host, TextEditingController ctrl, FocusNode node) MountMultiline(string initial, int maxLines = 6)
    {
        var host = new HamonRoot(new StubTextRenderer());
        var ctrl = new TextEditingController(initial);
        var node = new FocusNode();
        // Center で包む＝loose 制約でフィールドが「測った自然な高さ」になる（root 直下だと tight 高で潰れる）。
        host.SetRoot(() => new Center { Child = new TextField { Controller = ctrl, Node = node, Autofocus = true, Multiline = true, MaxLines = maxLines } });
        host.Update(new Size(300, 300));
        return (host, ctrl, node);
    }

    private static float FieldHeight(HamonRoot host) => host.Root!.Children[0].LayoutNode.Bounds.Height;

    [Fact]
    public void Enter_InsertsNewline_WhenMultiline()
    {
        (HamonRoot host, TextEditingController ctrl, _) = MountMultiline("ab");
        host.DispatchEditKey(TextEditKey.Enter);
        host.DispatchText('c');
        Assert.Equal("ab\nc", ctrl.Text);
        Assert.Equal(2, ctrl.LineCount);
    }

    [Fact]
    public void CaretLineColumn_ReflectNewlines()
    {
        TextEditingController c = new("ab\ncde");
        // caret は末尾（インデックス 6）。
        Assert.Equal(1, c.CaretLine);
        Assert.Equal(3, c.CaretColumn); // "cde" の後
        Assert.Equal(2, c.LineCount);
    }

    [Fact]
    public void MoveUp_PreservesColumn()
    {
        TextEditingController c = new("abcd\nef");
        c.End();        // 末尾 "ef" の後（line1, col2）
        Assert.Equal(1, c.CaretLine);
        c.MoveUp();     // 上の行（col2 を保つ＝"ab|cd"）
        Assert.Equal(0, c.CaretLine);
        Assert.Equal(2, c.CaretColumn);
    }

    [Fact]
    public void MoveUp_ClampsToShorterLine()
    {
        TextEditingController c = new("ab\ncdef");
        c.End();        // line1 col4
        c.MoveUp();     // 上の行は "ab"（長さ 2）→ col は 2 にクランプ
        Assert.Equal(0, c.CaretLine);
        Assert.Equal(2, c.CaretColumn);
    }

    [Fact]
    public void MoveDown_FromFirstLine()
    {
        TextEditingController c = new("abc\nxyz");
        c.Home();       // line1 col0... 実際は末尾開始なので Home は line1 の頭
        c.MoveUp();     // line0 col0
        Assert.Equal(0, c.CaretLine);
        c.MoveDown();   // line1 col0
        Assert.Equal(1, c.CaretLine);
        Assert.Equal(0, c.CaretColumn);
    }

    [Fact]
    public void HomeEnd_AreLineAware()
    {
        TextEditingController c = new("abc\ndefg");
        c.End();  // line1 末尾（"defg" の後＝インデックス 8）
        Assert.Equal(8, c.Caret);
        c.Home(); // line1 の頭（インデックス 4）
        Assert.Equal(4, c.Caret);
    }

    [Fact]
    public void Height_GrowsWithLineCount()
    {
        // 行高 20 + 縦パディング(8*2=16)。1 行なら 36、3 行なら 76。
        (HamonRoot host, TextEditingController ctrl, _) = MountMultiline("a");
        float h1 = FieldHeight(host);

        ctrl.SetText("a\nb\nc");
        host.Update(new Size(300, 300));
        float h3 = FieldHeight(host);

        Assert.True(h3 > h1, $"h1={h1} h3={h3}");
        Assert.Equal(h1 + (2 * 20f), h3, 0.5f); // 2 行分（40px）増える
    }

    [Fact]
    public void Height_ClampsToMaxLines()
    {
        (HamonRoot host, TextEditingController ctrl, _) = MountMultiline("a\nb\nc\nd\ne\nf\ng\nh", maxLines: 3);
        float h = FieldHeight(host);
        // 3 行 × 20 + 16 = 76 で頭打ち。
        Assert.Equal((3 * 20f) + 16f, h, 0.5f);
    }

    [Fact]
    public void SingleLine_Enter_StillSubmits()
    {
        string? submitted = null;
        var host = new HamonRoot(new StubTextRenderer());
        var ctrl = new TextEditingController("x");
        host.SetRoot(() => new TextField { Controller = ctrl, Node = new FocusNode(), Autofocus = true, OnSubmitted = s => submitted = s });
        host.Update(new Size(300, 100));

        host.DispatchEditKey(TextEditKey.Enter);
        Assert.Equal("x", submitted); // 単一行は従来どおり Enter=確定
        Assert.Equal("x", ctrl.Text);  // 改行は入らない
    }
}
