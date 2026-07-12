using Hamon.Layout;
using System.Globalization;

namespace Hamon.Widgets;

/// <summary>
/// Abstraction over the clipboard (copy/cut/paste).
/// Injected via <see cref="HamonRoot.Clipboard"/>; <see cref="InMemoryClipboard"/> provides an in-process default.
/// </summary>
public interface IClipboard
{
    string GetText();

    void SetText(string text);
}

/// <summary>Default in-process clipboard with no OS coordination. Copy and paste only work within a single process (e.g., for testing).</summary>
public sealed class InMemoryClipboard : IClipboard
{
    private string _text = string.Empty;

    public string GetText() => _text;

    public void SetText(string text) => _text = text ?? string.Empty;
}

/// <summary>
/// Text editing state for a <see cref="TextField"/> (equivalent to Flutter's <c>TextEditingController</c>).
/// Holds the <b>selection range</b> and marks the host dirty whenever an edit operation updates it.
/// Caret and selection movement operate in <b>grapheme cluster</b> units (surrogate pairs and combining
/// characters are each treated as a single unit). Owned by <see cref="TextField"/> and not recreated on rebuild.
/// </summary>
public sealed class TextEditingController
{
    private IHamonHost? _host; // 再描画通知の宛先。TextField のマウント/更新時に Attach で接続（Flutter は ChangeNotifier＝host 不要）。
    private int _anchor; // 選択の固定端（Caret が移動端）。_anchor == Caret なら選択なし。

    public TextEditingController(string text = "")
    {
        Text = text;
        Caret = text.Length;
        _anchor = Caret;
    }

    /// <summary>Connects to the host. Called by <see cref="TextField"/> at build/mount time; the host becomes the destination for redraw notifications on edit.</summary>
    internal void Attach(IHamonHost host) => _host = host;

    public string Text { get; private set; }

    /// <summary>Caret position (0..Text.Length, moving end when selected).</summary>
    public int Caret { get; private set; }

    /// <summary>Start (inclusion) of selection.</summary>
    public int SelectionStart => Math.Min(_anchor, Caret);

    /// <summary>End of selection (exclusive).</summary>
    public int SelectionEnd => Math.Max(_anchor, Caret);

    /// <summary>Is there a selection (range is not empty)?</summary>
    public bool HasSelection => _anchor != Caret;

    /// <summary>Selected string (empty if none).</summary>
    public string SelectedText => HasSelection ? Text.Substring(SelectionStart, SelectionEnd - SelectionStart) : string.Empty;

    /// <summary>IME text currently being composed (preedit).</summary>
    public string Composition { get; private set; } = string.Empty;

    /// <summary>Caret position in the text being converted (0..Composition.Length).</summary>
    public int CompositionCaret { get; private set; }

    public Action<string>? OnChanged { get; set; }

    /// <summary>Sets the IME composition text (preedit). This text is not yet committed, so it does not change <see cref="Text"/>.</summary>
    public void SetComposition(string preedit, int caret)
    {
        string value = preedit ?? string.Empty;
        if (value == Composition && caret == CompositionCaret)
        {
            return;
        }

        Composition = value;
        CompositionCaret = Math.Clamp(caret, 0, Composition.Length);
        _host?.MarkDirty();
    }

    public void Insert(char c)
    {
        if (char.IsControl(c))
        {
            return; // 制御文字は編集キー側で扱う
        }

        InsertString(c.ToString());
    }

    /// <summary>Inserts a string, replacing the current selection if any. Also commits any in-progress IME composition before inserting.</summary>
    public void InsertString(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        Composition = string.Empty; // 確定（commit）＝変換中を消して挿入
        CompositionCaret = 0;
        if (HasSelection)
        {
            RemoveSelection();
        }

        Text = Text.Insert(Caret, s);
        Caret += s.Length;
        _anchor = Caret;
        Changed();
    }

    public void Backspace()
    {
        if (HasSelection)
        {
            RemoveSelection();
            Changed();
            return;
        }

        if (Caret > 0)
        {
            int prev = PrevGrapheme(Caret);
            Text = Text.Remove(prev, Caret - prev);
            Caret = prev;
            _anchor = prev;
            Changed();
        }
    }

