using Hamon.Layout;
using Hamon.Widgets;
using System;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>
/// Performance regression detection: Steady frame allocation (ZeroAlloc convention) and virtualization (only visible items are materialized at 10,000 cases).
/// Since time measurement is dependent on the environment and is unstable, here we deterministically verify the "number of secured bytes" and "number of materialized cells."
/// </summary>
public class PerformanceTests
{
    private sealed class StubTextRenderer : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private sealed class StubPainter : IPainter
    {
        public void BeginFrame()
        {
        }

        public void EndFrame()
        {
        }

        public void FillRect(Rect rect, Color color)
        {
        }

        public void FillRoundedRect(Rect rect, Color color, float radius)
        {
        }

        public void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private static readonly Size Viewport = new(400, 600);

    [Fact]
    public void SteadyState_UpdateAndRender_AllocatesMinimally()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Column
        {
            Children = new Widget[]
            {
                new Text("Title") { FontSize = 22f },
                new Button { OnPressed = () => { }, Background = new Color(40, 44, 54), Child = new Text("OK") },
                new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(20) },
            },
        });
        var painter = new StubPainter();

        // ウォームアップ（reconcile/layout を済ませ、定常状態にする）。
        for (int i = 0; i < 5; i++)
        {
            host.Update(Viewport);
            host.Render(painter);
        }

        const int frames = 200;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < frames; i++)
        {
            host.Update(Viewport); // dirty 無し・サイズ不変＝再構築/再レイアウトなし
            host.Render(painter);
        }

        long perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
        // 定常フレームはほぼゼロアロケ（多少の許容を持たせるが、明確に小さいことを保証）。
        Assert.True(perFrame < 512, $"per-frame allocation = {perFrame} bytes");
    }

    [Fact]
    public void LargeRebuild_ManyChildren_Completes()
    {
        var host = new HamonRoot(new StubTextRenderer());
        int n = 1000;
        host.SetRoot(() =>
        {
            var items = new Widget[n];
            for (int i = 0; i < n; i++)
            {
                items[i] = new SizedBox { Width = Dimension.Px(50), Height = Dimension.Px(2) };
            }

            return new Column { Children = items };
        });

        host.Update(Viewport);
        Assert.Equal(n, host.Root!.Children.Count);

        // 複数回の全再構築でも完了する（スモーク：例外なく回る）。
        for (int r = 0; r < 5; r++)
        {
            host.MarkDirty();
            host.Update(Viewport);
        }

        Assert.Equal(n, host.Root!.Children.Count);
    }

    [Fact]
    public void ListView_TenThousand_RealizesOnlyVisible()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new ListView
        {
            ItemCount = 10000,
            ItemExtent = 40f,
            Builder = i => new SizedBox { Height = Dimension.Px(40) },
        });
        host.Update(Viewport); // viewport 高さ 600 → 可視は ~15 件

        int realized = host.Root!.Children.Count;
        Assert.True(realized < 40, $"realized cells = {realized} (should be ~visible, not 10000)");
        Assert.True(realized > 0);
    }

    [Fact]
    public void GridView_TenThousand_RealizesOnlyVisible()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new GridView
        {
            ItemCount = 10000,
            CrossAxisCount = 4,
            MainAxisExtent = 80f,
            Builder = i => new SizedBox { Height = Dimension.Px(80) },
        });
        host.Update(Viewport); // 600/80 ≒ 8 行 × 4 列 ＋バッファ

        int realized = host.Root!.Children.Count;
        Assert.True(realized < 80, $"realized cells = {realized} (should be ~visible, not 10000)");
        Assert.True(realized > 0);
    }
}
