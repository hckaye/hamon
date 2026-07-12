using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// Context to pass to the tree on the drawing pass.
/// <see cref="IPainter"/>(backend independent). <see cref="ITextRenderer"/>via.
/// Value type (no additional allocation per frame).
/// </summary>
public readonly struct PaintContext
{
    private readonly float _opacity;
    private readonly Transform2D _transform;
    private readonly float _rotation;       // ラジアン（0=回転なし＝軸並行の高速パス）
    private readonly Vec2 _rotationPivot;    // 回転中心（デバイス空間）

    public PaintContext(IPainter painter, ITextRenderer? text = null)
        : this(painter, text, 1f, Transform2D.Identity, 0f, Vec2.Zero)
    {
    }

    private PaintContext(IPainter painter, ITextRenderer? text, float opacity, Transform2D transform, float rotation, Vec2 rotationPivot)
    {
        Painter = painter;
        Text = text;
        _opacity = opacity;
        _transform = transform;
        _rotation = rotation;
        _rotationPivot = rotationPivot;
    }

    /// <summary>drawing backend.</summary>
    public IPainter Painter { get; }

    /// <summary>Text measurement/drawing (without it, the Text widget cannot be drawn).</summary>
    public ITextRenderer? Text { get; }

    /// <summary>Current opacity (0..1).</summary>
    public float CurrentOpacity => _opacity;

    /// <summary>Vertical scale of the current composite transform (used for scaling the size of text, etc.).</summary>
    public float ScaleY => _transform.Scale.Y;

    /// <summary>Horizontal scale of the current composite transform.</summary>
    public float ScaleX => _transform.Scale.X;

    /// <summary>Returns the context of child drawing multiplied by opacity (<see cref="Opacity"/>).</summary>
    public PaintContext WithOpacity(float factor) =>
        new(Painter, Text, _opacity * Math.Clamp(factor, 0f, 1f), _transform, _rotation, _rotationPivot);

    /// <summary>Returns a context loaded with transformations on child drawings (<see cref="Transform"/>). </summary>
    internal PaintContext WithTransform(in Transform2D transform) =>
        new(Painter, Text, _opacity, Transform2D.Compose(_transform, transform), _rotation, _rotationPivot);

    /// <summary>
    /// Returns the context loaded with rotation for the child's drawing (<see cref="Transform"/>rotation).<paramref name="pivotLocal"/>is the local coordinate fulcrum
    /// (Copy to device space with current transformation).
    /// Non-uniform scale + rotation nesting is not applicable (approximation).
    /// </summary>
    internal PaintContext WithRotation(float radians, Vec2 pivotLocal)
    {
        if (radians == 0f)
        {
            return this;
        }

        Vec2 pivotDevice = _transform.Apply(pivotLocal);
        Vec2 pivot = _rotation != 0f ? RotateAbout(pivotDevice, _rotationPivot, _rotation) : pivotDevice;
        return new(Painter, Text, _opacity, _transform, _rotation + radians, pivot);
    }

    private static Vec2 RotateAbout(Vec2 p, Vec2 pivot, float radians)
    {
        float s = MathF.Sin(radians);
        float c = MathF.Cos(radians);
        float dx = p.X - pivot.X;
        float dy = p.Y - pivot.Y;
        return new Vec2(pivot.X + (dx * c) - (dy * s), pivot.Y + (dx * s) + (dy * c));
    }

    /// <summary>Multiply the color by the current opacity (alpha only, keep RGB to avoid fading to black).</summary>
    public Color ApplyOpacity(Color color) =>
        new(color.R, color.G, color.B, (int)Math.Clamp(color.A * _opacity, 0f, 255f));

    /// <summary>Applies the current compositing transformation to a point (such as text position).</summary>
    public Vec2 ApplyTransform(Vec2 point) => _transform.Apply(point);

    /// <summary>Apply the current composite transformation to the rectangle and copy it to device space (raw drawing =<see cref="SceneDrawContext"/>etc.)</summary>
    public Rect ApplyTransform(Rect rect) => _transform.Apply(rect);

    /// <summary>Draw text from custom elements (composite transform/opacity/scale).<paramref name="position"/>is the top left of the UI coordinates.</summary>
    public void DrawText(string text, Vec2 position, float pixelSize, Color color, string? font = null) =>
        Text?.Draw(text, _transform.Apply(position), pixelSize * ScaleY, ApplyOpacity(color), font);

    /// <summary>Text dimensions (UI coordinates, unscaled). </summary>
    public Vec2 MeasureText(string text, float pixelSize, string? font = null) => Text?.Measure(text, pixelSize, font) ?? default;

    /// <summary>Stack rectangular clip (return opaque token<see cref="PopClip"/>fart).</summary>
    public object? PushClip(Rect rect) => Painter.PushClip(_transform.Apply(rect));

    public void PopClip(object? token) => Painter.PopClip(token);

    /// <summary>Accumulate compositing mode (additive compositing for glow, etc.) Return value token<see cref="PopBlend"/>fart).</summary>
    public object? PushBlend(BlendMode mode) => Painter.PushBlend(mode);

    public void PopBlend(object? token) => Painter.PopBlend(token);

    /// <summary>Paints a rectangle with a single color (applies current transform, opacity, and rotation).</summary>
    public void FillRect(Rect rect, Color color)
    {
        if (_rotation != 0f)
        {
            Painter.FillRectRotated(_transform.Apply(rect), ApplyOpacity(color), _rotation, _rotationPivot);
            return;
        }

        Painter.FillRect(_transform.Apply(rect), ApplyOpacity(color));
    }

    /// <summary>Paint the rounded rectangle with a single color (the radius is also scaled with the conversion scale). </summary>
    public void FillRoundedRect(Rect rect, Color color, float radius)
    {
        if (_rotation != 0f)
        {
            Painter.FillRectRotated(_transform.Apply(rect), ApplyOpacity(color), _rotation, _rotationPivot);
            return;
        }

        Painter.FillRoundedRect(_transform.Apply(rect), ApplyOpacity(color), radius * _transform.Scale.X);
    }

    /// <summary>Draw the entire texture into a rectangle (applying the current transformation, opacity, rotation,<paramref name="tint"/>multiplication).</summary>
    public void DrawTexture(ITexture texture, Rect dest, Color tint) =>
        DrawTexture(texture, dest, new RectInt(0, 0, texture.Width, texture.Height), tint);

    /// <summary>Draw a partial rectangle of the texture (sprite sheet, etc.) into a rectangle.</summary>
    public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
    {
        if (_rotation != 0f)
        {
            Painter.DrawTextureRotated(texture, _transform.Apply(dest), source, ApplyOpacity(tint), _rotation, _rotationPivot);
            return;
        }

        Painter.DrawTexture(texture, _transform.Apply(dest), source, ApplyOpacity(tint));
    }

    /// <summary>
    /// Texture with 9-slice (9-patch)<paramref name="dest"/>(Corners are original size, sides are expanded and contracted, and center is both expanded and contracted).
    /// For windows/button backgrounds/scroll bars, etc.<paramref name="border"/>is the texture frame width (px).<paramref name="source"/>By designation
    /// A partial area of ​​a sprite sheet can be used as a 9-slice source (the entire texture is unspecified).
    /// </summary>
    public void DrawNineSlice(ITexture texture, Rect dest, EdgeInsets border, Color tint, RectInt? source = null)
    {
        int sx0 = source?.X ?? 0;
        int sy0 = source?.Y ?? 0;
        int tw = source?.Width ?? texture.Width;
        int th = source?.Height ?? texture.Height;
        int l = (int)border.Left;
        int t = (int)border.Top;
        int r = (int)border.Right;
        int bm = (int)border.Bottom;

        Span<int> sx = stackalloc int[4] { sx0, sx0 + l, sx0 + tw - r, sx0 + tw };
        Span<int> sy = stackalloc int[4] { sy0, sy0 + t, sy0 + th - bm, sy0 + th };
        Span<float> dx = stackalloc float[4] { dest.X, dest.X + border.Left, dest.Right - border.Right, dest.Right };
        Span<float> dy = stackalloc float[4] { dest.Y, dest.Y + border.Top, dest.Bottom - border.Bottom, dest.Bottom };

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var src = new RectInt(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]);
                if (src.Width <= 0 || src.Height <= 0)
                {
                    continue;
                }

                DrawTexture(texture, new Rect(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]), src, tint);
            }
        }
    }

    /// <summary>Thickness<paramref name="thickness"/>Draw line segments (for charts/separators/arcs, etc. Apply rotation/scale).</summary>
    public void DrawLine(Vec2 a, Vec2 b, float thickness, Color color)
    {
        Vec2 da = _transform.Apply(a);
        Vec2 db = _transform.Apply(b);
        if (_rotation != 0f)
        {
            da = RotateAbout(da, _rotationPivot, _rotation);
            db = RotateAbout(db, _rotationPivot, _rotation);
        }

        Painter.DrawLine(da, db, thickness * _transform.Scale.X, ApplyOpacity(color));
    }

    /// <summary>Draw a filled circle (dots/spinners/chart points, etc.).</summary>
    public void FillCircle(Vec2 center, float radius, Color color)
    {
        Vec2 c = _transform.Apply(center);
        if (_rotation != 0f)
        {
            c = RotateAbout(c, _rotationPivot, _rotation);
        }

        Painter.FillCircle(c, radius * _transform.Scale.X, ApplyOpacity(color));
    }

    /// <summary>Paint a rectangle with a two-color gradation (ignoring rotation = gradation parallel to the axis).</summary>
    public void FillGradient(Rect rect, Color a, Color b, GradientAxis axis) =>
        Painter.FillGradient(_transform.Apply(rect), ApplyOpacity(a), ApplyOpacity(b), axis);

    /// <summary>
    /// Paint with multi-stop linear gradation. <see cref="FillGradient"/>) and synthesize
    /// (backend independent). <see cref="GradientStop.Position"/>Assuming ascending order.
    /// </summary>
    public void FillGradientStops(Rect rect, GradientStop[] stops, GradientAxis axis)
    {
        if (stops is null || stops.Length == 0)
        {
            return;
        }

        if (stops.Length == 1)
        {
            FillRect(rect, stops[0].Color);
            return;
        }

        bool vertical = axis == GradientAxis.Vertical;
        float first = Math.Clamp(stops[0].Position, 0f, 1f);
        float last = Math.Clamp(stops[stops.Length - 1].Position, 0f, 1f);

        if (first > 0f)
        {
            FillRect(Slice(rect, 0f, first, vertical), stops[0].Color); // 先頭より手前は端色
        }

        for (int i = 0; i < stops.Length - 1; i++)
        {
            float p0 = Math.Clamp(stops[i].Position, 0f, 1f);
            float p1 = Math.Clamp(stops[i + 1].Position, 0f, 1f);
            if (p1 > p0)
            {
                FillGradient(Slice(rect, p0, p1, vertical), stops[i].Color, stops[i + 1].Color, axis);
            }
        }

        if (last < 1f)
        {
            FillRect(Slice(rect, last, 1f, vertical), stops[stops.Length - 1].Color); // 末尾より先は端色
        }
    }

    private static Rect Slice(Rect r, float a, float b, bool vertical) => vertical
        ? new Rect(r.X, r.Y + (r.Height * a), r.Width, r.Height * (b - a))
        : new Rect(r.X + (r.Width * a), r.Y, r.Width * (b - a), r.Height);

    /// <summary>Casts a soft shadow (elevation) behind the rectangle.<paramref name="blur"/>is the width that bleeds outward.</summary>
    public void DrawShadow(Rect rect, Color color, float radius, float blur) =>
        Painter.FillShadow(_transform.Apply(rect), ApplyOpacity(color), radius * _transform.Scale.X, blur * _transform.Scale.X);

    /// <summary>Draw a frame line for a rectangle with rounded corners (approximating the four straight sides + four corner arcs as line segments).</summary>
    public void StrokeRoundedRect(Rect rect, Color color, float radius, float thickness)
    {
        float r = Math.Clamp(radius, 0f, Math.Min(rect.Width, rect.Height) / 2f);
        if (r <= 0f)
        {
            DrawOutline(rect, color, thickness);
            return;
        }

        float l = rect.X;
        float t = rect.Y;
        float rt = rect.Right;
        float bt = rect.Bottom;
        DrawLine(new Vec2(l + r, t), new Vec2(rt - r, t), thickness, color);       // 上
        DrawLine(new Vec2(l + r, bt), new Vec2(rt - r, bt), thickness, color);     // 下
        DrawLine(new Vec2(l, t + r), new Vec2(l, bt - r), thickness, color);       // 左
        DrawLine(new Vec2(rt, t + r), new Vec2(rt, bt - r), thickness, color);     // 右
        Arc(new Vec2(l + r, t + r), r, MathF.PI, MathF.PI * 1.5f, thickness, color);       // 左上
        Arc(new Vec2(rt - r, t + r), r, MathF.PI * 1.5f, MathF.PI * 2f, thickness, color); // 右上
        Arc(new Vec2(rt - r, bt - r), r, 0f, MathF.PI * 0.5f, thickness, color);           // 右下
        Arc(new Vec2(l + r, bt - r), r, MathF.PI * 0.5f, MathF.PI, thickness, color);      // 左下
    }

    /// <summary>center<paramref name="center"/>·radius<paramref name="radius"/>Draw the arc of as a line segment (ring/arc progress/corner of frame).</summary>
    public void Arc(Vec2 center, float radius, float startRadians, float endRadians, float thickness, Color color, int segments = 12)
    {
        if (segments < 1)
        {
            segments = 1;
        }

        // 弧は直線セグメントの連なりで描くため、継ぎ目に微小な隙間が空いて背景が透けやすい（黒い点/線に見える）。
        // 不透明色のときは各継ぎ目に丸ジョイント（半径=太さ/2 の円）を置いて隙間を塞ぎ、丸い端点にする。
        // 半透明色では円と線が重なって二重合成のムラになるため丸ジョイントは付けない（隙間も目立たない）。
        bool round = color.A >= 255;
        float cap = thickness * 0.5f;
        float step = (endRadians - startRadians) / segments;
        Vec2 prev = new(center.X + (MathF.Cos(startRadians) * radius), center.Y + (MathF.Sin(startRadians) * radius));
        if (round)
        {
            FillCircle(prev, cap, color);
        }

        for (int i = 1; i <= segments; i++)
        {
            float ang = startRadians + (step * i);
            Vec2 next = new(center.X + (MathF.Cos(ang) * radius), center.Y + (MathF.Sin(ang) * radius));
            DrawLine(prev, next, thickness, color);
            if (round)
            {
                FillCircle(next, cap, color);
            }

            prev = next;
        }
    }

    /// <summary>Draw a rectangular frame (focus ring, etc.).</summary>
    public void DrawOutline(Rect rect, Color color, float thickness)
    {
        FillRect(new Rect(rect.X, rect.Y, rect.Width, thickness), color);
        FillRect(new Rect(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        FillRect(new Rect(rect.X, rect.Y, thickness, rect.Height), color);
        FillRect(new Rect(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
