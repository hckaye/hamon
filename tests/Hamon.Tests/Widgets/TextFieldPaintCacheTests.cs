using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// TextFieldElement.Paint (PaintSingleLine/PaintMultiline) caches composed text/measurements across Paint calls to
/// avoid redoing Substring/Measure work every frame while the caret blinks with no other change. These tests drive
/// real edits (caret move, selection change without caret change, IME composition, multiline caret-line change)
/// between two Render calls and assert the second Render reflects the new state — i.e. the cache invalidates
/// correctly and never shows stale text/caret/selection from the previous Paint.
/// </summary>
public class TextFieldPaintCacheTests
{
    // 1 文字 = 10px 幅、行高 20px の決定論スタブ（TextFieldMultilineTests と同じ）。
    private sealed class StubTextRenderer : ITextRenderer
    {
        public List<(string Text, Vec2 Position)> Draws { get; } = new();

        public Vec2 Measure(string text, float pixelSize) => new(text.Length * 10f, 20f);

        public void Draw(string text, Vec2 position, float pixelSize, Color color) => Draws.Add((text, position));
    }

    private sealed class RecordingPainter : IPainter
    {
        public List<(Rect Rect, Color Color)> Rects { get; } = new();

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color) => Rects.Add((rect, color));

        public void FillRoundedRect(Rect rect, Color color, float radius)
        {
        }

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private sealed class RecordingTextInput : ITextInput
    {
        public Rect LastCaret { get; private set; }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void SetCaretRect(Rect caret) => LastCaret = caret;
    }

    private static readonly Color SelectionMarker = new(10, 20, 30, 40);

    [Fact]
    public void SingleLine_CaretMove_WithoutTextChange_UpdatesCaretXOnNextPaint()
    {
        var renderer = new StubTextRenderer();
        var host = new HamonRoot(renderer);
        var textInput = new RecordingTextInput();
        host.TextInput = textInput;
        var ctrl = new TextEditingController("abcde"); // caret=5（末尾）
        var node = new FocusNode();
        host.SetRoot(() => new TextField { Controller = ctrl, Node = node, Autofocus = true, Width = Dimension.Px(600) });
        host.Update(new Size(800, 100));

        host.Render(new RecordingPainter());
        float caretX1 = textInput.LastCaret.X; // "abcde" 全体の後ろ＝5*10=50 分だけ右

        host.DispatchEditKey(TextEditKey.Home); // caret=0 へ（テキストは無変化）
        host.Render(new RecordingPainter());
        float caretX2 = textInput.LastCaret.X;

        // キャッシュが古い入力のまま据え置かれていれば caretX2 == caretX1 のまま（バグ）になる。
        Assert.Equal(caretX1 - 50f, caretX2, 2);
    }

    [Fact]
    public void SingleLine_SelectionExtended_UpdatesHighlightWidthOnNextPaint()
    {
        var renderer = new StubTextRenderer();
        var host = new HamonRoot(renderer);
        var ctrl = new TextEditingController("hello world");
        var node = new FocusNode();
        host.SetRoot(() => new TextField { Controller = ctrl, Node = node, Autofocus = true, Width = Dimension.Px(600), SelectionColor = SelectionMarker });
        host.Update(new Size(800, 100));

        // caret=2 → 選択を 5 まで拡張（caret=5, anchor=2, 選択="llo"）。
        ctrl.SetSelection(2);
        ctrl.SetSelection(5, extend: true);
        Assert.True(ctrl.HasSelection);

        var painter1 = new RecordingPainter();
        host.Render(painter1);
        (Rect Rect, Color Color) sel1 = Assert.Single(painter1.Rects, r => r.Color.Equals(SelectionMarker));
        Assert.Equal(30f, sel1.Rect.Width, 2); // (5-2)*10

        // 選択をさらに 8 まで拡張（anchor=2 は不変、caret/SelectionEnd のみ 5→8）。選択は非空のままなので、
        // HasSelection の外側ガードには頼れず、選択オフセットのキャッシュ自体が SelectionEnd の変化を拾う必要がある。
        ctrl.SetSelection(8, extend: true);
        Assert.True(ctrl.HasSelection);
        Assert.Equal(2, ctrl.SelectionStart);
        Assert.Equal(8, ctrl.SelectionEnd);

        var painter2 = new RecordingPainter();
        host.Render(painter2);
        (Rect Rect, Color Color) sel2 = Assert.Single(painter2.Rects, r => r.Color.Equals(SelectionMarker));

        // キャッシュが選択範囲の変化を見ていなければ（バグ）幅が古い 30 のまま据え置かれる。
        Assert.Equal(60f, sel2.Rect.Width, 2); // (8-2)*10
    }

