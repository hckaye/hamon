using Hamon;
using Hamon.Layout;
using Hamon.Testing.Regression;
using Hamon.Testing.Rendering;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// <see cref="CooldownButton.ProgressGetter"/>(CD of reading when drawing) determinism test.
/// Buttons are not rebuilt when progress changes and presses are<c>getter() &gt;= 1</c>Only holds true when (= usable).
/// </summary>
public class CooldownButtonTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static void Tap(HamonRoot host, float x, float y)
    {
        host.DispatchPointer(new PointerEvent(new Vec2(x, y), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(x, y), PointerPhase.Up, 0.02f));
    }

    [Fact]
    public void ProgressGetter_GatesPressUntilReady_WithoutRebuild()
    {
        float readiness = 0.3f; // CD 中
        int builds = 0;
        int presses = 0;
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Align
        {
            Alignment = Alignment.TopLeft,
            Child = new GestureProbe
            {
                OnBuild = () => builds++,
                Child = new CooldownButton
                {
                    ProgressGetter = () => readiness,
                    OnPressed = () => presses++,
                    Size = 50f,
                },
            },
        });
        host.Update(new Size(100, 100));
        int buildsAfterMount = builds;

        // CD 中はタップしても発火しない（getter を押下時に読んで gate）。
        Tap(host, 25f, 25f);
        Assert.Equal(0, presses);

        // 進捗を進めても（getter のバック値を変えるだけ・atom/State 不使用）ボタンは再構築されない。
        readiness = 1f;
        host.Update(new Size(100, 100));
        Assert.Equal(buildsAfterMount, builds);

        // ready になればタップで発火する。
        Tap(host, 25f, 25f);
        Assert.Equal(1, presses);
    }

    // CD 暗幕の<b>スライド境界（下辺）は常に水平直線</b>であること（残量が高く角丸の下隅ゾーンに入っても丸まらない）の
    // ピクセル回帰。暗幕を角丸矩形のまま縮めると下隅がスライドして波打つ退行を固定する。残量を 0.9（tall・下隅ゾーン）に
    // して、暗幕中央の行と下端付近の行で「暗幕色で塗られた幅」が一致する＝下辺が full-width 直線であることを確かめる。
    [Theory]
    [InlineData(0.9f)]
    [InlineData(0.95f)]
    public void Cover_BottomEdge_IsStraight_EvenWhenTall(float remaining)
    {
        const int size = 56;
        var cover = new Color(255, 0, 255, 255); // 不透明＝判定が明瞭
        RasterImage img = GoldenImage.Render(
            size,
            size,
            () => new CooldownButton
            {
                ProgressGetter = () => 1f - remaining,
                OverlayColor = cover,
                Background = new Color(255, 255, 255, 255),
                Size = size,
                Radius = 10f,
            },
            background: new Color(0, 0, 0, 255));

        int CoverWidth(int y)
        {
            int n = 0;
            for (int x = 0; x < size; x++)
            {
                Color c = img.GetPixel(x, y);
                if (c.R > 200 && c.G < 60 && c.B > 200)
                {
                    n++;
                }
            }

            return n;
        }

        int coverBottom = (int)(size * remaining);
        int midWidth = CoverWidth(size / 4);   // 暗幕中央付近（角丸の中段＝full-width）
        int lowWidth = CoverWidth(coverBottom - 2); // 下端の少し上（半径=10 の下隅ゾーン内）

        // 下隅ゾーンでも暗幕は full-width のまま＝境界は水平直線。角丸が滑っていれば下端側が細る。
        Assert.True(midWidth >= size - 2, $"mid row should span full width but was {midWidth}");
        Assert.True(lowWidth >= midWidth - 2, $"bottom edge rounded in (low={lowWidth}, mid={midWidth})");
    }

    // Build 毎にカウントする観測用パススルー（再構築が起きていないことの確認用）。
    private sealed class GestureProbe : StatelessWidget
    {
        public required Widget Child { get; init; }

        public required Action OnBuild { get; init; }

        public override Widget Build(BuildContext context)
        {
            OnBuild();
            return Child;
        }
    }
}
