using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Handling of overflow (Flutter<c>TextOverflow</c>）。</summary>
public enum TextOverflow : byte
{
    /// <summary>As is (it will stick out unless the parent clips it).</summary>
    Clip,

    /// <summary>Omit the end with "..." to fit within the width.</summary>
    Ellipsis,
}

/// <summary>
/// Widget that draws text (Flutter<c>Text</c>).
/// Use for measurement/drawing<see cref="ITextRenderer"/>teeth<see cref="BuildContext"/>Supplied via
/// The margin is<see cref="Padding"/>or<see cref="Column"/>/<see cref="Row"/>Attach using Spacing (same as Flutter).
/// <see cref="Softwrap"/>Turn around,<see cref="Overflow"/>＝<see cref="TextOverflow.Ellipsis"/>omitted,
/// <see cref="MaxLines"/>to limit the number of lines (excess lines are omitted/cut). <see cref="HamonTheme.OnSurface"/>。
/// </summary>
public sealed class Text : Widget
{
    public Text(string data) => Data = data;

    public string Data { get; }

    public float FontSize { get; init; } = 16f;

    /// <summary>Font name used (<see cref="ITextRenderer"/>Name registered in . </summary>
    public string? Font { get; init; }

    /// <summary>Font color (default to theme if unspecified).</summary>
    public Color? Color { get; init; }

    /// <summary>Border color (unspecified = no border). </summary>
    public Color? OutlineColor { get; init; }

    /// <summary>Border thickness (px, default 0 = none).<see cref="OutlineColor"/>Valid when specified.</summary>
    public float OutlineWidth { get; init; }

    /// <summary>Wrap at width to make multiple lines (default false = 1 line).</summary>
    public bool Softwrap { get; init; }

    /// <summary>Handling when the width does not fit (default<see cref="TextOverflow.Clip"/>）。</summary>
    public TextOverflow Overflow { get; init; }

    /// <summary>Maximum number of rows (0=unlimited). <see cref="Overflow"/>Omit/cut the last line according to the following.</summary>
    public int MaxLines { get; init; }

    public override Element CreateElement() => new TextElement(this);
}

/// <summary>A holding entity for text. <see cref="ITextRenderer.Measure"/>Find it with and draw it with Paint.</summary>
internal sealed class TextElement : Element
{
    private const string Ellipsis = "…";

    private static readonly Vec2[] OutlineDirs =
    {
        new(-1f, -1f), new(0f, -1f), new(1f, -1f),
        new(-1f, 0f), new(1f, 0f),
        new(-1f, 1f), new(0f, 1f), new(1f, 1f),
    };

    private readonly LayoutNode _node;
    private readonly List<string> _lines = new();
    private float _lineHeight = 0f;

    // 直近の計測入力と結果（同一入力での再計測＝FontStash 呼び出しを避ける）。
    private bool _hasCache;
    private string? _cacheData;
    private float _cacheFontSize;
    private string? _cacheFont;
    private float _cacheMaxWidth;
    private bool _cacheSoftwrap;
    private TextOverflow _cacheOverflow;
    private int _cacheMaxLines;
    private Size _cacheSize;

    public TextElement(Text widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureText);
    }

    public override LayoutNode LayoutNode => _node;

    /// <summary>Display rows after layout (for inspection/testing).</summary>
    internal IReadOnlyList<string> Lines => _lines;

    public override void Paint(in PaintContext context)
    {
        var widget = (Text)Widget;
        Rect bounds = _node.Bounds;
        Color color = context.ApplyOpacity(widget.Color ?? Context.Theme.OnSurface);
        float size = widget.FontSize * context.ScaleY;
        bool outline = widget.OutlineColor is not null && widget.OutlineWidth > 0f;
        Color outlineColor = outline ? context.ApplyOpacity(widget.OutlineColor!.Value) : default;
        float ow = widget.OutlineWidth * context.ScaleY;

        for (int i = 0; i < _lines.Count; i++)
        {
            Vec2 position = context.ApplyTransform(new Vec2(bounds.X, bounds.Y + (i * _lineHeight)));
            if (outline)
            {
                // 本体の下に8方向オフセットで縁取りを描く（バックエンド非依存＝Draw を複数回）。
                for (int d = 0; d < OutlineDirs.Length; d++)
                {
                    Renderer.Draw(_lines[i], new Vec2(position.X + (OutlineDirs[d].X * ow), position.Y + (OutlineDirs[d].Y * ow)), size, outlineColor, widget.Font);
                }
            }

            Renderer.Draw(_lines[i], position, size, color, widget.Font);
        }
    }

    private ITextRenderer Renderer => Context.Text
        ?? throw new InvalidOperationException("Text の計測/描画には ITextRenderer が要る（HamonRoot 経由で BuildContext に供給する）。");

    private float Width(string s) => Renderer.Measure(s, ((Text)Widget).FontSize, ((Text)Widget).Font).X;

    private Size MeasureText(BoxConstraints constraints)
    {
        var widget = (Text)Widget;

        // 同一入力なら前回の行分割/サイズを再利用（FontStash 計測を省く）。MaxWidth 以外の制約は本計測に影響しない。
        if (_hasCache
            && _cacheData == widget.Data
            && _cacheFontSize == widget.FontSize
            && _cacheFont == widget.Font
            && _cacheMaxWidth.Equals(constraints.MaxWidth)
            && _cacheSoftwrap == widget.Softwrap
            && _cacheOverflow == widget.Overflow
            && _cacheMaxLines == widget.MaxLines)
        {
            return _cacheSize;
        }

        _lineHeight = Renderer.Measure("Ag", widget.FontSize, widget.Font).Y;
        _lines.Clear();

        float maxW = constraints.MaxWidth;
        bool bounded = float.IsFinite(maxW);

        if (!bounded || (!widget.Softwrap && widget.Overflow == TextOverflow.Clip))
        {
            // 無制約 or クリップ1行：そのまま（はみ出しは親のクリップに委ねる）。
            _lines.Add(widget.Data);
        }
        else if (!widget.Softwrap)
        {
            _lines.Add(Width(widget.Data) <= maxW ? widget.Data : Ellipsize(widget.Data, maxW));
        }
        else
        {
            Wrap(widget.Data, maxW, widget.MaxLines, widget.Overflow);
        }

        float widthUsed = 0f;
        for (int i = 0; i < _lines.Count; i++)
        {
            widthUsed = Math.Max(widthUsed, Width(_lines[i]));
        }

        _cacheSize = new Size(widthUsed, _lines.Count * _lineHeight);
        _hasCache = true;
        _cacheData = widget.Data;
        _cacheFontSize = widget.FontSize;
        _cacheFont = widget.Font;
        _cacheMaxWidth = constraints.MaxWidth;
        _cacheSoftwrap = widget.Softwrap;
        _cacheOverflow = widget.Overflow;
        _cacheMaxLines = widget.MaxLines;
        return _cacheSize;
    }

    /// <summary>width<paramref name="maxW"/>Returns the longest prefix that fits + “…”.</summary>
    private string Ellipsize(string text, float maxW)
    {
        if (Width(Ellipsis) > maxW)
        {
            return Ellipsis;
        }

        int lo = 0;
        int hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Width(text.Substring(0, mid) + Ellipsis) <= maxW)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return text.Substring(0, lo) + Ellipsis;
    }

    private void Wrap(string text, float maxW, int maxLines, TextOverflow overflow)
    {
        int i = 0;
        while (i < text.Length)
        {
            int j = i + 1;
            while (j < text.Length && Width(text.Substring(i, (j - i) + 1)) <= maxW)
            {
                j++;
            }

            // 行数上限に達し、まだ残りがあるなら末尾行を省略/切りで締める。
            if (maxLines > 0 && _lines.Count == maxLines - 1 && j < text.Length)
            {
                string rest = text.Substring(i);
                _lines.Add(overflow == TextOverflow.Ellipsis ? Ellipsize(rest, maxW) : text.Substring(i, j - i));
                return;
            }

            _lines.Add(text.Substring(i, j - i));
            i = j;
        }

        if (_lines.Count == 0)
        {
            _lines.Add(string.Empty);
        }
    }
}
