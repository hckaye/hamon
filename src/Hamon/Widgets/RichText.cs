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
        _placed.Clear();
        _x = 0f;
        _y = 0f;
        _lineHeight = 0f;
        _maxX = 0f;
        _lineStart = true;
        _layoutMaxWidth = W.Wrap && float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : float.PositiveInfinity;

        Color fallback = W.Color ?? Context.Theme.OnSurface;
        for (int i = 0; i < W.Spans.Count; i++)
        {
            TextSpan span = W.Spans[i];
            FlowSpan(span.Text, span.FontSize ?? W.FontSize, span.Color ?? fallback);
        }

        float width = float.IsFinite(_layoutMaxWidth) ? Math.Min(_maxX, _layoutMaxWidth) : _maxX;
        float height = _y + _lineHeight;
        return new Size(width, height);
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
