using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>A section of inline decorative text (Flutter<c>TextSpan</c>). <see cref="RichText"/>default.</summary>
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
/// Placing multiple style strings inline as one paragraph (Flutter<c>RichText</c>).
/// Wraps each word using spaces in the string (<see cref="Wrap"/>).
/// </summary>
public sealed class RichText : Widget
{
    public IReadOnlyList<TextSpan> Spans { get; init; } = Array.Empty<TextSpan>();

    /// <summary>Default size when span does not specify color/size.</summary>
    public float FontSize { get; init; } = 16f;

    /// <summary>Default text color (when span is not specified. If not specified, theme's OnSurface).</summary>
    public Color? Color { get; init; }

    /// <summary>Whether to wrap words in units of available width (false = 1 line, width is content).</summary>
    public bool Wrap { get; init; } = true;

    public override Element CreateElement() => new RichTextElement(this);
}

/// <summary><see cref="RichText"/>holding entity. </summary>
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
        ?? throw new InvalidOperationException("RichText の計測/描画には ITextRenderer が要る（HamonRoot 経由）。");

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
