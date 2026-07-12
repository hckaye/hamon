using Hamon.Layout;
using Hamon.Testing.Regression;
using Hamon.Testing.Rendering;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Rendering;

/// <summary>
/// Semantic guard that directly fixes the "symptom itself" of past drawing bugs (will always fail if it occurs again).
/// ・Black jaggedness of spinner/ring (background is transparent at Arc segment seam)
/// ・Black haze on the edge of the toast/card (the shadow behind it is visible on the opaque surface)
/// </summary>
public class RenderArtifactGuardTests
{
    private static readonly Color Bg = GoldenImage.DefaultBackground;

    // ---- スピナー/リングの継ぎ目（黒いギザギザ） ----

    [Fact]
    public void RadialRing_FullTrack_HasNoSeamGaps()
    {
        RasterImage img = GoldenImage.Render(80, 80, () => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new CircularProgressIndicator { Value = 0f, Diameter = 64f, StrokeWidth = 8f, BackgroundColor = new Color(120, 140, 200) },
        });

        // フルトラック（360°）は継ぎ目に背景が透けない（丸ジョイントで隙間を塞ぐコア実装の回帰固定）。
        RenderGuards.AssertStrokeContinuity(img, new Vec2(32, 32), radius: 28f, thickness: 8f, background: Bg);
    }

    [Fact]
    public void StrokeContinuityGuard_DetectsRingGap()
    {
        // テストのテスト：全周リングは通り、一部が欠けた（背景が貫通する）リングは確実に検知して落ちること。
        var center = new Vec2(40, 40);
        const float r = 28f;
        const float thickness = 8f;

        var full = new RasterCanvas(80, 80);
        full.Clear(Bg);
        DrawArc(new RasterPainter(full), center, r, thickness, 0f, MathF.PI * 2f, segments: 48);
        RenderGuards.AssertStrokeContinuity(full.ToImage(), center, r, thickness, Bg); // 全周＝通る

        var gapped = new RasterCanvas(80, 80);
        gapped.Clear(Bg);
        DrawArc(new RasterPainter(gapped), center, r, thickness, 0f, MathF.PI * 2f * (300f / 360f), segments: 40); // 60°欠落
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            RenderGuards.AssertStrokeContinuity(gapped.ToImage(), center, r, thickness, Bg));
    }

    // ---- トースト/カードの黒モヤ（不透明面に影が透ける） ----

    [Fact]
    public void ElevatedCard_OpaqueFace_HasSolidInterior_NoShadowBleed()
    {
        RasterImage opaque = RenderCard(new Color(70, 74, 90, 255));
        RenderGuards.AssertSolidInterior(opaque, CardBody, new Color(70, 74, 90, 255), inset: 12, tol: 6);
    }

    [Fact]
    public void ShadowBleedGuard_DetectsHaze_WhenFaceIsTranslucent()
    {
        // 半透明の面は背後の影が透けて内部が暗くなる（モヤ）＝過去バグ。不透明より暗いことを確認（テストのテスト）。
        RasterImage opaque = RenderCard(new Color(70, 74, 90, 255));
        RasterImage translucent = RenderCard(new Color(70, 74, 90, 130));

        double opaqueLuma = RenderGuards.MeanLuma(opaque, CardBody, inset: 12);
        double hazeLuma = RenderGuards.MeanLuma(translucent, CardBody, inset: 12);
        Assert.True(hazeLuma < opaqueLuma - 5, $"半透明面で影の透け（モヤ）が出ていない: opaque={opaqueLuma:F1} translucent={hazeLuma:F1}");

        // 内部一様ガードは半透明面では落ちる（影のムラを検知）。
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            RenderGuards.AssertSolidInterior(translucent, CardBody, new Color(70, 74, 90, 130), inset: 12, tol: 6));
    }

    // ---- 角丸の縦シーム（半透明色で角と帯が二重ブレンドして縦線になる） ----

    [Fact]
    public void TranslucentRoundedRect_HasNoVerticalSeam()
    {
        // 白地に半透明黒の角丸を1枚塗る。重なり無しレイアウトなら内部は一様（過去：角と帯の重複で x≈左右端に暗い縦線）。
        var canvas = new RasterCanvas(200, 120);
        var white = new Color(255, 255, 255, 255);
        canvas.Clear(white);
        var face = new Color(20, 20, 20, 110);
        var rect = new Rect(20, 20, 160, 80);
        new RasterPainter(canvas).FillRoundedRect(rect, face, 20f);

        // 角を避けた内部（inset=r）が、白上に半透明を重ねた一様色であること（縦シーム＝暗い列が無い）。
        RenderGuards.AssertSolidInterior(canvas.ToImage(), rect, BlendOver(face, white), inset: 20, tol: 4);
    }

    [Fact]
    public void SolidInteriorGuard_DetectsVerticalSeam()
    {
        // テストのテスト：同じ列を半透明で二度塗る（＝過去の二重ブレンド）と暗い縦線になり、ガードが確実に落ちること。
        var canvas = new RasterCanvas(200, 120);
        var white = new Color(255, 255, 255, 255);
        canvas.Clear(white);
        var face = new Color(20, 20, 20, 110);
        var rect = new Rect(20, 20, 160, 80);
        var painter = new RasterPainter(canvas);
        painter.FillRoundedRect(rect, face, 20f);
        painter.FillRect(new Rect(40, 20, 1, 80), face); // x=40 の列を二度塗り＝縦シーム捏造

        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            RenderGuards.AssertSolidInterior(canvas.ToImage(), rect, BlendOver(face, white), inset: 20, tol: 4));
    }

    private static Color BlendOver(Color src, Color dst)
    {
        int a = src.A;
        int inv = 255 - a;
        return new Color(
            (byte)(((src.R * a) + (dst.R * inv) + 127) / 255),
            (byte)(((src.G * a) + (dst.G * inv) + 127) / 255),
            (byte)(((src.B * a) + (dst.B * inv) + 127) / 255),
            255);
    }

    private static readonly Rect CardBody = new(40, 36, 160, 48);

    private static RasterImage RenderCard(Color face) => GoldenImage.Render(240, 120, () => new Align
    {
        Alignment = Alignment.Center,
        Child = new Material
        {
            Color = face,
            Radius = 20f,
            Elevation = 4f,
            Child = new SizedBox { Width = Dimension.Px(160), Height = Dimension.Px(48) },
        },
    });

    private static void DrawArc(RasterPainter p, Vec2 center, float r, float thickness, float start, float end, int segments)
    {
        var color = new Color(120, 140, 200);
        float step = (end - start) / segments;
        Vec2 prev = new(center.X + (MathF.Cos(start) * r), center.Y + (MathF.Sin(start) * r));
        p.FillCircle(prev, thickness * 0.5f, color);
        for (int i = 1; i <= segments; i++)
        {
            float ang = start + (step * i);
            Vec2 next = new(center.X + (MathF.Cos(ang) * r), center.Y + (MathF.Sin(ang) * r));
            p.DrawLine(prev, next, thickness, color);
            p.FillCircle(next, thickness * 0.5f, color);
            prev = next;
        }
    }
}