    [Fact]
    public void SingleLine_Composition_ThenCommit_AcrossPaints_ReflectsCommittedTextNotStalePreedit()
    {
        var renderer = new StubTextRenderer();
        var host = new HamonRoot(renderer);
        var ctrl = new TextEditingController();
        var node = new FocusNode();
        host.SetRoot(() => new TextField { Controller = ctrl, Node = node, Autofocus = true, Width = Dimension.Px(600) });
        host.Update(new Size(800, 100));

        host.DispatchComposition("にほん", 3); // IME 変換中（未確定）
        host.Render(new RecordingPainter());
        Assert.Contains(renderer.Draws, d => d.Text == "にほん");

        renderer.Draws.Clear();
        host.DispatchText('日'); // 確定：Composition は消え、Text="日" になる
        Assert.Equal(string.Empty, ctrl.Composition);
        Assert.Equal("日", ctrl.Text);

        host.Render(new RecordingPainter());

        // キャッシュが変換中入力のまま据え置かれていれば「にほん」が残り、"日" が描かれない（バグ）。
        Assert.Contains(renderer.Draws, d => d.Text == "日");
        Assert.DoesNotContain(renderer.Draws, d => d.Text.Contains("にほん"));
    }

    private static (HamonRoot host, TextEditingController ctrl, RecordingTextInput textInput) MountMultiline(string initial)
    {
        var renderer = new StubTextRenderer();
        var host = new HamonRoot(renderer);
        var textInput = new RecordingTextInput();
        host.TextInput = textInput;
        var ctrl = new TextEditingController(initial);
        var node = new FocusNode();
        // Center で包む＝loose 制約（TextFieldMultilineTests と同じ理由）。
        host.SetRoot(() => new Center { Child = new TextField { Controller = ctrl, Node = node, Autofocus = true, Multiline = true, MaxLines = 6, SelectionColor = SelectionMarker } });
        host.Update(new Size(300, 300));
        return (host, ctrl, textInput);
    }

    [Fact]
    public void Multiline_CaretLineChange_WithoutTextChange_UpdatesCaretYOnNextPaint()
    {
        (HamonRoot host, TextEditingController ctrl, RecordingTextInput textInput) = MountMultiline("ab\ncd");
        Assert.Equal(5, ctrl.Caret); // 末尾（2行目 "cd" の後）
        Assert.Equal(1, ctrl.CaretLine);

        host.Render(new RecordingPainter());
        float y1 = textInput.LastCaret.Y;

        host.DispatchEditKey(TextEditKey.Up); // 1 行目へ（テキストは無変化、キャレット行のみ変化）
        Assert.Equal(0, ctrl.CaretLine);
        host.Render(new RecordingPainter());
        float y2 = textInput.LastCaret.Y;

        // キャッシュが古いキャレット行のまま据え置かれていれば y2 == y1 のまま（バグ）になる。
        Assert.Equal(20f, y1 - y2, 2); // 行高 20px 分だけ上へ
    }

    [Fact]
    public void Multiline_SelectionExtended_UpdatesHighlightWidthOnNextPaint()
    {
        (HamonRoot host, TextEditingController ctrl, _) = MountMultiline("hello world");

        // caret=2 → 選択を 5 まで拡張（caret=5, anchor=2, 選択="llo"）。
        ctrl.SetSelection(2);
        ctrl.SetSelection(5, extend: true);
        Assert.True(ctrl.HasSelection);

        var painter1 = new RecordingPainter();
        host.Render(painter1);
        (Rect Rect, Color Color) sel1 = Assert.Single(painter1.Rects, r => r.Color.Equals(SelectionMarker));
        Assert.Equal(30f, sel1.Rect.Width, 2); // (5-2)*10

        // 選択をさらに 8 まで拡張（anchor=2 は不変、caret/SelectionEnd のみ 5→8）。選択は非空のままなので、
        // HasSelection の外側ガードには頼れず、選択オフセットのキャッシュ自体が SelectionEnd の変化を拾う必要がある。
        ctrl.SetSelection(8, extend: true);
        Assert.True(ctrl.HasSelection);
        Assert.Equal(2, ctrl.SelectionStart);
        Assert.Equal(8, ctrl.SelectionEnd);

        var painter2 = new RecordingPainter();
        host.Render(painter2);
        (Rect Rect, Color Color) sel2 = Assert.Single(painter2.Rects, r => r.Color.Equals(SelectionMarker));

        // キャッシュが選択範囲の変化を見ていなければ（バグ）幅が古い 30 のまま据え置かれる。
        Assert.Equal(60f, sel2.Rect.Width, 2); // (8-2)*10
    }
}