    public void Delete()
    {
        if (HasSelection)
        {
            RemoveSelection();
            Changed();
            return;
        }

        if (Caret < Text.Length)
        {
            int next = NextGrapheme(Caret);
            Text = Text.Remove(Caret, next - Caret);
            Changed();
        }
    }

    // --- キャレット移動（選択は畳む）。書記素単位。 ---
    public void MoveLeft() => Collapse(HasSelection ? SelectionStart : PrevGrapheme(Caret));

    public void MoveRight() => Collapse(HasSelection ? SelectionEnd : NextGrapheme(Caret));

    /// <summary>Moves the caret to the beginning of the line (the current line, for multi-line text).</summary>
    public void Home() => Collapse(LineStart(Caret));

    /// <summary>Moves the caret to the end of the line (the current line, for multi-line text).</summary>
    public void End() => Collapse(LineEnd(Caret));

    /// <summary>Moves the caret up one row, keeping the column position.</summary>
    public void MoveUp() => Collapse(VerticalCaret(-1));

    /// <summary>Moves the caret down one row, keeping the column position.</summary>
    public void MoveDown() => Collapse(VerticalCaret(+1));

    /// <summary>The caret's line number (0-based, delimited by \n).</summary>
    public int CaretLine => LineIndexOf(Caret);

    /// <summary>The caret's column number (character offset from the beginning of the current line).</summary>
    public int CaretColumn => Caret - LineStart(Caret);

