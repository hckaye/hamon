using Hamon.Layout;
using Hamon.Testing.Rendering;
using Xunit;

namespace Hamon.Testing.Regression;

/// <summary>
/// past drawing bugs<b>the symptoms themselves</b>A semantic guard that directly tests for (golden matches plus reliably dropping recurrences).
/// Run against real pixel output. <see cref="RasterPainter"/>composition is strict).
/// </summary>
public static class RenderGuards
{
    /// <summary>
    /// Opaque widget rectangle<b>outer ring</b>Verify that there are no pixels that are clearly darker than either the content color or the background color.
    /// → Fixed the black haze on the edges of the toast/the dark fringes caused by alpha compositing errors.<b>Not used for shadow widgets</b>(Shadows are intentionally dark).
    /// </summary>
    public static void AssertNoDarkHalo(
        RasterImage image,
        Rect contentRect,
        Color background,
        Color content,
        int ringWidth = 3,
        int darkerBy = 24,
        int maxBadPixels = 0)
    {
        int refLuma = Math.Min(Luma(background), Luma(content));
        int threshold = refLuma - darkerBy;

        int x0 = (int)MathF.Floor(contentRect.X) - ringWidth;
        int y0 = (int)MathF.Floor(contentRect.Y) - ringWidth;
        int x1 = (int)MathF.Ceiling(contentRect.Right) + ringWidth;
        int y1 = (int)MathF.Ceiling(contentRect.Bottom) + ringWidth;

        int bad = 0;
        var worst = (x: -1, y: -1, luma: 999);
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
                {
                    continue;
                }

                // リング（矩形の外側 ringWidth 帯）だけを見る＝コンテンツ内部は除外。
                bool inside = x >= contentRect.X && x < contentRect.Right && y >= contentRect.Y && y < contentRect.Bottom;
                if (inside)
                {
                    continue;
                }

                int luma = Luma(image.GetPixel(x, y));
                if (luma < threshold)
                {
                    bad++;
                    if (luma < worst.luma)
                    {
                        worst = (x, y, luma);
                    }
                }
            }
        }

        Assert.True(
            bad <= maxBadPixels,
            $"黒モヤ/暗フリンジ検出: 外周に暗い画素 {bad} 個 (許容 {maxBadPixels})。" +
            $"最暗 ({worst.x},{worst.y}) luma={worst.luma} < しきい値 {threshold}（背景/コンテンツ基準 {refLuma}）");
    }

    /// <summary>
    /// circular stroke<b>outer edge</b>The image is scanned at a fine angle to verify that there are no gaps (seams) where the background penetrates.
    /// → Fixed black jag (Arc segment seam) of spinner/arc progress.
    /// </summary>
    public static void AssertStrokeContinuity(
        RasterImage image,
        Vec2 center,
        float radius,
        float thickness,
        Color background,
        int angleSamples = 1440,
        int bgTolerance = 24,
        int maxGapSamples = 2)
    {
        float sampleR = radius + (thickness * 0.5f) - 1.5f; // 外縁のわずか内側（継ぎ目の切欠きが現れる位置）
        int gaps = 0;
        float worstAngle = -1f;
        for (int i = 0; i < angleSamples; i++)
        {
            double a = i * 2.0 * Math.PI / angleSamples;
            int px = (int)MathF.Round(center.X + ((float)Math.Cos(a) * sampleR));
            int py = (int)MathF.Round(center.Y + ((float)Math.Sin(a) * sampleR));
            if (px < 0 || py < 0 || px >= image.Width || py >= image.Height)
            {
                continue;
            }

            if (IsBackground(image.GetPixel(px, py), background, bgTolerance))
            {
                gaps++;
                if (worstAngle < 0f)
                {
                    worstAngle = (float)(a * 180.0 / Math.PI);
                }
            }
        }

        Assert.True(
            gaps <= maxGapSamples,
            $"ストローク継ぎ目/隙間検出: 外縁に背景貫通 {gaps}/{angleSamples} サンプル (許容 {maxGapSamples})。" +
            $"最初の隙間 ≈ {worstAngle:F1}°（半径 {sampleR:F1}）");
    }

    /// <summary>
    /// rectangle<b>internal</b>(from the edge<paramref name="inset"/>Verify that the area (reduced area) is filled with the expected color.
    /// → Fixed a regression in shadowed cards/toasts where the shadow behind the opaque surface shows through and the interior becomes dark (moya) (if it is opaque, the interior is uniform).
    /// It is used in cases where the inside is plain (padding strip/plain card) because it will falsely detect if the child has drawings such as text.
    /// </summary>
    public static void AssertSolidInterior(
        RasterImage image,
        Rect rect,
        Color expected,
        int inset = 6,
        int tol = 6,
        int maxBadPixels = 0)
    {
        int x0 = (int)MathF.Ceiling(rect.X) + inset;
        int y0 = (int)MathF.Ceiling(rect.Y) + inset;
        int x1 = (int)MathF.Floor(rect.Right) - inset;
        int y1 = (int)MathF.Floor(rect.Bottom) - inset;

        int bad = 0;
        var worst = (x: -1, y: -1, diff: 0);
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
                {
                    continue;
                }

                Color c = image.GetPixel(x, y);
                int diff = Math.Max(Math.Max(Math.Abs(c.R - expected.R), Math.Abs(c.G - expected.G)), Math.Abs(c.B - expected.B));
                if (diff > tol)
                {
                    bad++;
                    if (diff > worst.diff)
                    {
                        worst = (x, y, diff);
                    }
                }
            }
        }

        Assert.True(
            bad <= maxBadPixels,
            $"内部の非一様/暗化検出（モヤの疑い）: 想定色から外れる画素 {bad} 個 (許容 {maxBadPixels})。" +
            $"最差 ({worst.x},{worst.y}) Δ={worst.diff} > 許容 {tol}");
    }

    /// <summary>Average brightness inside a rectangle (assistance of haze detection/quantification of shadow transparency).</summary>
    public static double MeanLuma(RasterImage image, Rect rect, int inset = 6)
    {
        int x0 = (int)MathF.Ceiling(rect.X) + inset;
        int y0 = (int)MathF.Ceiling(rect.Y) + inset;
        int x1 = (int)MathF.Floor(rect.Right) - inset;
        int y1 = (int)MathF.Floor(rect.Bottom) - inset;
        long sum = 0;
        long n = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
                {
                    continue;
                }

                sum += Luma(image.GetPixel(x, y));
                n++;
            }
        }

        return n == 0 ? 0 : (double)sum / n;
    }

    private static int Luma(Color c) => ((c.R * 2) + (c.G * 3) + c.B) * c.A / 255 / 6;

    private static bool IsBackground(Color c, Color bg, int tol) =>
        Math.Abs(c.R - bg.R) <= tol && Math.Abs(c.G - bg.G) <= tol && Math.Abs(c.B - bg.B) <= tol;
}
