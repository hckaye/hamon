using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Verification of Scrollbar (scroll by dragging the knob/tap the track, passing through when the content fits, cannot be operated if Interactive=false).</summary>
public class ScrollbarTests
{
    private sealed class StubText : ITextRenderer
    {
        public Vec2 Measure(string text, float pixelSize) => new(text.Length * pixelSize, pixelSize);

        public void Draw(string text, Vec2 position, float pixelSize, Color color)
        {
        }
    }

    private static Widget Rows(int count, float rowHeight)
    {
        var rows = new Widget[count];
        for (int i = 0; i < count; i++)
        {
            rows[i] = new SizedBox { Height = Dimension.Px(rowHeight) };
        }

        return new Column { Children = rows };
    }

    // 右端ストリップ（幅8）のつまみを操作できるよう、80x100 の ScrollView を Scrollbar で包んで配置する。
    private static HamonRoot Mount(ScrollController controller, int rowCount, bool interactive = true)
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new Scrollbar
                {
                    Controller = controller,
                    Interactive = interactive,
                    Thickness = 8f,
                    Child = new ScrollView
                    {
                        Controller = controller,
                        Height = Dimension.Px(100f),
                        Width = Dimension.Px(80f),
                        Child = Rows(rowCount, 30f),
                    },
                },
            },
        });
        host.Update(new Size(400, 400));
        return host;
    }

    [Fact]
    public void ThumbDrag_Scrolls()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, rowCount: 10); // content 300 > viewport 100（max=200）
        Assert.Equal(0f, controller.Offset);

        // 右端ストリップ（x≈76）の上端＝つまみを掴み、下へドラッグ。
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 8f), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 60f), PointerPhase.Move, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 60f), PointerPhase.Up, 0.1f));
        host.Update(new Size(400, 400));

        Assert.True(controller.Offset > 50f, $"つまみドラッグでスクロールする: {controller.Offset}");
    }

    [Fact]
    public void TrackTap_JumpsScroll()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, rowCount: 10);

        // トラック下方（つまみの外）をタップ＝そこへジャンプ。
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 90f), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 90f), PointerPhase.Up, 0.05f));
        host.Update(new Size(400, 400));

        Assert.True(controller.Offset > 0f, $"トラックタップでジャンプする: {controller.Offset}");
    }

    [Fact]
    public void NoOverflow_DoesNotScroll()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, rowCount: 2); // content 60 < viewport 100（max=0）

        host.DispatchPointer(new PointerEvent(new Vec2(76f, 8f), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 60f), PointerPhase.Move, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 60f), PointerPhase.Up, 0.1f));
        host.Update(new Size(400, 400));

        Assert.Equal(0f, controller.Offset); // スクロール余地が無ければ何もしない
    }

    [Fact]
    public void Interactive_False_DoesNotDrag()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, rowCount: 10, interactive: false);

        host.DispatchPointer(new PointerEvent(new Vec2(76f, 8f), PointerPhase.Down, 0f));
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 60f), PointerPhase.Move, 0.05f));
        host.DispatchPointer(new PointerEvent(new Vec2(76f, 60f), PointerPhase.Up, 0.1f));
        host.Update(new Size(400, 400));

        Assert.Equal(0f, controller.Offset); // Interactive=false はつまみで動かない（表示のみ）
    }

    [Fact]
    public void ProgrammaticScroll_StillReflectedByBar()
    {
        var controller = new ScrollController();
        HamonRoot host = Mount(controller, rowCount: 10);

        controller.JumpTo(120f);
        host.Update(new Size(400, 400));

        Assert.Equal(120f, controller.Offset); // プログラム制御は従来どおり（バーはこれを描画に反映）
    }

    private sealed class NullPainter : IPainter
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

    private sealed class StubTexture : ITexture
    {
        public int Width => 32;

        public int Height => 32;
    }

    private sealed class TextureCounter : NullPainterBase
    {
        public int DrawTextureCount;

        public override void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint) => DrawTextureCount++;
    }

    private abstract class NullPainterBase : IPainter
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

        public virtual void DrawTexture(ITexture texture, Rect dest, RectInt source, Color tint)
        {
        }

        public object? PushClip(Rect rect) => null;

        public void PopClip(object? token)
        {
        }
    }

    private static HamonRoot MountCustom(ScrollController controller, int rowCount, float? thumbExtent, ScrollbarPartRenderer? thumbRenderer = null, ScrollbarPartRenderer? trackRenderer = null)
    {
        var host = new HamonRoot(new StubText());
        host.SetRoot(() => new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Start,
            Children = new Widget[]
            {
                new Scrollbar
                {
                    Controller = controller,
                    ThumbVisibility = true, // 常時表示＝必ず Paint が走る
                    ThumbExtent = thumbExtent,
                    ThumbRenderer = thumbRenderer,
                    TrackRenderer = trackRenderer,
                    Child = new ScrollView { Controller = controller, Height = Dimension.Px(100f), Width = Dimension.Px(80f), Child = Rows(rowCount, 30f) },
                },
            },
        });
        host.Update(new Size(400, 400));
        return host;
    }

    [Fact]
    public void ThumbExtent_Fixed_IgnoresContentSize()
    {
        Rect captured = default;
        var controller = new ScrollController();
        HamonRoot host = MountCustom(controller, rowCount: 100, thumbExtent: 50f,
            thumbRenderer: (in PaintContext _, Rect r, float _, bool _) => captured = r);
        host.Render(new NullPainter());

        // content が巨大でもつまみ長は固定 50（比例なら最小 24 に張り付くはず）。
        Assert.Equal(50f, captured.Height, 1);
    }

    [Fact]
    public void ThumbExtent_Default_IsProportional()
    {
        Rect captured = default;
        var controller = new ScrollController();
        HamonRoot host = MountCustom(controller, rowCount: 10, thumbExtent: null,
            thumbRenderer: (in PaintContext _, Rect r, float _, bool _) => captured = r);
        host.Render(new NullPainter());

        // content 300 / viewport 100 → 比例つまみ ≈ 33.3（固定 50 とは別）。
        Assert.InRange(captured.Height, 30f, 36f);
    }

    [Fact]
    public void TrackRenderer_ReceivesFullStrip()
    {
        Rect captured = default;
        var controller = new ScrollController();
        HamonRoot host = MountCustom(controller, rowCount: 10, thumbExtent: null,
            thumbRenderer: (in PaintContext _, Rect _, float _, bool _) => { },
            trackRenderer: (in PaintContext _, Rect r, float _, bool _) => captured = r);
        host.Render(new NullPainter());

        Assert.Equal(8f, captured.Width, 1);    // 太さ 8 のストリップ全体
        Assert.Equal(100f, captured.Height, 1); // viewport 高さ
    }

    [Fact]
    public void DrawNineSlice_EmitsNineQuads()
    {
        var counter = new TextureCounter();
        var ctx = new PaintContext(counter);
        ctx.DrawNineSlice(new StubTexture(), new Rect(0f, 0f, 100f, 100f), EdgeInsets.All(8f), Color.White);

        Assert.Equal(9, counter.DrawTextureCount); // 角4＋辺4＋中央1
    }
}
