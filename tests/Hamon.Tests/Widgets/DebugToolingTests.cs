using Hamon.Layout;
using Hamon.Widgets;
using Xunit;

namespace Hamon.Tests.Widgets;

/// <summary>Testing of debug diagnostics (tree/layout/hit testing/focus dump/exception catching bounds).</summary>
public class DebugToolingTests
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

    private static readonly Size Viewport = new(200, 200);

    [Fact]
    public void DumpWidgetTree_ListsTypesIndented()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Column { Children = new Widget[] { new SizedBox { Width = Dimension.Px(10), Height = Dimension.Px(10) } } });
        host.Update(Viewport);

        string dump = HamonDebug.DumpWidgetTree(host.Root);
        Assert.Contains("Column", dump);
        Assert.Contains("SizedBox", dump);
    }

    [Fact]
    public void DumpLayout_IncludesBounds()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(20) } });
        host.Update(Viewport);

        string dump = HamonDebug.DumpLayout(host.Root);
        Assert.Contains("40x20", dump);
    }

    [Fact]
    public void DumpHitTest_ShowsHitChain_AndPointerFlag()
    {
        var host = new HamonRoot(new StubTextRenderer());
        host.SetRoot(() => new Align { Alignment = Alignment.TopLeft, Child = new GestureDetector { OnTap = () => { }, Child = new SizedBox { Width = Dimension.Px(100), Height = Dimension.Px(100) } } });
        host.Update(Viewport);

        string dump = HamonDebug.DumpHitTest(host.Root, new Vec2(50, 50));
        Assert.Contains("GestureDetector", dump);
        Assert.Contains("[pointer]", dump);

        string miss = HamonDebug.DumpHitTest(host.Root, new Vec2(150, 150)); // SizedBox 100x100 の外
        Assert.Contains("no hit", miss);
    }

    [Fact]
    public void DumpFocusTree_MarksFocusedNode()
    {
        var host = new HamonRoot(new StubTextRenderer());
        var node = new FocusNode { Id = 7 };
        host.SetRoot(() => new Focus { Node = node, Autofocus = true, Child = new SizedBox { Width = Dimension.Px(40), Height = Dimension.Px(40) } });
        host.Update(Viewport);

        string dump = host.Focus.DumpFocusTree();
        Assert.Contains("id=7", dump);
        Assert.Contains("*", dump); // フォーカス中マーク
    }

    [Fact]
    public void ErrorBoundary_CatchesBuildException_DoesNotThrow()
    {
        Exception? caught = null;
        var host = new HamonRoot(new StubTextRenderer()) { OnError = e => caught = e };
        bool boom = true;
        host.SetRoot(() => boom ? throw new InvalidOperationException("build boom") : new SizedBox());

        host.Update(Viewport); // 例外を投げず捕捉される
        Assert.NotNull(caught);
        Assert.Contains("boom", caught!.Message);

        // 回復：次フレームは正常にビルドできる。
        boom = false;
        caught = null;
        host.MarkDirty();
        host.Update(Viewport);
        Assert.Null(caught);
    }

    [Fact]
    public void ErrorBoundary_CatchesRenderException()
    {
        Exception? caught = null;
        var host = new HamonRoot(new StubTextRenderer()) { OnError = e => caught = e };
        host.SetRoot(() => new ThrowOnPaint());
        host.Update(Viewport);

        host.Render(new StubPainter()); // 描画例外を捕捉
        Assert.NotNull(caught);
    }

    // 描画時に必ず例外を投げる検証用ウィジェット。
    private sealed class ThrowOnPaint : Widget, IRenderConfig
    {
        Style IRenderConfig.Style => new() { Kind = LayoutKind.Box, Width = Dimension.Px(10), Height = Dimension.Px(10) };
        IReadOnlyList<Widget>? IRenderConfig.Children => null;
        Color? IRenderConfig.Background => null;
        Func<BoxConstraints, Size>? IRenderConfig.Measure => null;

        public override Element CreateElement() => new ThrowOnPaintElement(this);
    }

    private sealed class ThrowOnPaintElement : RenderElement
    {
        public ThrowOnPaintElement(Widget w)
            : base(w)
        {
        }

        public override void Paint(in PaintContext context) => throw new InvalidOperationException("paint boom");
    }
}
