using Hamon.Layout;
using Hamon.Testing.Perf;
using Hamon.Testing.Regression;
using Hamon.Testing.Rendering;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Rendering;

/// <summary>
/// The health of the image/performance regression infrastructure itself (golden independent).
/// Confirm that real fonts can be drawn, PNG can be reciprocated, and PerfHarness can be measured.
/// </summary>
public class RenderingFoundationTests
{
    [Fact]
    public void Png_RoundTrips_Pixels()
    {
        byte[] rgba = new byte[4 * 4 * 4];
        for (int i = 0; i < rgba.Length; i++)
        {
            rgba[i] = (byte)((i * 37) % 256);
        }

        var src = new RasterImage(4, 4, rgba);
        RasterImage back = PngCodec.Decode(PngCodec.Encode(src));

        Assert.Equal(src.Width, back.Width);
        Assert.Equal(src.Height, back.Height);
        Assert.Equal(src.Rgba, back.Rgba);
    }

    [Fact]
    public void Render_Container_FillsExpectedColor()
    {
        var color = new Color(200, 80, 40, 255);
        RasterImage img = GoldenImage.Render(120, 80, () => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Container { Width = Dimension.Px(80), Height = Dimension.Px(40), Color = color },
        });

        Color center = img.GetPixel(40, 20);
        Assert.Equal(color.R, center.R);
        Assert.Equal(color.G, center.G);
        Assert.Equal(color.B, center.B);

        // 矩形の外（右下隅）は背景のまま。
        Color outside = img.GetPixel(110, 70);
        Assert.Equal(GoldenImage.DefaultBackground.R, outside.R);
        Assert.Equal(GoldenImage.DefaultBackground.G, outside.G);
    }

    [Fact]
    public void Render_Text_ProducesForegroundPixels()
    {
        RasterImage img = GoldenImage.Render(200, 60, () => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new Text("Hello") { FontSize = 32f },
        });

        int foreground = 0;
        for (int y = 0; y < 60; y++)
        {
            for (int x = 0; x < 200; x++)
            {
                Color c = img.GetPixel(x, y);
                int diff = Math.Abs(c.R - GoldenImage.DefaultBackground.R) +
                           Math.Abs(c.G - GoldenImage.DefaultBackground.G) +
                           Math.Abs(c.B - GoldenImage.DefaultBackground.B);
                if (diff > 60)
                {
                    foreground++;
                }
            }
        }

        Assert.True(foreground > 50, $"実フォントの前景ピクセルが少なすぎる: {foreground}");
    }

    [Fact]
    public void PerfHarness_Measures_SteadyState()
    {
        PerfReport report = PerfHarness.Measure(
            () => new Column
            {
                Children = new Widget[]
                {
                    new Text("Title") { FontSize = 22f },
                    new Button { OnPressed = () => { }, Background = new Color(40, 44, 54), Child = new Text("OK") },
                },
            },
            new Size(400, 600),
            perFrame: InputScript.None,
            frames: 60);

        Assert.Equal(60, report.PerFrameAllocBytes.Length);
        Assert.True(report.MaxDrawCalls > 0, "描画コールが記録されていない");
        report.AssertSteadyAllocBelow(2048); // 定常はごく小さい
    }
}
