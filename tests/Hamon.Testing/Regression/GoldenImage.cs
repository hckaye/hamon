using Hamon.Layout;
using Hamon.Testing.Rendering;
using Hamon.Widgets;
using System.Runtime.CompilerServices;
using Xunit;

namespace Hamon.Testing.Regression;

/// <summary>
/// The core of image regression.
/// Match with error tolerance. <c>.actual.png</c>and<c>.diff.png</c>Outputs next to golden and fails.
/// environmental variables<c>HAMON_UPDATE_GOLDENS=1</c>(re)generate golden with .
/// </summary>
public static class GoldenImage
{
    /// <summary>Default background color (mimics a dark app background).</summary>
    public static readonly Color DefaultBackground = new(18, 18, 22, 255);

    public static bool UpdateMode =>
        Environment.GetEnvironmentVariable("HAMON_UPDATE_GOLDENS") is "1" or "true" or "TRUE";

    /// <summary>Draws the widget into a width×height buffer and returns an image.<paramref name="drive"/>is the operation before capture (input + update only, no render).</summary>
    public static RasterImage Render(
        int width,
        int height,
        Func<Widget> build,
        Color? background = null,
        HamonTheme? theme = null,
        Action<HamonRoot>? drive = null) =>
        Render(width, height, _ => build(), background, theme, drive);

    /// <summary>build is<see cref="HamonRoot"/>(Used for constructing Controller=TextField etc. that requires host).</summary>
    public static RasterImage Render(
        int width,
        int height,
        Func<HamonRoot, Widget> build,
        Color? background = null,
        HamonTheme? theme = null,
        Action<HamonRoot>? drive = null)
    {
        var canvas = new RasterCanvas(width, height);
        var text = new HeadlessFontRenderer(canvas, TestAssets.DefaultFont);
        // golden は不透明・決定論で固定したいため Hamon Dark を既定にする（アプリ既定の波紋ライトは半透明面を含む）。
        var host = new HamonRoot(text) { Theme = theme ?? HamonTheme.Dark };
        host.SetRoot(() => build(host));
        host.Update(new Size(width, height));
        drive?.Invoke(host);

        canvas.Clear(background ?? DefaultBackground);
        host.Render(new RasterPainter(canvas));
        return canvas.ToImage();
    }

    /// <summary>Copy the image to golden (<c>Goldens/&lt;category&gt;/&lt;name&gt;.png</c>).</summary>
    public static void AssertMatches(
        RasterImage actual,
        string category,
        string name,
        int tolerance = 4,
        double maxDiffRatio = 0.0,
        [CallerFilePath] string callerFile = "")
    {
        string path = TestPaths.GoldenPath(callerFile, category, name);

        if (UpdateMode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, PngCodec.Encode(actual));
            return;
        }

        if (!File.Exists(path))
        {
            string actualPath = WriteSibling(path, ".actual.png", actual);
            Assert.Fail($"golden 不在: {path}\n  HAMON_UPDATE_GOLDENS=1 で生成してください。実画像: {actualPath}");
            return;
        }

        RasterImage golden = PngCodec.Decode(File.ReadAllBytes(path));
        if (golden.Width != actual.Width || golden.Height != actual.Height)
        {
            string actualPath = WriteSibling(path, ".actual.png", actual);
            Assert.Fail($"画像サイズ不一致: golden {golden.Width}x{golden.Height} != actual {actual.Width}x{actual.Height}\n  実画像: {actualPath}");
            return;
        }

        int bad = 0;
        int maxDiff = 0;
        byte[] diff = new byte[actual.Rgba.Length];
        for (int i = 0; i < actual.Rgba.Length; i += 4)
        {
            int dr = Math.Abs(actual.Rgba[i] - golden.Rgba[i]);
            int dg = Math.Abs(actual.Rgba[i + 1] - golden.Rgba[i + 1]);
            int db = Math.Abs(actual.Rgba[i + 2] - golden.Rgba[i + 2]);
            int da = Math.Abs(actual.Rgba[i + 3] - golden.Rgba[i + 3]);
            int d = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
            maxDiff = Math.Max(maxDiff, d);

            if (d > tolerance)
            {
                bad++;
                diff[i] = 255;
                diff[i + 1] = 0;
                diff[i + 2] = 0;
                diff[i + 3] = 255;
            }
            else
            {
                byte g = (byte)((actual.Rgba[i] + actual.Rgba[i + 1] + actual.Rgba[i + 2]) / 6); // 文脈用に薄いグレー
                diff[i] = g;
                diff[i + 1] = g;
                diff[i + 2] = g;
                diff[i + 3] = 255;
            }
        }

        int total = actual.Width * actual.Height;
        double ratio = (double)bad / total;
        if (ratio > maxDiffRatio)
        {
            string actualPath = WriteSibling(path, ".actual.png", actual);
            string diffPath = WriteSibling(path, ".diff.png", new RasterImage(actual.Width, actual.Height, diff));
            Assert.Fail(
                $"画像リグレッション: {category}/{name}\n" +
                $"  差分ピクセル {bad}/{total} (率 {ratio:P3} > 許容 {maxDiffRatio:P3}), 最大チャンネル差 {maxDiff} (許容 {tolerance})\n" +
                $"  golden: {path}\n  actual: {actualPath}\n  diff:   {diffPath}");
        }
    }

    /// <summary>One-shot helper to draw and instantly compare golden.</summary>
    public static void AssertRender(
        int width,
        int height,
        Func<Widget> build,
        string category,
        string name,
        Color? background = null,
        HamonTheme? theme = null,
        Action<HamonRoot>? drive = null,
        int tolerance = 4,
        double maxDiffRatio = 0.0,
        [CallerFilePath] string callerFile = "")
    {
        RasterImage image = Render(width, height, build, background, theme, drive);
        AssertMatches(image, category, name, tolerance, maxDiffRatio, callerFile);
    }

    /// <summary>build is<see cref="HamonRoot"/>(for constructing Controllers such as TextField).</summary>
    public static void AssertRender(
        int width,
        int height,
        Func<HamonRoot, Widget> build,
        string category,
        string name,
        Color? background = null,
        HamonTheme? theme = null,
        Action<HamonRoot>? drive = null,
        int tolerance = 4,
        double maxDiffRatio = 0.0,
        [CallerFilePath] string callerFile = "")
    {
        RasterImage image = Render(width, height, build, background, theme, drive);
        AssertMatches(image, category, name, tolerance, maxDiffRatio, callerFile);
    }

    private static string WriteSibling(string goldenPath, string suffix, RasterImage image)
    {
        string dir = Path.GetDirectoryName(goldenPath)!;
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(goldenPath) + suffix);
        File.WriteAllBytes(path, PngCodec.Encode(image));
        return path;
    }
}