    /// <summary>The number of lines (the number of \n characters, plus one).</summary>
    public int LineCount
    {
        get
        {
            int count = 1;
            for (int i = 0; i < Text.Length; i++)
            {
                if (Text[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }
    }

    // --- 選択拡張（_anchor を固定して Caret を動かす）。Shift+方向や Shift+Home/End 相当。 ---
    public void SelectLeft() => ExtendCaret(PrevGrapheme(Caret));

    public void SelectRight() => ExtendCaret(NextGrapheme(Caret));

    public void SelectToHome() => ExtendCaret(0);

    public void SelectToEnd() => ExtendCaret(Text.Length);

    /// <summary>Select all (equivalent to Ctrl/Cmd+A).</summary>
    public void SelectAll()
    {
        if (Text.Length == 0)
        {
            return;
        }

        _anchor = 0;
        Caret = Text.Length;
        _host?.MarkDirty();
    }

    /// <summary>Copy the selection to the clipboard (does nothing if there is no selection).</summary>
    public void Copy(IClipboard clipboard)
    {
        if (HasSelection)
        {
            clipboard.SetText(SelectedText);
        }
    }

    /// <summary>Cut the selection (copy + delete).</summary>
    public void Cut(IClipboard clipboard)
    {
        if (HasSelection)
        {
            clipboard.SetText(SelectedText);
            RemoveSelection();
            Changed();
        }
    }

    /// <summary>Pastes the clipboard contents, replacing the selection if one exists.</summary>
    public void Paste(IClipboard clipboard) => InsertString(clipboard.GetText());

    /// <summary>Replaces the text, clamping the caret to the new range and clearing the selection.</summary>
    public void SetText(string text)
    {
        Text = text ?? string.Empty;
        Caret = Math.Clamp(Caret, 0, Text.Length);
        _anchor = Caret;
        Changed();
    }

    /// <summary>Directly sets the caret position (for tap/drag selection). Pass <paramref name="extend"/>=true to extend the current selection instead of collapsing it.</summary>
    public void SetSelection(int caret, bool extend = false)
    {
        Caret = Math.Clamp(caret, 0, Text.Length);
        if (!extend)
        {
            _anchor = Caret;
        }

        _host?.MarkDirty();
    }

    private void RemoveSelection()
    {
        int start = SelectionStart;
        int end = SelectionEnd;
        Text = Text.Remove(start, end - start);
        Caret = start;
        _anchor = start;
    }

    private void Collapse(int caret)
    {
        int clamped = Math.Clamp(caret, 0, Text.Length);
        if (clamped != Caret || HasSelection)
        {
            Caret = clamped;
            _anchor = clamped;
            _host?.MarkDirty();
        }
    }

    private void ExtendCaret(int caret)
    {
        int clamped = Math.Clamp(caret, 0, Text.Length);
        if (clamped != Caret)
        {
            Caret = clamped; // _anchor は固定＝選択が伸びる
            _host?.MarkDirty();
        }
    }

    // 直前の書記素クラスタ境界（サロゲートペア/結合文字を1単位として戻る）。
    private int PrevGrapheme(int index)
    {
        if (index <= 0)
        {
            return 0;
        }

        int pos = 0;
        int prev = 0;
        while (pos < index)
        {
            prev = pos;
            pos += StringInfo.GetNextTextElementLength(Text, pos);
        }

        return prev;
    }

    // 次の書記素クラスタ境界。
    private int NextGrapheme(int index)
    {
        if (index >= Text.Length)
        {
            return Text.Length;
        }

        return index + StringInfo.GetNextTextElementLength(Text, index);
    }

    // 指定オフセットを含む行の先頭インデックス（直前の \n の次、無ければ 0）。
    private int LineStart(int index)
    {
        int i = Math.Clamp(index, 0, Text.Length) - 1;
        while (i >= 0 && Text[i] != '\n')
        {
            i--;
        }

        return i + 1;
    }

    // 指定オフセットを含む行の末尾インデックス（次の \n の位置、無ければ末尾）。
    private int LineEnd(int index)
    {
        int i = Math.Clamp(index, 0, Text.Length);
        while (i < Text.Length && Text[i] != '\n')
        {
            i++;
        }

        return i;
    }

    private int LineIndexOf(int index)
    {
        int line = 0;
        int limit = Math.Clamp(index, 0, Text.Length);
        for (int i = 0; i < limit; i++)
        {
            if (Text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    // 現在の列を保って上下の行へ。境界では先頭/末尾へ。
    private int VerticalCaret(int deltaLines)
    {
        int column = CaretColumn;
        int lineStart = LineStart(Caret);

        if (deltaLines < 0)
        {
            if (lineStart == 0)
            {
                return 0; // 最上行＝先頭へ
            }

            int prevStart = LineStart(lineStart - 1);
            int prevEnd = lineStart - 1; // 直前 \n の位置
            return Math.Min(prevStart + column, prevEnd);
        }
        else
        {
            int lineEnd = LineEnd(Caret);
            if (lineEnd >= Text.Length)
            {
                return Text.Length; // 最下行＝末尾へ
            }

            int nextStart = lineEnd + 1;
            int nextEnd = LineEnd(nextStart);
            return Math.Min(nextStart + column, nextEnd);
        }
    }

    private void Changed()
    {
        OnChanged?.Invoke(Text);
        _host?.MarkDirty();
    }
}

/// <summary>
/// Single-line (or multi-line) text input widget (a minimal equivalent of Flutter's <c>TextField</c>).
/// Receives characters and edit keys via <see cref="HamonRoot.DispatchText"/> / <see cref="HamonRoot.DispatchEditKey"/>.
/// The caret blinks using an <see cref="AnimationController"/>, and long text scrolls horizontally to keep the caret visible.
/// </summary>
public sealed class TextField : Widget
{
    public TextEditingController Controller { get; init; } = null!;

    public FocusNode Node { get; init; } = new();

    public bool Autofocus { get; init; }

    public string Placeholder { get; init; } = string.Empty;

    /// <summary>Accessibility label read by screen readers. If not specified, the placeholder text is used instead.</summary>
    public string? SemanticLabel { get; init; }

    public float FontSize { get; init; } = 16f;

    public Color? TextColor { get; init; }

    public Color? PlaceholderColor { get; init; }

    public Color? CaretColor { get; init; }

    /// <summary>Highlight color for the selection. If not specified, a semi-transparent version of the theme's Primary color is used.</summary>
    public Color? SelectionColor { get; init; }

    public Color? Background { get; init; }

    public EdgeInsets Padding { get; init; } = EdgeInsets.Symmetric(10f, 8f);

    /// <summary>Width (Auto means the full available width).</summary>
    public Dimension Width { get; init; }

    /// <summary>Enables multi-line input. When true, Enter inserts a line break and the height grows with the number of lines.</summary>
    public bool Multiline { get; init; }

    /// <summary>Maximum number of visible lines in multi-line mode (an upper bound on height; additional lines scroll). Defaults to 6.</summary>
    public int MaxLines { get; init; } = 6;

    /// <summary>Minimum number of visible lines in multi-line mode (a lower bound on height). Defaults to 1.</summary>
    public int MinLines { get; init; } = 1;

    /// <summary>Invoked when Enter is pressed to submit the text.</summary>
    public Action<string>? OnSubmitted { get; init; }

    /// <summary>Notifies of focus gain/loss via <see cref="WidgetState"/>, e.g. to drive a custom animation such as highlighting the border when focused.</summary>
    public Action<WidgetState>? OnStateChanged { get; init; }

    public override Element CreateElement() => new TextFieldElement(this);
}

/// <summary>The <see cref="Element"/> that backs a <see cref="TextField"/>.</summary>
internal sealed class TextFieldElement : Element
{
    private readonly LayoutNode _node;
    private AnimationController? _blink;
    private float _lastCaretX = float.NaN; // キャレットが動いたら点滅を可視へリセットするための前回値

    public TextFieldElement(TextField widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    public override LayoutNode LayoutNode => _node;

    public override bool WantsPointer => true; // タップでフォーカス（HamonRoot が祖先 FocusNode を探す）

    internal override FocusNode? FocusNodeOrNull => ((TextField)Widget).Node;

    private TextField Widget_ => (TextField)Widget;

    private ITextRenderer Renderer => Context.Text
        ?? throw new InvalidOperationException("TextField requires an ITextRenderer to measure/paint (supplied via HamonRoot).");

    public override void Mount(Element? parent, BuildContext context)
    {
        base.Mount(parent, context);
        FocusNode node = Widget_.Node;
        node.OnTextInput = c => Widget_.Controller.Insert(c);
        node.OnEditKey = HandleEditKey;
        node.OnComposition = (preedit, caret) => Widget_.Controller.SetComposition(preedit, caret); // IME 変換中
        node.OnFocusChange = OnFocusChange;
        node.SemanticLabel = Widget_.SemanticLabel ?? (Widget_.Placeholder.Length > 0 ? Widget_.Placeholder : null);

        if (context.Focusable)
        {
            context.Focus?.Register(node, () => LayoutNode.Bounds, EnclosingScope(), EnclosingScrollable(), context.Traversable);
            if (Widget_.Autofocus)
            {
                context.Focus?.AutofocusIfNone(node);
            }
        }

        if (context.Owner is { } owner)
        {
            Widget_.Controller.Attach(owner); // 編集での再描画通知の宛先を接続
            _blink = owner.CreateAnimation(0.5f, Curves.Linear);
            _blink.OnCompleted = () =>
            {
                if (_blink!.Value <= 0f)
                {
                    _blink.Forward();
                }
                else
                {
                    _blink.Reverse();
                }
            };
            if (node.HasFocus)
            {
                _blink.Forward();
            }
        }
    }

    public override void Update(Widget newWidget)
    {
        base.Update(newWidget);
        if (Context.Owner is { } owner)
        {
            Widget_.Controller.Attach(owner); // 再構築で Controller が差し替わっても通知先を維持
        }
    }

    public override void Unmount()
    {
        Context.Focus?.Unregister(Widget_.Node);
        _blink?.Stop();
        base.Unmount();
    }

    public override void Paint(in PaintContext context)
    {
        TextField widget = Widget_;
        TextEditingController controller = widget.Controller;
        HamonTheme theme = Context.Theme;
        Rect bounds = _node.Bounds;

        if (widget.Background is Color background)
        {
            context.FillRect(bounds, background);
        }

        EdgeInsets pad = widget.Padding;
        var inner = new Rect(bounds.X + pad.Left, bounds.Y + pad.Top, Math.Max(0f, bounds.Width - pad.Horizontal), Math.Max(0f, bounds.Height - pad.Vertical));

        object? prev = context.PushClip(inner);

        if (widget.Multiline)
        {
            PaintMultiline(context, inner, widget, controller, theme);
        }
        else
        {
            PaintSingleLine(context, inner, widget, controller, theme);
        }

        context.PopClip(prev);
    }

    private void PaintSingleLine(in PaintContext context, Rect inner, TextField widget, TextEditingController controller, HamonTheme theme)
    {
        // 表示テキスト＝確定文字のキャレット位置に変換中(preedit)を挿入したもの。preedit は下線付き。
        string before = controller.Text.Substring(0, controller.Caret);
        string comp = controller.Composition;
        string after = controller.Text.Substring(controller.Caret);
        string composed = before + comp + after;

        bool empty = composed.Length == 0;
        string shown = empty ? widget.Placeholder : composed;
        Color color = empty ? (widget.PlaceholderColor ?? theme.OnSurfaceVariant) : (widget.TextColor ?? theme.OnSurface);

        float fs = widget.FontSize;
        float beforeW = before.Length > 0 ? Renderer.Measure(before, fs).X : 0f;
        float compW = comp.Length > 0 ? Renderer.Measure(comp, fs).X : 0f;
        float compCaretW = comp.Length > 0 ? Renderer.Measure(comp.Substring(0, controller.CompositionCaret), fs).X : 0f;
        float caretX = beforeW + compCaretW; // 変換中はその中のキャレット、なければ確定キャレット
        float lineH = Renderer.Measure("Ag", fs).Y;
        float scroll = Math.Max(0f, caretX - inner.Width); // キャレットを枠内に保つ横スクロール
        float textLeft = inner.X - scroll;

        // 選択ハイライト（変換中でないときのみ）。テキストの背面に塗る。
        if (widget.Node.HasFocus && comp.Length == 0 && controller.HasSelection)
        {
            float selStartX = controller.SelectionStart > 0 ? Renderer.Measure(controller.Text.Substring(0, controller.SelectionStart), fs).X : 0f;
            float selEndX = controller.SelectionEnd > 0 ? Renderer.Measure(controller.Text.Substring(0, controller.SelectionEnd), fs).X : 0f;
            Color selColor = widget.SelectionColor ?? new Color(theme.Primary.R, theme.Primary.G, theme.Primary.B, 90);
            context.FillRect(new Rect(textLeft + selStartX, inner.Y, MathF.Max(0f, selEndX - selStartX), lineH), selColor);
        }

        if (shown.Length > 0)
        {
            Vec2 pos = context.ApplyTransform(new Vec2(textLeft, inner.Y));
            Renderer.Draw(shown, pos, fs * context.ScaleY, context.ApplyOpacity(color));
        }

        if (comp.Length > 0) // 変換中テキストの下線
        {
            context.FillRect(new Rect(textLeft + beforeW, inner.Y + lineH - 2f, compW, 2f), widget.CaretColor ?? theme.Primary);
        }

        if (widget.Node.HasFocus)
        {
            // キャレットが動いたら（入力・移動）点滅を可視からやり直す＝打鍵中は常にハッキリ見える。
            if (_blink is not null && caretX != _lastCaretX && !float.IsNaN(_lastCaretX))
            {
                _blink.JumpTo(1f);
                _blink.Reverse();
            }

            _lastCaretX = caretX;

            Color caret = widget.CaretColor ?? theme.Primary;
            bool visible = (_blink?.Value ?? 1f) >= 0.5f;
            if (visible)
            {
                context.FillRect(new Rect(textLeft + caretX, inner.Y, 2.5f, lineH), caret);
            }

            // IME 候補ウィンドウの位置にキャレット矩形（絶対座標）を伝える。
            Context.Owner?.SetTextInputCaret(new Rect(textLeft + caretX, inner.Y, 2.5f, lineH));
        }
        else
        {
            _lastCaretX = float.NaN; // 非フォーカス時はリセット（再フォーカスで誤発火しない）
        }
    }

    private void PaintMultiline(in PaintContext context, Rect inner, TextField widget, TextEditingController controller, HamonTheme theme)
    {
        float fs = widget.FontSize;
        float lineH = Renderer.Measure("Ag", fs).Y;

        // 確定テキストのキャレット位置に変換中(preedit)を挿入した表示用テキスト。
        int caretGlobal = controller.Caret + (controller.Composition.Length > 0 ? controller.CompositionCaret : 0);
        string composed = controller.Text.Insert(controller.Caret, controller.Composition);
        bool empty = composed.Length == 0;

        string[] lines = (empty ? widget.Placeholder : composed).Split('\n');
        Color textColor = empty ? (widget.PlaceholderColor ?? theme.OnSurfaceVariant) : (widget.TextColor ?? theme.OnSurface);

        // composed 上の各行先頭オフセット（キャレット行/列とのマッピング用）。
        int caretLine = 0;
        int caretLineStart = 0;
        {
            int offset = 0;
            for (int li = 0; li < lines.Length; li++)
            {
                int lineLen = lines[li].Length;
                if (caretGlobal >= offset && caretGlobal <= offset + lineLen)
                {
                    caretLine = li;
                    caretLineStart = offset;
                }

                offset += lineLen + 1; // +1 は \n
            }
        }

        int maxLines = Math.Max(1, widget.MaxLines);
        int firstVisible = caretLine >= maxLines ? caretLine - maxLines + 1 : 0;
        int caretColX = caretGlobal - caretLineStart;
        float caretXOnLine = caretColX > 0 ? Renderer.Measure(lines[caretLine].Substring(0, Math.Min(caretColX, lines[caretLine].Length)), fs).X : 0f;
        float hscroll = Math.Max(0f, caretXOnLine - inner.Width);
        float textLeft = inner.X - hscroll;

        // 選択ハイライト（変換中でないときのみ・行ごとに塗る）。
        if (widget.Node.HasFocus && controller.Composition.Length == 0 && controller.HasSelection)
        {
            Color selColor = widget.SelectionColor ?? new Color(theme.Primary.R, theme.Primary.G, theme.Primary.B, 90);
            int selStart = controller.SelectionStart;
            int selEnd = controller.SelectionEnd;
            int offset = 0;
            for (int li = 0; li < lines.Length; li++)
            {
                int lineLen = lines[li].Length;
                int visualRow = li - firstVisible;
                if (visualRow >= 0 && visualRow < maxLines)
                {
                    int s = Math.Max(selStart, offset) - offset;
                    int e = Math.Min(selEnd, offset + lineLen) - offset;
                    if (e > s && s <= lineLen)
                    {
                        float sx = s > 0 ? Renderer.Measure(lines[li].Substring(0, s), fs).X : 0f;
                        float ex = e > 0 ? Renderer.Measure(lines[li].Substring(0, Math.Min(e, lineLen)), fs).X : 0f;
                        float y = inner.Y + (visualRow * lineH);
                        context.FillRect(new Rect(textLeft + sx, y, MathF.Max(0f, ex - sx), lineH), selColor);
                    }
                }

                offset += lineLen + 1;
            }
        }

        // 各表示行を描画。
        int lastRow = Math.Min(lines.Length, firstVisible + maxLines);
        for (int li = firstVisible; li < lastRow; li++)
        {
            if (lines[li].Length == 0)
            {
                continue;
            }

            float y = inner.Y + ((li - firstVisible) * lineH);
            Vec2 pos = context.ApplyTransform(new Vec2(textLeft, y));
            Renderer.Draw(lines[li], pos, fs * context.ScaleY, context.ApplyOpacity(textColor));
        }

        // 変換中テキストの下線（キャレット行上）。
        if (controller.Composition.Length > 0)
        {
            int compStartCol = controller.Caret - caretLineStart;
            float ux = compStartCol > 0 ? Renderer.Measure(lines[caretLine].Substring(0, Math.Min(compStartCol, lines[caretLine].Length)), fs).X : 0f;
            float uw = Renderer.Measure(controller.Composition, fs).X;
            float uy = inner.Y + ((caretLine - firstVisible) * lineH) + lineH - 2f;
            context.FillRect(new Rect(textLeft + ux, uy, uw, 2f), widget.CaretColor ?? theme.Primary);
        }

        if (widget.Node.HasFocus)
        {
            float caretY = inner.Y + ((caretLine - firstVisible) * lineH);
            float caretSig = (caretLine * 100000f) + caretXOnLine; // 行/列いずれの移動でも点滅を可視からやり直す
            if (_blink is not null && caretSig != _lastCaretX && !float.IsNaN(_lastCaretX))
            {
                _blink.JumpTo(1f);
                _blink.Reverse();
            }

            _lastCaretX = caretSig;

            bool visible = (_blink?.Value ?? 1f) >= 0.5f;
            if (visible)
            {
                context.FillRect(new Rect(textLeft + caretXOnLine, caretY, 2.5f, lineH), widget.CaretColor ?? theme.Primary);
            }

            Context.Owner?.SetTextInputCaret(new Rect(textLeft + caretXOnLine, caretY, 2.5f, lineH));
        }
        else
        {
            _lastCaretX = float.NaN;
        }
    }

    private void OnFocusChange(bool focused)
    {
        if (focused)
        {
            Context.Owner?.BeginTextInput(); // IME/ソフトキーボードを有効化
            _blink?.JumpTo(1f);
            _blink?.Reverse();
        }
        else
        {
            Widget_.Controller.SetComposition(string.Empty, 0); // フォーカスを失ったら変換中を破棄
            Context.Owner?.EndTextInput();
            _blink?.Stop();
        }

        Widget_.OnStateChanged?.Invoke(focused ? WidgetState.Focused : WidgetState.None);
    }

    private void HandleEditKey(TextEditKey key)
    {
        TextEditingController c = Widget_.Controller;
        IClipboard clipboard = (Context.Owner as HamonRoot)?.Clipboard ?? FallbackClipboard;
        switch (key)
        {
            case TextEditKey.Backspace:
                c.Backspace();
                break;
            case TextEditKey.Delete:
                c.Delete();
                break;
            case TextEditKey.Left:
                c.MoveLeft();
                break;
            case TextEditKey.Right:
                c.MoveRight();
                break;
            case TextEditKey.Up:
                c.MoveUp();
                break;
            case TextEditKey.Down:
                c.MoveDown();
                break;
            case TextEditKey.Home:
                c.Home();
                break;
            case TextEditKey.End:
                c.End();
                break;
            case TextEditKey.SelectLeft:
                c.SelectLeft();
                break;
            case TextEditKey.SelectRight:
                c.SelectRight();
                break;
            case TextEditKey.SelectHome:
                c.SelectToHome();
                break;
            case TextEditKey.SelectEnd:
                c.SelectToEnd();
                break;
            case TextEditKey.SelectAll:
                c.SelectAll();
                break;
            case TextEditKey.Copy:
                c.Copy(clipboard);
                break;
            case TextEditKey.Cut:
                c.Cut(clipboard);
                break;
            case TextEditKey.Paste:
                c.Paste(clipboard);
                break;
            case TextEditKey.NewLine:
                c.InsertString("\n");
                break;
            case TextEditKey.Enter:
                if (Widget_.Multiline)
                {
                    c.InsertString("\n"); // 複数行では Enter＝改行
                }
                else
                {
                    Widget_.OnSubmitted?.Invoke(c.Text);
                }

                break;
        }
    }

    // 非視覚ホスト等で Clipboard を持たない場合の保険（プロセス内）。
    private static readonly IClipboard FallbackClipboard = new InMemoryClipboard();

    private Size MeasureSelf(BoxConstraints constraints)
    {
        TextField widget = Widget_;
        float lineH = Renderer.Measure("Ag", widget.FontSize).Y;

        int rows = 1;
        if (widget.Multiline)
        {
            // 行数に追従（MinLines..MaxLines にクランプ）。超過はスクロール（PaintMultiline が縦オフセット）。
            int lineCount = widget.Controller.LineCount;
            rows = Math.Clamp(lineCount, Math.Max(1, widget.MinLines), Math.Max(1, widget.MaxLines));
        }

        float height = (lineH * rows) + widget.Padding.Vertical;
        float? fixedW = widget.Width.Resolve(constraints.MaxWidth);
        float width = fixedW ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : 200f);
        return new Size(width, height);
    }
}
