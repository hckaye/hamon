using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>A run of inline styled text (Flutter's <c>TextSpan</c>). Falls back to <see cref="RichText"/>'s defaults for unset properties.</summary>
public sealed class TextSpan
{
    public TextSpan(string text)
    {
        Text = text;
    }

    public string Text { get; init; }

    public float? FontSize { get; init; }

    public Color? Color { get; init; }
}

/// <summary>
/// Lays out multiple differently styled strings inline as a single paragraph
/// (Flutter's <c>RichText</c>). Wraps at word boundaries (spaces) when
/// <see cref="Wrap"/> is enabled.
/// </summary>
public sealed class RichText : Widget
{
    public IReadOnlyList<TextSpan> Spans { get; init; } = Array.Empty<TextSpan>();

    /// <summary>Default font size used when a span doesn't specify its own.</summary>
    public float FontSize { get; init; } = 16f;

    /// <summary>Default text color used when a span doesn't specify its own (falls back to the theme's <see cref="HamonTheme.OnSurface"/> if unspecified).</summary>
    public Color? Color { get; init; }

    /// <summary>Whether to wrap words within the available width (false = single line, width sized to content).</summary>
    public bool Wrap { get; init; } = true;

    public override Element CreateElement() => new RichTextElement(this);
}

/// <summary>The <see cref="Element"/> that backs a <see cref="RichText"/> widget.</summary>
internal sealed class RichTextElement : Element
{
    private readonly LayoutNode _node;
    private readonly List<Placed> _placed = new();

    // インライン配置の進行状態（MeasureSelf 内で使う）。
    private float _x;
    private float _y;
    private float _lineHeight;
    private float _maxX;
    private bool _lineStart;
    private float _layoutMaxWidth;

    // 直近の計測入力と結果（同一入力での再計測＝FlowSpan/Measure 呼び出しを避ける）。
    // Spans は毎回新しいインスタンスで渡されることが多い（参照が変わる）ため、内容（Text/FontSize/Color）で比較する。
    // Color は計測結果（Size）自体には影響しないが、_placed に焼き込まれ Paint で使われるため、キャッシュヒット時に
    // 古い色のまま Paint されないようキャッシュキーに含める。
    private bool _hasCache;
    private int _cacheSpanCount;
    private SpanSnapshot[] _cacheSpans = Array.Empty<SpanSnapshot>();
    private float _cacheFontSize;
    private Color? _cacheColor;
    private bool _cacheWrap;
    private float _cacheLayoutMaxWidth;
    private Size _cacheSize;

    public RichTextElement(RichText widget)
        : base(widget)
    {
        _node = new LayoutNode(measure: MeasureSelf);
    }

    private RichText W => (RichText)Widget;

    public override LayoutNode LayoutNode => _node;

    private ITextRenderer Renderer => Context.Text
        ?? throw new InvalidOperationException("RichText requires an ITextRenderer to measure/paint (supplied via HamonRoot).");

    public override void Paint(in PaintContext context)
    {
        Rect b = _node.Bounds;
        for (int i = 0; i < _placed.Count; i++)
        {
            Placed p = _placed[i];
            Vec2 pos = context.ApplyTransform(new Vec2(b.X + p.X, b.Y + p.Y));
            Renderer.Draw(p.Text, pos, p.FontSize * context.ScaleY, context.ApplyOpacity(p.Color));
        }
    }

    private Size MeasureSelf(BoxConstraints constraints)
    {
        RichText widget = W;
        float layoutMaxWidth = widget.Wrap && float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : float.PositiveInfinity;

        // 同一入力なら前回のフロー結果/サイズを再利用（FlowSpan の Substring/Measure 呼び出しを省く）。
        if (_hasCache
            && _cacheFontSize == widget.FontSize
            && Nullable.Equals(_cacheColor, widget.Color)
            && _cacheWrap == widget.Wrap
            && _cacheLayoutMaxWidth.Equals(layoutMaxWidth)
            && SpansUnchanged(widget.Spans))
        {
            return _cacheSize;
        }

        _placed.Clear();
        _x = 0f;
        _y = 0f;
        _lineHeight = 0f;
        _maxX = 0f;
        _lineStart = true;
        _layoutMaxWidth = layoutMaxWidth;

        Color fallback = widget.Color ?? Context.Theme.OnSurface;
        for (int i = 0; i < widget.Spans.Count; i++)
        {
            TextSpan span = widget.Spans[i];
            FlowSpan(span.Text, span.FontSize ?? widget.FontSize, span.Color ?? fallback);
        }

        float width = float.IsFinite(_layoutMaxWidth) ? Math.Min(_maxX, _layoutMaxWidth) : _maxX;
        float height = _y + _lineHeight;
        _cacheSize = new Size(width, height);

        CacheSpans(widget.Spans);
        _hasCache = true;
        _cacheFontSize = widget.FontSize;
        _cacheColor = widget.Color;
        _cacheWrap = widget.Wrap;
        _cacheLayoutMaxWidth = layoutMaxWidth;

        return _cacheSize;
    }

    /// <summary>Whether <paramref name="spans"/> has the same content (Text/FontSize/Color per span) as the cached snapshot.</summary>
    private bool SpansUnchanged(IReadOnlyList<TextSpan> spans)
    {
        if (spans.Count != _cacheSpanCount)
        {
            return false;
        }

        for (int i = 0; i < spans.Count; i++)
        {
            TextSpan span = spans[i];
            SpanSnapshot cached = _cacheSpans[i];
            if (span.Text != cached.Text || !Nullable.Equals(span.FontSize, cached.FontSize) || !Nullable.Equals(span.Color, cached.Color))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Snapshots <paramref name="spans"/> content into <see cref="_cacheSpans"/> (reusing the buffer when the count is unchanged).</summary>
    private void CacheSpans(IReadOnlyList<TextSpan> spans)
    {
        if (_cacheSpans.Length != spans.Count)
        {
            _cacheSpans = spans.Count == 0 ? Array.Empty<SpanSnapshot>() : new SpanSnapshot[spans.Count];
        }

        for (int i = 0; i < spans.Count; i++)
        {
            TextSpan span = spans[i];
            _cacheSpans[i] = new SpanSnapshot(span.Text, span.FontSize, span.Color);
        }

        _cacheSpanCount = spans.Count;
    }

    private void FlowSpan(string text, float fontSize, Color color)
    {
        float spaceWidth = Renderer.Measure(" ", fontSize).X;
        float glyphHeight = Renderer.Measure("Ag", fontSize).Y;

        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == ' ')
            {
                int j = i;
                while (j < text.Length && text[j] == ' ')
                {
                    j++;
                }

                if (!_lineStart)
                {
                    _x += spaceWidth * (j - i); // 行頭の空白は捨てる（折り返し後の見栄え）
                }

                i = j;
                continue;
            }

            int k = i;
            while (k < text.Length && text[k] != ' ')
            {
                k++;
            }

            string word = text.Substring(i, k - i);
            float wordWidth = Renderer.Measure(word, fontSize).X;

            if (!_lineStart && _x + wordWidth > _layoutMaxWidth)
            {
                // 折り返し。
                _maxX = Math.Max(_maxX, _x);
                _x = 0f;
                _y += _lineHeight;
                _lineHeight = 0f;
                _lineStart = true;
            }

            _placed.Add(new Placed(word, _x, _y, fontSize, color));
            _x += wordWidth;
            _maxX = Math.Max(_maxX, _x);
            _lineHeight = Math.Max(_lineHeight, glyphHeight);
            _lineStart = false;
            i = k;
        }
    }

    /// <summary>A lightweight snapshot of a <see cref="TextSpan"/>'s content, used to detect whether <c>Spans</c> changed since the last measure.</summary>
    private readonly struct SpanSnapshot
    {
        public SpanSnapshot(string text, float? fontSize, Color? color)
        {
            Text = text;
            FontSize = fontSize;
            Color = color;
        }

        public string Text { get; }

        public float? FontSize { get; }

        public Color? Color { get; }
    }

    private readonly struct Placed
    {
        public Placed(string text, float x, float y, float fontSize, Color color)
        {
            Text = text;
            X = x;
            Y = y;
            FontSize = fontSize;
            Color = color;
        }

        public string Text { get; }

        public float X { get; }

        public float Y { get; }

        public float FontSize { get; }

        public Color Color { get; }
    }
}
