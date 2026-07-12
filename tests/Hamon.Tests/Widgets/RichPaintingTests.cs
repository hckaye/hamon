using Hamon.Layout;
using Hamon.Widgets;
using System.Collections.Generic;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Verification of rich drawing primitives (line/circle/gradation/shadow/rotation) and the Material/Card/Progress systems that use them.
/// Count issued commands with IPainter for recording (no pixels required).
/// </summary>
public class RichPaintingTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    // 新プリミティブも override して記録する完全な記録器。
    private sealed class Recorder : IPainter
    {
        public int Rects;
        public int RoundedRects;
        public int Lines;
        public int Circles;
        public int Gradients;
        public int Shadows;
        public int RotatedFills;
        public int RotatedTextures;

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color) => Rects++;

        public void FillRoundedRect(Rect rect, Color color, float radius) => RoundedRects++;

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }

        public void DrawLine(Vec2 a, Vec2 b, float thickness, Color color) => Lines++;

        public void FillCircle(Vec2 center, float radius, Color color) => Circles++;

        public void FillGradient(Rect rect, Color a, Color b, GradientAxis axis) => Gradients++;

        public void FillShadow(Rect rect, Color color, float radius, float blur) => Shadows++;

        public void FillRectRotated(Rect rect, Color color, float radians, Vec2 pivot) => RotatedFills++;

        public void DrawTextureRotated(ITexture texture, Rect dest, RectInt source, Color tint, float radians, Vec2 pivot) => RotatedTextures++;

        public int BlendPushes;

        public object? PushBlend(BlendMode mode)
        {
            BlendPushes++;
            return mode;
        }

        public void PopBlend(object? token)
        {
        }
    }

    // 基本プリミティブだけ実装した「簡易バックエンド」（新メソッドは DIM の既定実装に委ねる）。
    private sealed class MinimalPainter : IPainter
    {
        public int Rects;
        public int RoundedRects;

        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color) => Rects++;

        public void FillRoundedRect(Rect rect, Color color, float radius) => RoundedRects++;

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private static readonly Size Viewport = new(200, 200);

    private static Recorder Render(Widget root)
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = root });
        host.Update(Viewport);
        var rec = new Recorder();
        host.Render(rec);
        return rec;
    }

    [Fact]
    public void Material_Elevation_EmitsShadow()
    {
        Recorder rec = Render(new Material
        {
            Elevation = 2f,
            Radius = 8f,
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) },
        });
        Assert.True(rec.Shadows > 0, "elevation>0 で影が描かれる");
        Assert.True(rec.RoundedRects > 0, "面（角丸）が描かれる");
    }

    [Fact]
    public void Material_NoElevation_NoShadow()
    {
        Recorder rec = Render(new Material
        {
            Elevation = 0f,
            Child = new SizedBox { Width = Dimension.Px(80), Height = Dimension.Px(80) },
        });
        Assert.Equal(0, rec.Shadows);
    }

    [Fact]
    public void Material_Gradient_EmitsGradient()
    {
        Recorder rec = Render(new Material
        {
            Color = new Color(20, 30, 40),
            GradientTo = new Color(60, 80, 120),
            Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(60) },
        });
        Assert.True(rec.Gradients > 0, "グラデが描かれる");
    }

    [Fact]
    public void DeterminateProgress_EmitsArcLines()
    {
        Recorder rec = Render(new CircularProgressIndicator { Value = 0.5f, Diameter = 48f });
        Assert.True(rec.Lines > 0, "リング弧は線分で描く");
    }

    [Fact]
    public void Arc_Opaque_AddsRoundJoints()
    {
        var rec = new Recorder();
        var ctx = new PaintContext(rec);
        ctx.Arc(new Vec2(50f, 50f), 20f, 0f, MathF.PI, 6f, Color.White, 8);
        Assert.True(rec.Lines > 0, "弧は線分で描く");
        Assert.True(rec.Circles > 0, "不透明な弧は継ぎ目を丸ジョイント（円）で塞ぐ＝隙間の黒抜けを防ぐ");
    }

    [Fact]
    public void Arc_Translucent_NoRoundJoints()
    {
        var rec = new Recorder();
        var ctx = new PaintContext(rec);
        ctx.Arc(new Vec2(50f, 50f), 20f, 0f, MathF.PI, 6f, new Color(255, 255, 255, 60), 8);
        Assert.True(rec.Lines > 0, "弧は線分で描く");
        Assert.Equal(0, rec.Circles); // 半透明は二重合成ムラを避けて丸ジョイントを付けない
    }

    [Fact]
    public void Spinner_RegistersTicker_AndPaints()
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = new CircularProgressIndicator { Diameter = 32f } });
        host.Update(Viewport);

        // 数フレーム進めても例外なく回り続ける。
        for (int i = 0; i < 5; i++)
        {
            host.Update(Viewport, 0.1f);
        }

        var rec = new Recorder();
        host.Render(rec);
        Assert.True(rec.Lines > 0, "スピナー弧は線分で描く");
    }

    [Fact]
    public void PaintContext_Rotation_RoutesToRotatedFill()
    {
        var rec = new Recorder();
        PaintContext ctx = new PaintContext(rec).WithRotation(0.5f, new Vec2(10f, 10f));
        ctx.FillRect(new Rect(0f, 0f, 20f, 20f), Color.White);
        ctx.FillRoundedRect(new Rect(0f, 0f, 20f, 20f), Color.White, 4f);

        Assert.Equal(2, rec.RotatedFills); // 回転時は FillRect/FillRoundedRect とも回転塗りへ
        Assert.Equal(0, rec.Rects);
        Assert.Equal(0, rec.RoundedRects);
    }

    [Fact]
    public void PaintContext_NoRotation_UsesFastPath()
    {
        var rec = new Recorder();
        var ctx = new PaintContext(rec);
        ctx.FillRect(new Rect(0f, 0f, 20f, 20f), Color.White);
        Assert.Equal(1, rec.Rects);
        Assert.Equal(0, rec.RotatedFills);
    }

    [Fact]
    public void Dim_FillCircle_FallsBackToRoundedRect()
    {
        var min = new MinimalPainter();
        IPainter p = min;
        p.FillCircle(new Vec2(5f, 5f), 5f, Color.White);
        Assert.Equal(1, min.RoundedRects); // 既定実装が角丸矩形で近似
    }

    [Fact]
    public void Dim_FillGradient_FallsBackToFillRect()
    {
        var min = new MinimalPainter();
        IPainter p = min;
        p.FillGradient(new Rect(0f, 0f, 10f, 10f), Color.Black, Color.White, GradientAxis.Vertical);
        Assert.Equal(1, min.Rects); // 既定実装が中間色のベタ塗り
    }

    [Fact]
    public void Dim_FillShadow_IsNoOp()
    {
        var min = new MinimalPainter();
        IPainter p = min;
        p.FillShadow(new Rect(0f, 0f, 10f, 10f), Color.Black, 4f, 6f);
        Assert.Equal(0, min.Rects);
        Assert.Equal(0, min.RoundedRects); // 既定は無描画
    }

    [Fact]
    public void Card_RendersSurface()
    {
        Recorder rec = Render(new Card { Elevation = 1f, Child = new SizedBox { Width = Dimension.Px(120), Height = Dimension.Px(80) } });
        Assert.True(rec.Shadows > 0);
        Assert.True(rec.RoundedRects > 0);
    }

    [Fact]
    public void PushBlend_RoutesToPainter()
    {
        var rec = new Recorder();
        var ctx = new PaintContext(rec);
        object? token = ctx.PushBlend(BlendMode.Additive);
        ctx.PopBlend(token);
        Assert.Equal(1, rec.BlendPushes);
    }

    [Fact]
    public void Dim_PushBlend_IsNoOp()
    {
        var min = new MinimalPainter();
        IPainter p = min;
        Assert.Null(p.PushBlend(BlendMode.Additive)); // 既定（簡易バックエンド）は無視
    }

    private sealed class TextRec : ITextRenderer
    {
        public string? Last;
        public Vec2 LastPos;
        public float LastSize;

        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
            Last = text;
            LastPos = position;
            LastSize = pixelSize;
        }
    }

    [Fact]
    public void DrawText_RoutesToRendererWithScale()
    {
        var tr = new TextRec();
        var ctx = new PaintContext(new Recorder(), tr);
        ctx.DrawText("剣", new Vec2(10f, 20f), 24f, Color.White);
        Assert.Equal("剣", tr.Last);
        Assert.Equal(24f, tr.LastSize); // ScaleY=1（変換なし）
        Assert.Equal(new Vec2(10f, 20f), tr.LastPos);
        Assert.Equal(new Vec2(24f, 24f), ctx.MeasureText("剣", 24f)); // レンダラの計測を返す
    }
}
